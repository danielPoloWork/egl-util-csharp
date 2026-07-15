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
/// value, so a cached <c>default(T)</c> is not mistaken for a miss. By default this implementation does
/// not serialise concurrent misses for the same key, so under contention the factory may run more than
/// once per key (last write wins); construct it with <c>coalesceConcurrentMisses: true</c> to gate
/// concurrent misses per key so the factory runs at most once per key per node (#50, ADR-0039).
/// </para>
/// <para>
/// <b>Promotion restarts the TTL.</b> When an L2 hit re-populates L1, the entry is written with the
/// <em>current</em> call's <c>ttl</c> restarted from now — not the original writer's remaining lifetime.
/// So a value read from L2 just before it expires lives another full <c>ttl</c> window in that node's L1,
/// making the effective per-node staleness bound approach <b>~2× ttl</b>; and when callers pass different
/// ttls for the same key, the promoted L1 lifetime is whichever the <em>promoting</em> caller used. Choose
/// a <c>ttl</c> that accounts for this doubled bound where freshness matters. Distinct from the cross-node
/// staleness below (#42); see ADR-0009.
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
/// <para>
/// <b>L2 outages.</b> A provider fault on the <b>post-factory L2 write</b> does not fail the call: once
/// the factory has produced a value, an <see cref="IDistributedCache"/> write fault is swallowed and the
/// value is returned (and still promoted to L1), so a distributed-cache blip never turns a successful
/// computation into a business-operation failure — the entry is simply not stored in L2 that time.
/// Cancellation is <b>not</b> swallowed: an <see cref="OperationCanceledException"/> from the supplied
/// token propagates. All <b>other</b> L2 faults propagate: a fault <i>reading</i> L2 (before the factory
/// runs) fails the call, as does an L2 fault in <see cref="RemoveAsync"/> — which removes L2 before L1
/// (#91), so a fault there leaves <b>both</b> levels populated. See ADR-0024.
/// </para>
/// </remarks>
public sealed class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly bool _coalesceConcurrentMisses;

    // Per-key gates for opt-in coalescing (#50). Ref-counted: an entry is created on first use for a key and
    // removed when its last waiter leaves, so an unbounded key space does not leak SemaphoreSlims. Guarded by
    // locking the dictionary. Never touched when coalescing is disabled (the fast path returns first).
    private readonly Dictionary<string, KeyGate> _gates = new(StringComparer.Ordinal);

    /// <summary>Creates a two-level cache over the given in-memory and distributed caches.</summary>
    /// <param name="memoryCache">The L1 in-memory cache.</param>
    /// <param name="distributedCache">The L2 distributed cache.</param>
    /// <exception cref="ArgumentNullException">Either cache is <see langword="null"/>.</exception>
    public CacheService(IMemoryCache memoryCache, IDistributedCache distributedCache)
        : this(memoryCache, distributedCache, coalesceConcurrentMisses: false)
    {
    }

    /// <summary>Creates a two-level cache, optionally coalescing concurrent misses for the same key.</summary>
    /// <param name="memoryCache">The L1 in-memory cache.</param>
    /// <param name="distributedCache">The L2 distributed cache.</param>
    /// <param name="coalesceConcurrentMisses">
    /// When <see langword="true"/>, concurrent <see cref="GetOrCreateAsync"/> calls that miss for the same key
    /// are serialised behind a per-key gate so the factory runs at most once per key per node — the others
    /// await and return the winner's value. The default <see langword="false"/> keeps the lock-free behaviour
    /// where each concurrent miss runs the factory (last write wins). Opt in when the factory is expensive and
    /// concurrent misses are likely (#50, ADR-0039). The gate is per-instance and per-node; it does not
    /// coordinate across processes.
    /// </param>
    /// <exception cref="ArgumentNullException">Either cache is <see langword="null"/>.</exception>
    public CacheService(IMemoryCache memoryCache, IDistributedCache distributedCache, bool coalesceConcurrentMisses)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(distributedCache);
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _coalesceConcurrentMisses = coalesceConcurrentMisses;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is specified and not strictly positive.</exception>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="T"/> is not round-trippable by declared-type serialization — it is
    /// <see cref="object"/>, an interface, or an abstract class, or the factory returns a runtime subtype
    /// of <typeparamref name="T"/> (which would silently lose the subtype's members). See #33 / ADR-0023.
    /// </exception>
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

        // The cache serialises the declared type T; reject a non-round-trippable T (object/interface/
        // abstract) up front — before touching either level or running the factory — so the misuse fails
        // fast rather than after the factory produces a value that cannot be stored faithfully (#33).
        CacheSerializer.EnsureDeclaredTypeIsRoundTrippable<T>();

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

        // Fast path (the default): resolve the miss without serialising concurrent callers — under
        // contention the factory may run more than once per key (last write wins), the documented trade-off.
        if (!_coalesceConcurrentMisses)
        {
            return await ResolveOnMissAsync(key, factory, ttl, cancellationToken).ConfigureAwait(false);
        }

        // Opt-in coalescing (#50, ADR-0039): serialise concurrent misses for the same key behind a per-key
        // gate so at most one factory runs per key per node. A waiter re-checks L1 after the gate (the winner
        // populates it) and ResolveOnMissAsync re-checks L2, so a blocked caller returns the winner's value
        // rather than running the factory again. The gate is ref-counted and removed when its last waiter
        // leaves, so an unbounded key space does not leak semaphores.
        KeyGate gate = RentGate(key);
        var acquired = false;
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            // Double-checked: the caller that just held the gate may have populated L1.
            if (_memoryCache.TryGetValue(key, out T? promoted))
            {
                return promoted;
            }

            return await ResolveOnMissAsync(key, factory, ttl, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                gate.Semaphore.Release();
            }

            ReturnGate(key, gate);
        }
    }

    // The miss path shared by the fast and coalesced routes: check L2, else run the factory and write both
    // levels (a null result is returned but not cached). Under coalescing this runs inside the per-key gate,
    // so its L2 check catches a value a concurrent winner just wrote and the factory runs at most once.
    private async ValueTask<T?> ResolveOnMissAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl, CancellationToken cancellationToken)
    {
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

        // Serialize before the write so a serialization/contract error (a programming error, e.g. a
        // non-round-trippable type) still surfaces; only the distributed-store write itself is treated
        // as an outage below.
        byte[] payload = CacheSerializer.Serialize(created);
        try
        {
            await _distributedCache
                .SetAsync(key, payload, ToDistributedOptions(ttl), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The value was computed successfully; a distributed-cache write fault (a provider outage)
            // must not fail the business operation, so it is swallowed and the value returned (still
            // promoted to L1 below), just not stored in L2 this time. Cancellation is excluded from the
            // filter so the caller's cancellation contract is honoured (#62, ADR-0024).
        }

        SetMemory(key, created, ttl);
        return created;
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        // Delete L2 first, then L1 (standard cache-aside order). Removing L1 last means a concurrent
        // same-node read hits L1 for the whole L2 round-trip and never re-reads the still-present L2, so it
        // cannot re-promote the removed value into L1 — shrinking that window from a full L2 round-trip to
        // the nanoseconds between these two calls (#91). A narrow residual remains (a read that had already
        // missed L1 could complete its promotion after the L1 removal) — the same lock-free two-level
        // limitation as the cross-node staleness in #42.
        await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        _memoryCache.Remove(key);
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

    // Rents the per-key gate (opt-in coalescing), creating it on first use and incrementing its waiter count
    // under the lock so it cannot be removed/disposed while in use. Must be paired with ReturnGate in a finally.
    private KeyGate RentGate(string key)
    {
        lock (_gates)
        {
            if (!_gates.TryGetValue(key, out KeyGate? gate))
            {
                gate = new KeyGate();
                _gates[key] = gate;
            }

            gate.Waiters++;
            return gate;
        }
    }

    // Returns the gate; when its last waiter leaves, the entry is removed and its semaphore disposed so a
    // one-shot key does not leak. Waiters is guarded here and stays > 0 while any renter holds a reference,
    // so no disposal races with a concurrent WaitAsync on the same gate.
    private void ReturnGate(string key, KeyGate gate)
    {
        lock (_gates)
        {
            if (--gate.Waiters == 0)
            {
                _gates.Remove(key);
                gate.Dispose();
            }
        }
    }

    // A per-key coalescing gate: a binary semaphore plus a waiter count (guarded by the _gates lock).
    private sealed class KeyGate : IDisposable
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Waiters { get; set; }

        public void Dispose() => Semaphore.Dispose();
    }
}
