using System.Text;
using System.Text.Json;

using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace It.D4np.Util.Tests;

public sealed class CacheServiceTests : IDisposable
{
    private readonly MemoryCache _l1 = new(new MemoryCacheOptions());
    private readonly IDistributedCache _l2 =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly CacheService _cache;

    public CacheServiceTests() => _cache = new CacheService(_l1, _l2);

    public void Dispose() => _l1.Dispose();

    private sealed record Widget(int Id, string Name);

    private interface IShape { }

    private abstract record ShapeBase(int Sides);

    private sealed record Circle(int Sides, double Radius) : ShapeBase(Sides), IShape;

    private record Animal(string Name); // concrete, non-sealed base

    private sealed record Dog(string Name, string Breed) : Animal(Name);

    [Fact]
    public async Task GetOrCreateAsync_invokes_the_factory_once_then_serves_from_cache()
    {
        int calls = 0;
        Task<Widget> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new Widget(1, "a"));
        }

        Widget? first = await _cache.GetOrCreateAsync("w:1", Factory);
        Widget? second = await _cache.GetOrCreateAsync("w:1", Factory);

        Assert.Equal(new Widget(1, "a"), first);
        Assert.Equal(first, second);
        Assert.Equal(1, calls); // the second read is served from L1
    }

    [Fact]
    public async Task GetOrCreateAsync_promotes_a_distributed_hit_into_the_memory_level()
    {
        await _l2.SetAsync("w:2", new Widget(2, "b")); // seed only L2
        int calls = 0;
        Task<Widget> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new Widget(0, "x"));
        }

        Widget? read = await _cache.GetOrCreateAsync("w:2", Factory);

        Assert.Equal(new Widget(2, "b"), read);
        Assert.Equal(0, calls); // served from L2, factory never called
        Assert.True(_l1.TryGetValue("w:2", out Widget? promoted));
        Assert.Equal(new Widget(2, "b"), promoted); // L2 hit promoted into L1
    }

    [Fact]
    public async Task GetOrCreateAsync_writes_through_to_the_distributed_level()
    {
        await _cache.GetOrCreateAsync("w:4", _ => Task.FromResult(new Widget(4, "d")));

        Assert.Equal(new Widget(4, "d"), await _l2.GetAsync<Widget>("w:4"));
    }

    [Fact]
    public async Task RemoveAsync_invalidates_both_levels()
    {
        await _cache.GetOrCreateAsync("w:3", _ => Task.FromResult(new Widget(3, "c")));

        await _cache.RemoveAsync("w:3");

        Assert.False(_l1.TryGetValue("w:3", out Widget? _));
        Assert.Null(await _l2.GetAsync<Widget>("w:3"));
    }

    [Fact]
    public async Task GetOrCreateAsync_does_not_cache_a_null_factory_result()
    {
        int calls = 0;
        Task<Widget?> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult<Widget?>(null);
        }

        Widget? first = await _cache.GetOrCreateAsync("w:5", Factory);
        Widget? second = await _cache.GetOrCreateAsync("w:5", Factory);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, calls); // null is not cached, so the factory runs again
        Assert.False(_l1.TryGetValue("w:5", out _));
    }

    [Fact]
    public async Task GetOrCreateAsync_caches_a_default_value_type_rather_than_treating_it_as_a_miss()
    {
        int calls = 0;
        Task<int> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(0);
        }

        int first = await _cache.GetOrCreateAsync("n:1", Factory);
        int second = await _cache.GetOrCreateAsync("n:1", Factory);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(1, calls); // 0 is a real cached value, not a miss
    }

    [Fact]
    public async Task GetOrCreateAsync_serves_a_default_value_type_from_the_distributed_level()
    {
        // Regression guard: L2 presence is decided from the stored bytes, so a stored 0 is not a miss.
        await _l2.SetAsync("n:2", 0);
        int calls = 0;
        Task<int> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(99);
        }

        int read = await _cache.GetOrCreateAsync("n:2", Factory);

        Assert.Equal(0, read);
        Assert.Equal(0, calls); // served from L2 despite the value being default(int)
    }

    [Fact]
    public async Task GetOrCreateAsync_treats_a_json_null_L2_entry_as_a_miss()
    {
        // Regression for #59: a JSON "null" in L2 (e.g. written by another client) must not be promoted
        // into L1 as a null "hit" — it is treated as a miss so the factory runs and its value is cached.
        await _l2.SetAsync("null:1", Encoding.UTF8.GetBytes("null"), new DistributedCacheEntryOptions());
        var calls = 0;
        Task<Widget?> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult<Widget?>(new Widget(1, "a"));
        }

        Widget? first = await _cache.GetOrCreateAsync("null:1", Factory);
        Widget? second = await _cache.GetOrCreateAsync("null:1", Factory);

        Assert.Equal(new Widget(1, "a"), first);  // the JSON-null was a miss; the factory produced the value
        Assert.Equal(new Widget(1, "a"), second);
        Assert.Equal(1, calls); // factory ran once (not poisoned); the second call hit the real L1 value
    }

    [Fact]
    public async Task GetOrCreateAsync_propagates_a_serialization_error_for_a_corrupt_distributed_entry()
    {
        // Seed L2 with raw bytes that are not valid JSON. The instance IDistributedCache.SetAsync is
        // preferred over the typed extension, so the bytes are stored verbatim (not JSON-wrapped).
        byte[] corrupt = Encoding.UTF8.GetBytes("{ this is not json ");
        await _l2.SetAsync("corrupt:1", corrupt, new DistributedCacheEntryOptions());

        await Assert.ThrowsAnyAsync<JsonException>(
            () => _cache.GetOrCreateAsync("corrupt:1", _ => Task.FromResult(new Widget(1, "a"))).AsTask());
    }

    // Regression for #20: a common production registration is AddMemoryCache(o => o.SizeLimit = N),
    // which requires every entry to specify a Size. The read-through write must not throw.
    [Fact]
    public async Task GetOrCreateAsync_works_against_a_size_limited_memory_cache()
    {
        using var limited = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var cache = new CacheService(limited, _l2);
        var calls = 0;
        Task<Widget> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new Widget(7, "g"));
        }

        Widget? first = await cache.GetOrCreateAsync("s:1", Factory);  // read-through write, no throw
        Widget? second = await cache.GetOrCreateAsync("s:1", Factory); // served from the size-limited L1

        Assert.Equal(new Widget(7, "g"), first);
        Assert.Equal(first, second);
        Assert.Equal(1, calls);
        Assert.True(limited.TryGetValue("s:1", out Widget? _)); // the entry was actually stored in L1
    }

    [Fact]
    public async Task GetOrCreateAsync_with_ttl_works_against_a_size_limited_memory_cache()
    {
        using var limited = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var cache = new CacheService(limited, _l2);

        // The ttl branch (AbsoluteExpirationRelativeToNow) must also specify a size.
        Widget? value = await cache.GetOrCreateAsync(
            "s:2", _ => Task.FromResult(new Widget(8, "h")), TimeSpan.FromMinutes(5));

        Assert.Equal(new Widget(8, "h"), value);
        Assert.True(limited.TryGetValue("s:2", out _));
    }

    [Fact]
    public async Task GetOrCreateAsync_promotes_a_distributed_hit_into_a_size_limited_memory_cache()
    {
        using var limited = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var cache = new CacheService(limited, _l2);
        await _l2.SetAsync("s:3", new Widget(9, "i")); // seed only L2

        Widget? read = await cache.GetOrCreateAsync("s:3", _ => Task.FromResult(new Widget(0, "x")));

        Assert.Equal(new Widget(9, "i"), read);
        Assert.True(limited.TryGetValue("s:3", out Widget? promoted)); // promotion write did not throw
        Assert.Equal(new Widget(9, "i"), promoted);
    }

    [Fact]
    public void Constructor_rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new CacheService(null!, _l2));
        Assert.Throws<ArgumentNullException>(() => new CacheService(_l1, null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetOrCreateAsync_rejects_a_null_or_empty_key(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _cache.GetOrCreateAsync(key!, _ => Task.FromResult(1)).AsTask());
    }

    [Fact]
    public async Task GetOrCreateAsync_rejects_a_null_factory()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _cache.GetOrCreateAsync<int>("k", null!).AsTask());
    }

    [Theory]
    [InlineData(0)]   // TimeSpan.Zero
    [InlineData(-5)]  // negative (e.g. a deadline already in the past)
    public async Task GetOrCreateAsync_rejects_a_non_positive_ttl(int ttlSeconds)
    {
        var factoryRan = false;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _cache.GetOrCreateAsync(
            "ttl:bad",
            _ =>
            {
                factoryRan = true;
                return Task.FromResult(1);
            },
            TimeSpan.FromSeconds(ttlSeconds)).AsTask());

        Assert.False(factoryRan); // validated up-front, before the factory runs or any level is written
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task RemoveAsync_rejects_a_null_or_empty_key(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.RemoveAsync(key!).AsTask());
    }

    [Fact]
    public async Task GetOrCreateAsync_applies_a_positive_ttl_as_absolute_expiration_to_both_levels()
    {
        // #101: on a miss, the ttl must be written to BOTH levels as AbsoluteExpirationRelativeToNow
        // (not sliding, not one level only). Capturing spies assert the options directly — deterministic,
        // no waiting.
        var ttl = TimeSpan.FromMinutes(5);
        var l1 = new CapturingMemoryCache();
        var l2 = new CapturingDistributedCache();
        var cache = new CacheService(l1, l2);

        await cache.GetOrCreateAsync("k", _ => Task.FromResult("v"), ttl);

        Assert.NotNull(l1.LastEntry);
        Assert.Equal(ttl, l1.LastEntry!.AbsoluteExpirationRelativeToNow); // L1 carries the ttl...
        Assert.Null(l1.LastEntry.SlidingExpiration);                      // ...as absolute-relative, not sliding
        Assert.NotNull(l2.LastOptions);
        Assert.Equal(ttl, l2.LastOptions!.AbsoluteExpirationRelativeToNow); // L2 carries the same ttl...
        Assert.Null(l2.LastOptions.SlidingExpiration);
        Assert.Null(l2.LastOptions.AbsoluteExpiration);                     // ...not sliding, not a fixed instant
    }

    [Fact]
    public async Task GetOrCreateAsync_without_a_ttl_writes_entries_with_no_expiration()
    {
        // #101: absent a ttl, neither level gets an expiration (a regression that always set one, or
        // defaulted to sliding, would fail here).
        var l1 = new CapturingMemoryCache();
        var l2 = new CapturingDistributedCache();
        var cache = new CacheService(l1, l2);

        await cache.GetOrCreateAsync("k", _ => Task.FromResult("v"));

        Assert.NotNull(l1.LastEntry);
        Assert.Null(l1.LastEntry!.AbsoluteExpirationRelativeToNow);
        Assert.Null(l1.LastEntry.SlidingExpiration);
        Assert.NotNull(l2.LastOptions);
        Assert.Null(l2.LastOptions!.AbsoluteExpirationRelativeToNow);
        Assert.Null(l2.LastOptions.SlidingExpiration);
        Assert.Null(l2.LastOptions.AbsoluteExpiration);
    }

    [Fact]
    public async Task GetOrCreateAsync_under_concurrent_misses_runs_the_factory_per_call_and_converges_to_one_value()
    {
        // #51: the documented concurrent-miss contract — CacheService does NOT serialise concurrent misses
        // for the same key, so the factory may run more than once (last write wins, one coherent cached
        // value). A barrier forces all N calls past the L1/L2 miss check before any of them writes, so the
        // hazard is exercised deterministically rather than relying on a wall-clock race.
        const int n = 16;
        var calls = 0;
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Widget?> Invoke() => _cache.GetOrCreateAsync<Widget>("hot", async _ =>
        {
            int id = Interlocked.Increment(ref calls);
            if (id == n)
            {
                barrier.SetResult(); // the Nth miss releases them all — every call is now past the miss check
            }

            await barrier.Task.ConfigureAwait(false);
            return new Widget(id, "v" + id);
        }).AsTask();

        Widget?[] results = await Task.WhenAll(Enumerable.Range(0, n).Select(_ => Invoke()));

        // Every call missed and ran its own factory (not serialised), each returning its own distinct value.
        Assert.Equal(n, calls);
        Assert.Equal(Enumerable.Range(1, n), results.Select(widget => widget!.Id).OrderBy(id => id));

        // The storm converged to a single coherent cached value: later reads hit the cache (the factory must
        // not run again) and return one of the produced values, stably.
        Widget? served = await _cache.GetOrCreateAsync<Widget>(
            "hot", _ => throw new InvalidOperationException("post-storm read must hit L1; the factory must not re-run"));
        Widget? servedAgain = await _cache.GetOrCreateAsync<Widget>(
            "hot", _ => throw new InvalidOperationException("post-storm read must hit L1; the factory must not re-run"));

        Assert.Equal(n, calls); // unchanged — the coherent reads were cache hits
        Assert.NotNull(served);
        Assert.InRange(served!.Id, 1, n);
        Assert.Equal(served, servedAgain);
    }

    [Fact]
    public async Task GetOrCreateAsync_rejects_a_polymorphic_declared_type_before_running_the_factory()
    {
        // #33: the cache serialises the declared type T, so an object/interface/abstract T cannot
        // round-trip (an empty {} for object) and would silently lose the concrete value's members. It is
        // rejected up front — before the factory runs — rather than stored lossy.
        var ranObject = false;
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetOrCreateAsync<object>(
            "poly:obj",
            _ =>
            {
                ranObject = true;
                return Task.FromResult<object>(new Circle(1, 2.0));
            }).AsTask());
        Assert.False(ranObject); // guarded before the factory ran

        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetOrCreateAsync<IShape>(
            "poly:iface", _ => Task.FromResult<IShape>(new Circle(1, 2.0))).AsTask());

        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetOrCreateAsync<ShapeBase>(
            "poly:abstract", _ => Task.FromResult<ShapeBase>(new Circle(1, 2.0))).AsTask());
    }

    [Fact]
    public async Task GetOrCreateAsync_rejects_a_runtime_subtype_of_the_declared_type()
    {
        // #33: even a concrete declared type must match the value's runtime type — a Dog stored under
        // GetOrCreateAsync<Animal> would serialise only Animal's members and silently lose Breed.
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetOrCreateAsync<Animal>(
            "poly:sub", _ => Task.FromResult<Animal>(new Dog("Rex", "Lab"))).AsTask());
    }

    [Fact]
    public async Task GetOrCreateAsync_round_trips_a_concrete_type_including_a_nullable_value_type()
    {
        // #33 happy path: a concrete type whose runtime type equals T round-trips, and a Nullable<T> value
        // type must not trip the runtime-type guard (a boxed int? reports its underlying type, int).
        Widget? widget = await _cache.GetOrCreateAsync("ok:widget", _ => Task.FromResult(new Widget(1, "a")));
        Assert.Equal(new Widget(1, "a"), widget);

        int? number = await _cache.GetOrCreateAsync<int?>("ok:nullable", _ => Task.FromResult<int?>(7));
        Assert.Equal(7, number);
    }

    // Always-miss IMemoryCache that captures the options of the last entry created, so a test can assert
    // exactly what expiration CacheService applied to L1 without waiting for a clock.
    private sealed class CapturingMemoryCache : IMemoryCache
    {
        public CapturingCacheEntry? LastEntry { get; private set; }

        public bool TryGetValue(object key, out object? value)
        {
            value = null;
            return false;
        }

        public ICacheEntry CreateEntry(object key) => LastEntry = new CapturingCacheEntry(key);

        public void Remove(object key)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingCacheEntry(object key) : ICacheEntry
    {
        public object Key { get; } = key;

        public object? Value { get; set; }

        public DateTimeOffset? AbsoluteExpiration { get; set; }

        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        public TimeSpan? SlidingExpiration { get; set; }

        public IList<IChangeToken> ExpirationTokens { get; } = [];

        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = [];

        public CacheItemPriority Priority { get; set; }

        public long? Size { get; set; }

        public void Dispose()
        {
        }
    }

    // Always-miss IDistributedCache that captures the options of the last SetAsync, so a test can assert
    // exactly what expiration CacheService applied to L2.
    private sealed class CapturingDistributedCache : IDistributedCache
    {
        public DistributedCacheEntryOptions? LastOptions { get; private set; }

        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => LastOptions = options;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            LastOptions = options;
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}
