using It.D4np.Util.Application;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// A two-level <see cref="ICacheService"/>: a fast in-memory L1 (<see cref="IMemoryCache"/>) in front
/// of a shared distributed L2 (<see cref="IDistributedCache"/>). It acts as a caching Proxy over the
/// value factory and the distributed store, realising the read-through / cache-aside strategy.
/// </summary>
/// <remarks>
/// On a read, L1 is checked first, then L2 (a hit there re-populates L1); a miss invokes the factory
/// and writes both levels. Presence in L2 is decided from the stored bytes, not the deserialised
/// value, so a cached <c>default(T)</c> is not mistaken for a miss. This implementation does not
/// serialise concurrent misses for the same key, so under contention the factory may run more than
/// once per key (last write wins).
/// </remarks>
public sealed class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;

    /// <summary>Creates a two-level cache over the given in-memory and distributed caches.</summary>
    /// <param name="memoryCache">The L1 in-memory cache.</param>
    /// <param name="distributedCache">The L2 distributed cache.</param>
    /// <exception cref="ArgumentNullException">Either cache is <see langword="null"/>.</exception>
    public CacheService(IMemoryCache memoryCache, IDistributedCache distributedCache)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(distributedCache);
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
    }

    /// <inheritdoc />
    /// <exception cref="System.Text.Json.JsonException">
    /// A stored distributed-cache entry holds malformed JSON, or <typeparamref name="T"/> is not
    /// JSON-serializable. This signals data corruption or a programming error, not expected control flow.
    /// </exception>
    public async ValueTask<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);

        // L1 — in-memory. TryGetValue returns a bool, so a cached default(T) is unambiguous.
        if (_memoryCache.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        // L2 — distributed. Decide presence from the raw bytes, then deserialise.
        byte[]? bytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (bytes is { Length: > 0 })
        {
            T? fromDistributed = CacheSerializer.Deserialize<T>(bytes);
            SetMemory(key, fromDistributed, ttl);
            return fromDistributed;
        }

        // Miss — create, then write both levels. A null result is returned but not cached.
        T created = await factory(cancellationToken).ConfigureAwait(false);
        if (created is null)
        {
            return created;
        }

        await _distributedCache
            .SetAsync(key, CacheSerializer.Serialize(created), ToDistributedOptions(ttl), cancellationToken)
            .ConfigureAwait(false);
        SetMemory(key, created, ttl);
        return created;
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        _memoryCache.Remove(key);
        await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private void SetMemory<T>(string key, T value, TimeSpan? ttl)
    {
        if (ttl is { } expiration)
        {
            _memoryCache.Set(key, value, expiration);
        }
        else
        {
            _memoryCache.Set(key, value);
        }
    }

    private static DistributedCacheEntryOptions ToDistributedOptions(TimeSpan? ttl) =>
        ttl is { } expiration
            ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration }
            : new DistributedCacheEntryOptions();
}
