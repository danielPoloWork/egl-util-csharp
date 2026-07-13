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

    private sealed class Annotated
    {
        public int Id { get; set; }

        public string? Note { get; set; } = "n/a"; // non-null C# default
    }

    private sealed record AnnotatedRecord(int Id, string? Note = "n/a"); // non-null ctor-param default

    private interface IShape { }

    private abstract record ShapeBase(int Sides);

    private sealed record Circle(int Sides, double Radius) : ShapeBase(Sides), IShape;

    private record Animal(string Name); // concrete, non-sealed base

    private sealed record Dog(string Name, string Breed) : Animal(Name);

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
        // A nullable property round-trips faithfully: null stays null, a non-null value is preserved.
        // (#58 dropped the WhenWritingNull option that used to omit — and so corrupt — null properties.)
        var value = new Optional(3, note);

        await _cache.SetAsync("opt:3", value);
        Optional? read = await _cache.GetAsync<Optional>("opt:3");

        Assert.Equal(value, read);
        Assert.Equal(note, read!.Note);
    }

    [Fact]
    public async Task An_explicitly_null_property_survives_the_round_trip_despite_a_non_null_default()
    {
        // Regression for #58: a property set to null must round-trip back as null. Previously
        // WhenWritingNull omitted it on write, so deserialisation left it at the C# default ("n/a") —
        // silent corruption of a value cached and then read back through L2. Covers both non-null-default
        // shapes the issue names: a class property initializer and a record constructor-parameter default
        // (distinct System.Text.Json deserialization paths).
        var value = new Annotated { Id = 3, Note = null };
        await _cache.SetAsync("ann:3", value);
        Annotated? read = await _cache.GetAsync<Annotated>("ann:3");

        Assert.NotNull(read);
        Assert.Equal(3, read!.Id);
        Assert.Null(read.Note); // pre-fix: "n/a"

        await _cache.SetAsync("annrec:4", new AnnotatedRecord(4, Note: null));
        AnnotatedRecord? readRecord = await _cache.GetAsync<AnnotatedRecord>("annrec:4");

        Assert.NotNull(readRecord);
        Assert.Null(readRecord!.Note); // pre-fix: "n/a"
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

    [Fact]
    public async Task GetAsync_rejects_a_polymorphic_declared_type_even_for_an_absent_key()
    {
        // #33: T is part of the cache contract; an object/interface/abstract declared type cannot
        // round-trip and is rejected up front — even when the key is absent.
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetAsync<object>("absent"));
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetAsync<IShape>("absent"));
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.GetAsync<ShapeBase>("absent"));
    }

    [Fact]
    public async Task SetAsync_rejects_a_polymorphic_declared_type()
    {
        // #33: serialising an object/interface/abstract T would store only the declared type's members.
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.SetAsync<object>("poly:obj", new Circle(1, 2.0)));
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.SetAsync<IShape>("poly:iface", new Circle(1, 2.0)));
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.SetAsync<ShapeBase>("poly:abstract", new Circle(1, 2.0)));
    }

    [Fact]
    public async Task SetAsync_rejects_a_runtime_subtype_of_the_declared_type()
    {
        // #33: a Dog stored under SetAsync<Animal> would serialise only Animal's members, losing Breed.
        await Assert.ThrowsAsync<NotSupportedException>(() => _cache.SetAsync<Animal>("poly:sub", new Dog("Rex", "Lab")));
    }
}
