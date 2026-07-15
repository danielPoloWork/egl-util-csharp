using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace It.D4np.Util.Tests;

// Opt-in per-key coalescing (#50, ADR-0039): with coalescing ON, concurrent misses for the same key run the
// factory at most once per key per node; with it OFF (the default) each concurrent miss runs the factory.
// The two tests are a built-in contrast — same gated-factory setup, differing only by the flag.
public class CacheServiceCoalescingTests
{
    private static CacheService NewCache(bool coalesce) =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            coalesceConcurrentMisses: coalesce);

    [Fact]
    public async Task Coalescing_enabled_runs_the_factory_once_under_concurrent_misses()
    {
        const int callers = 8;
        var factoryCalls = 0;
        var factoryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Factory(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref factoryCalls);
            factoryEntered.TrySetResult(); // the winning caller is inside the factory, holding the per-key gate
            await release.Task; // hold it there while the other callers pile up on the gate
            return 42;
        }

        CacheService cache = NewCache(coalesce: true);

        // Start all callers for the same key; each misses L1 and (all but the winner) blocks on the gate.
        Task<int>[] callersInFlight = Enumerable.Range(0, callers)
            .Select(_ => cache.GetOrCreateAsync<int>("stampede", Factory).AsTask())
            .ToArray();

        await factoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(30)); // one caller reached the factory
        release.SetResult(); // let it finish; the blocked callers re-check the cache after the gate
        int[] results = await Task.WhenAll(callersInFlight).WaitAsync(TimeSpan.FromSeconds(30));

        // Exactly one factory run despite N concurrent misses — the blocked callers returned the winner's value.
        Assert.Equal(1, Volatile.Read(ref factoryCalls));
        Assert.All(results, value => Assert.Equal(42, value));
    }

    [Fact]
    public async Task Coalescing_disabled_runs_the_factory_for_each_concurrent_miss()
    {
        // The default (lock-free) behaviour and the contrast that shows the opt-in gate above is doing
        // something: with no gate, every concurrent miss runs the factory (last write wins) — the documented
        // trade-off (ADR-0009).
        const int callers = 8;
        var factoryCalls = 0;
        var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<int> Factory(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref factoryCalls) == callers)
            {
                allEntered.TrySetResult(); // all N callers are inside the factory at once — no coalescing
            }

            await release.Task;
            return 42;
        }

        CacheService cache = NewCache(coalesce: false);

        Task<int>[] callersInFlight = Enumerable.Range(0, callers)
            .Select(_ => cache.GetOrCreateAsync<int>("stampede", Factory).AsTask())
            .ToArray();

        await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(30)); // completes only if all N ran the factory
        release.SetResult();
        await Task.WhenAll(callersInFlight).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(callers, Volatile.Read(ref factoryCalls)); // one factory run per concurrent miss
    }

    [Fact]
    public async Task Coalescing_enabled_still_serves_and_caches_the_value()
    {
        // Sanity: with coalescing on, a plain miss still runs the factory once, returns the value, and caches
        // it — a second call is served from the cache without re-running the factory.
        var factoryCalls = 0;
        CacheService cache = NewCache(coalesce: true);

        int first = await cache.GetOrCreateAsync<int>("k", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult(7);
        });
        int second = await cache.GetOrCreateAsync<int>("k", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult(7);
        });

        Assert.Equal(7, first);
        Assert.Equal(7, second);
        Assert.Equal(1, Volatile.Read(ref factoryCalls)); // second call hit the cache
    }
}
