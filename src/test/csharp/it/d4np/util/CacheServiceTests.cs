using System.Text;
using System.Text.Json;

using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

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
    [InlineData("")]
    [InlineData(null)]
    public async Task RemoveAsync_rejects_a_null_or_empty_key(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.RemoveAsync(key!).AsTask());
    }
}
