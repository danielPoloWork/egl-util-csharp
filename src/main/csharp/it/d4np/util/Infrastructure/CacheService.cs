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
/// <para>
/// On a read, L1 is checked first, then L2 (a hit there re-populates L1); a miss invokes the factory
/// and writes both levels. Presence in L2 is decided from the stored bytes, not the deserialised
/// value, so a cached <c>default(T)</c> is not mistaken for a miss. This implementation does not
/// serialise concurrent misses for the same key, so under contention the factory may run more than
/// once per key (last write wins).
/// </para>
/// <para>
/// Every L1 entry is written with an explicit <see cref="MemoryCacheEntryOptions.Size"/> of one unit,
/// so the service works whether or not the supplied <see cref="IMemoryCache"/> was registered with a
/// <c>SizeLimit</c> (a size-limited cache requires a per-entry size, an unlimited one ignores it).
/// When the cache is size-limited, its <c>SizeLimit</c> therefore bounds the <b>number</b> of
/// entries this service keeps in L1.
/// </para>
/// <para>
/// <b>Cross-node coherence.</b> L1 is <b>per process</b>; only L2 is shared. A write or
/// <see cref="RemoveAsync"/> updates the calling process's L1 and the shared L2, but there is no
/// invalidation channel to other instances, so a peer keeps serving its own L1 copy until that entry
/// expires or is evicted — indefinitely if it was cached with <c>ttl = null</c>. This is the expected
/// behaviour of a two-level cache with no invalidation bus, not a defect. In a multi-instance
/// deployment pass a <b>bounded</b> <c>ttl</c> so every L1 entry self-expires and the staleness window
/// is capped; a removal or update is only guaranteed to be observed everywhere once each instance's L1
/// entry has expired or been evicted. See ADR-0009.
/// </para>
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is specified and not strictly positive.</exception>
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

        // A non-positive TTL is invalid: the underlying entry-options setter rejects it, so validate
        // up-front to fail fast with context — before running the factory or touching either level.
        if (ttl is { } lifetime && lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ttl), ttl, "The cache time-to-live must be a positive duration when specified.");
        }

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

            // A stored JSON `null` (e.g. written by another client sharing the store) is treated as a
            // miss, not promoted into L1 — otherwise it would poison L1 with a null "hit" and every
            // later read would skip the factory. This keeps the never-cache-null policy. A default
            // value type (e.g. 0) is not null, so it is still a real hit.
            if (fromDistributed is not null)
            {
                SetMemory(key, fromDistributed, ttl);
                return fromDistributed;
            }
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

    // Each L1 entry is written with an explicit size of one unit. A size-limited IMemoryCache
    // (registered with MemoryCacheOptions.SizeLimit) requires every entry to specify a Size, so
    // without this the first read-through write throws InvalidOperationException; an unlimited cache
    // ignores the Size, so it is safe to set unconditionally. Consequently, for such a cache the
    // SizeLimit bounds the number of CacheService entries (see the type remarks).
    private void SetMemory<T>(string key, T value, TimeSpan? ttl)
    {
        var options = new MemoryCacheEntryOptions { Size = 1 };
        if (ttl is { } expiration)
        {
            options.AbsoluteExpirationRelativeToNow = expiration;
        }

        _memoryCache.Set(key, value, options);
    }

    private static DistributedCacheEntryOptions ToDistributedOptions(TimeSpan? ttl) =>
        ttl is { } expiration
            ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration }
            : new DistributedCacheEntryOptions();
}
