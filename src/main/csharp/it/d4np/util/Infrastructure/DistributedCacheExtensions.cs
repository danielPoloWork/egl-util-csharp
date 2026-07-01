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
    /// The deserialised value, or <see langword="default"/> if the key is not present. For a value
    /// type, an absent key and a stored <c>default(T)</c> both yield <c>default</c>; callers needing to
    /// tell them apart should use a reference type or a nullable value type.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="System.Text.Json.JsonException">The stored bytes are not valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T?> GetAsync<T>(
        this IDistributedCache cache, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(key);

        byte[]? bytes = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return bytes is { Length: > 0 } ? CacheSerializer.Deserialize<T>(bytes) : default;
    }

    /// <summary>Serialises <paramref name="value"/> as compact UTF-8 JSON and stores it under <paramref name="key"/>.</summary>
    /// <typeparam name="T">The value type to serialise.</typeparam>
    /// <param name="cache">The distributed cache; must not be <see langword="null"/>.</param>
    /// <param name="key">The cache key; must not be <see langword="null"/> or empty.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="options">Optional entry options (expiration); a fresh instance is used when <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty.</exception>
    public static Task SetAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(key);

        byte[] bytes = CacheSerializer.Serialize(value);
        return cache.SetAsync(key, bytes, options ?? new DistributedCacheEntryOptions(), cancellationToken);
    }
}
