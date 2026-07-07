using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace It.D4np.Util.Tests;

public class DistributedCacheExtensionsTests
{
    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private sealed record Widget(int Id, string Name);

    private sealed record Optional(int Id, string? Note);

    [Fact]
    public async Task SetAsync_then_GetAsync_round_trips_a_value()
    {
        var widget = new Widget(7, "cog");

        await _cache.SetAsync("w:7", widget);
        Widget? read = await _cache.GetAsync<Widget>("w:7");

        Assert.Equal(widget, read);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("present")]
    public async Task Round_trips_an_object_with_a_nullable_property(string? note)
    {
        // Guards the compact WhenWritingNull option: a null property is omitted on write and must
        // deserialise back to null (not corrupt the value); a non-null property round-trips intact.
        var value = new Optional(3, note);

        await _cache.SetAsync("opt:3", value);
        Optional? read = await _cache.GetAsync<Optional>("opt:3");

        Assert.Equal(value, read);
        Assert.Equal(note, read!.Note);
    }

    [Fact]
    public async Task SetAsync_then_GetAsync_round_trips_a_collection()
    {
        string[] tags = ["alpha", "beta", "gamma"];

        await _cache.SetAsync("tags:1", tags);
        string[]? read = await _cache.GetAsync<string[]>("tags:1");

        Assert.Equal(tags, read); // element-wise sequence comparison
    }

    [Fact]
    public async Task GetAsync_returns_default_for_an_absent_key()
    {
        Assert.Null(await _cache.GetAsync<Widget>("missing"));
    }

    [Fact]
    public async Task Round_trips_a_default_value_type_as_a_real_stored_value()
    {
        await _cache.SetAsync("n:0", 0);

        Assert.Equal(0, await _cache.GetAsync<int>("n:0"));
    }

    [Fact]
    public async Task GetAsync_rejects_a_null_cache()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((IDistributedCache)null!).GetAsync<Widget>("k"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetAsync_rejects_a_null_or_empty_key(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.GetAsync<Widget>(key!));
    }

    [Fact]
    public async Task SetAsync_rejects_a_null_cache()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ((IDistributedCache)null!).SetAsync("k", 1));
    }

    [Fact]
    public async Task SetAsync_rejects_a_null_value()
    {
        // A JSON null would be indistinguishable from an absent key and would poison CacheService's L1;
        // storing null is refused, matching the never-cache-null policy (#59).
        Widget? nothing = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.SetAsync("w:null", nothing));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SetAsync_rejects_a_null_or_empty_key(string? key)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _cache.SetAsync(key!, 1));
    }
}
