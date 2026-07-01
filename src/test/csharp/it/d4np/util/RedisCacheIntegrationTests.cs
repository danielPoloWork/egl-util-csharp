using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

using Testcontainers.Redis;

namespace It.D4np.Util.Tests;

/// <summary>
/// Integration tests for the distributed cache layer against a real Redis in a Testcontainers
/// container. When Docker is unavailable (e.g. the macOS/Windows CI runners, or a dev box without
/// Docker) the container fails to start and each test is skipped rather than failed.
/// </summary>
public sealed class RedisCacheIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _container;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            // Build() resolves and validates the Docker endpoint eagerly, so it is guarded here (not in
            // a field initializer) to skip — rather than fail the class constructor — when Docker is absent.
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
            _dockerAvailable = true;
        }
        catch (Exception)
        {
            // Docker is not reachable on this host; the Redis tests below skip.
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private IDistributedCache NewRedisCache() =>
        new RedisCache(Options.Create(new RedisCacheOptions { Configuration = _container!.GetConnectionString() }));

    private sealed record Sample(int Id, string Name);

    [SkippableFact]
    public async Task DistributedCacheExtensions_round_trip_against_Redis()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available; skipping the Testcontainers Redis test.");

        IDistributedCache cache = NewRedisCache();
        var value = new Sample(42, "answer");

        await cache.SetAsync("sample:42", value);
        Sample? read = await cache.GetAsync<Sample>("sample:42");

        Assert.Equal(value, read);
        Assert.Null(await cache.GetAsync<Sample>("sample:absent"));
    }

    [SkippableFact]
    public async Task CacheService_two_level_read_through_and_invalidation_against_Redis()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available; skipping the Testcontainers Redis test.");

        using var l1 = new MemoryCache(new MemoryCacheOptions());
        IDistributedCache redis = NewRedisCache();
        var cache = new CacheService(l1, redis);
        int calls = 0;

        Sample? first = await cache.GetOrCreateAsync("s:1", _ =>
        {
            calls++;
            return Task.FromResult(new Sample(1, "a"));
        });

        // Drop L1 so the second read must come from Redis (L2), proving write-through + promotion.
        l1.Remove("s:1");
        Sample? second = await cache.GetOrCreateAsync("s:1", _ =>
        {
            calls++;
            return Task.FromResult(new Sample(9, "z"));
        });

        Assert.Equal(new Sample(1, "a"), first);
        Assert.Equal(new Sample(1, "a"), second); // served from Redis, factory not re-invoked
        Assert.Equal(1, calls);

        await cache.RemoveAsync("s:1");
        Assert.Null(await redis.GetAsync<Sample>("s:1"));
    }
}
