using Microsoft.Extensions.Caching.Distributed;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Typed get/set helpers over <see cref="IDistributedCache"/> that serialise values as compact UTF-8
/// <c>System.Text.Json</c>, adapting the byte-oriented cache interface to strongly-typed access.
/// </summary>
public static class DistributedCacheExtensions
{
    /// <summary>
    /// Reads and deserialises the value stored under <paramref name="key"/>, or returns
    /// <see langword="default"/> when the key is absent.
    /// </summary>
    /// <typeparam name="T">The value type to deserialise.</typeparam>
    /// <param name="cache">The distributed cache; must not be <see langword="null"/>.</param>
    /// <param name="key">The cache key; must not be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The deserialised value, or <see langword="default"/> if the key is not present. An absent key
    /// and a stored <c>default(T)</c> both yield <c>default</c> — for a value type a stored <c>0</c>,
    /// and for a reference type a stored JSON <c>null</c> — so an absent key cannot be told apart from a
    /// stored default. (<see cref="SetAsync"/> refuses to write a <see langword="null"/>, so a stored
    /// JSON <c>null</c> only arises from another writer.)
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not round-trippable by declared-type serialization — it is <see cref="object"/>, an interface, or an abstract class (#33 / ADR-0023).</exception>
    /// <exception cref="System.Text.Json.JsonException">The stored bytes are not valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T?> GetAsync<T>(
        this IDistributedCache cache, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(key);
        // T is part of the cache contract: reject a non-round-trippable declared type up front, even when
        // the key turns out to be absent, so the misuse is reported consistently (#33).
        CacheSerializer.EnsureDeclaredTypeIsRoundTrippable<T>();

        byte[]? bytes = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return bytes is { Length: > 0 } ? CacheSerializer.Deserialize<T>(bytes) : default;
    }

    /// <summary>Serialises <paramref name="value"/> as compact UTF-8 JSON and stores it under <paramref name="key"/>.</summary>
    /// <typeparam name="T">The value type to serialise.</typeparam>
    /// <param name="cache">The distributed cache; must not be <see langword="null"/>.</param>
    /// <param name="key">The cache key; must not be <see langword="null"/> or empty.</param>
    /// <param name="value">The value to store; must not be <see langword="null"/> (a stored JSON <c>null</c> is indistinguishable from an absent key and never cached — matching the <see cref="It.D4np.Util.Application.ICacheService"/> contract). Its runtime type must equal <typeparamref name="T"/>.</param>
    /// <param name="options">Optional entry options (expiration); a fresh instance is used when <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is <see cref="object"/>, an interface, or an abstract class, or <paramref name="value"/>'s runtime type is a subtype of <typeparamref name="T"/> (declared-type serialization would lose its members; #33 / ADR-0023).</exception>
    public static Task SetAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(key);
        // Never store a JSON `null`: it is indistinguishable from an absent key on read and would
        // otherwise be promoted into CacheService's L1 as a null "hit" (never-cache-null policy).
        ArgumentNullException.ThrowIfNull(value);

        // Serialize enforces the round-trippable-T contract (declared type + runtime type == T) (#33).
        byte[] bytes = CacheSerializer.Serialize(value);
        return cache.SetAsync(key, bytes, options ?? new DistributedCacheEntryOptions(), cancellationToken);
    }
}
