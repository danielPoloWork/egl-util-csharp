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
    private Exception? _startupFailure;

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
        catch (Exception exception)
        {
            // Container start failed. Usually Docker is simply absent (the tests below skip), but it could
            // also be a real regression (bad image tag, a Testcontainers break); capture it so
            // SkipOrFailIfNoRedis can fail loudly under REQUIRE_DOCKER instead of hiding it as a skip (#100).
            _startupFailure = exception;
            _dockerAvailable = false;
        }
    }

    // Skips when Docker is genuinely unavailable, but FAILS when REQUIRE_DOCKER=1 (set on the CI leg where
    // Docker is present) — so a container-start regression can no longer silently downgrade the only
    // real-Redis coverage to a skip while the pipeline stays green (#100).
    private void SkipOrFailIfNoRedis()
    {
        if (!_dockerAvailable && Environment.GetEnvironmentVariable("REQUIRE_DOCKER") == "1")
        {
            throw new InvalidOperationException(
                "REQUIRE_DOCKER=1 but the Testcontainers Redis container did not start, so the real-Redis " +
                "integration tests did not run — fix Docker/Testcontainers rather than letting coverage vanish.",
                _startupFailure);
        }

        Skip.IfNot(_dockerAvailable, "Docker is not available; skipping the Testcontainers Redis test.");
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
        SkipOrFailIfNoRedis();

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
        SkipOrFailIfNoRedis();

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

    [SkippableFact]
    public async Task CacheService_expires_a_key_after_its_ttl_against_Redis()
    {
        SkipOrFailIfNoRedis();

        using var l1 = new MemoryCache(new MemoryCacheOptions());
        IDistributedCache redis = NewRedisCache();
        var cache = new CacheService(l1, redis);
        var ttl = TimeSpan.FromSeconds(1);
        int calls = 0;

        Task<Sample> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new Sample(1, "a"));
        }

        await cache.GetOrCreateAsync("exp:1", Factory, ttl); // calls == 1; written to L1 and Redis with the ttl
        l1.Remove("exp:1"); // drop L1 so expiry is decided by Redis (L2)

        // Redis honours the ttl (PEXPIRE) deterministically; poll until the key is gone, with headroom.
        var expired = false;
        for (int attempt = 0; attempt < 100 && !expired; attempt++)
        {
            if (await redis.GetAsync<Sample>("exp:1") is null)
            {
                expired = true;
            }
            else
            {
                await Task.Delay(100);
            }
        }

        Assert.True(expired, "the Redis key should expire within its ttl window");

        // With L1 removed and L2 expired, the next read misses both levels and re-invokes the factory.
        Sample? afterExpiry = await cache.GetOrCreateAsync("exp:1", Factory, ttl);
        Assert.Equal(new Sample(1, "a"), afterExpiry);
        Assert.Equal(2, calls);
    }
}
