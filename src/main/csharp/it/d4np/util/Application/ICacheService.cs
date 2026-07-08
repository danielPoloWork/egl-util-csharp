namespace It.D4np.Util.Application;

/// <summary>
/// A two-level (in-memory L1 + distributed L2) cache abstraction. <see cref="GetOrCreateAsync"/>
/// serves a cached value if present at either level and otherwise invokes a factory, stores the
/// result at both levels, and returns it (the read-through / cache-aside strategy).
/// <see cref="RemoveAsync"/> invalidates a key at both levels.
/// </summary>
/// <remarks>
/// An implementation backed by a serialising distributed store may throw a serialisation exception
/// (for the built-in <c>CacheService</c>, <see cref="System.Text.Json.JsonException"/>) if a stored
/// entry cannot be deserialised or a value cannot be serialised — a data-corruption or programming
/// error, not expected control flow.
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// Returns the value cached under <paramref name="key"/> if present (checking the in-memory level
    /// first, then the distributed level); otherwise invokes <paramref name="factory"/>, caches its
    /// result at both levels with the given time-to-live, and returns it.
    /// </summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="key">The cache key; must not be <see langword="null"/> or empty.</param>
    /// <param name="factory">
    /// Produces the value on a cache miss; invoked at most once per miss on the calling path. A
    /// <see langword="null"/> result is returned to the caller but not cached.
    /// </param>
    /// <param name="ttl">
    /// How long the entry stays valid; must be a <b>strictly positive</b> duration when specified.
    /// <see langword="null"/> means no explicit expiration (the entry lives until evicted by the
    /// underlying caches).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cached or freshly created value; may be <see langword="null"/> if the factory returned <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is specified and not strictly positive.</exception>
    ValueTask<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>Invalidates <paramref name="key"/> at both cache levels.</summary>
    /// <remarks>
    /// This removes the entry from the calling instance's in-memory L1 and the shared distributed L2. It
    /// does <b>not</b> invalidate other instances' L1, which keep serving their own copy until that entry
    /// expires or is evicted (with no TTL, until it is evicted or explicitly removed) — so in a
    /// multi-instance deployment cache with a bounded <c>ttl</c> to cap the cross-node staleness window.
    /// For the built-in <c>CacheService</c> see its remarks and ADR-0009.
    /// </remarks>
    /// <param name="key">The cache key to remove; must not be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty.</exception>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
