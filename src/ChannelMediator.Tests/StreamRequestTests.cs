using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

public class StreamRequestTests
{
    // ── Dispatch ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenStreamRequestSent_ThenYieldsAllItems()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var results = new List<int>();
        await foreach (var item in mediator.CreateStream(new NumberStreamRequest(5)))
        {
            results.Add(item);
        }

        Assert.Equal([1, 2, 3, 4, 5], results);
    }

    [Fact]
    public async Task WhenHandlerYieldsNothing_ThenEmptySequence()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(EmptyStreamHandler).Assembly);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var results = new List<string>();
        await foreach (var item in mediator.CreateStream(new EmptyStreamRequest()))
        {
            results.Add(item);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task WhenNoHandlerRegistered_ThenThrowsInvalidOperationException()
    {
        var handlers = new List<IRequestHandlerWrapper>();
        var mediator = new Mediator(handlers);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream(new NumberStreamRequest(1)))
            {
            }
        });
    }

    [Fact]
    public async Task WhenNullRequest_ThenThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream<int>(null!))
            {
            }
        });
    }

    // ── DI Registration ───────────────────────────────────────────────────────

    [Fact]
    public async Task WhenManualRegistration_ThenDispatchesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator();
        services.AddStreamRequestHandler<NumberStreamRequest, int, NumberStreamHandler>();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var results = new List<int>();
        await foreach (var item in mediator.CreateStream(new NumberStreamRequest(3)))
        {
            results.Add(item);
        }

        Assert.Equal([1, 2, 3], results);
    }

    [Fact]
    public void WhenAssemblyScan_ThenStreamHandlersAreDiscovered()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        var sp = services.BuildServiceProvider();

        var wrappers = sp.GetServices<IStreamRequestHandlerWrapper>().ToList();

        Assert.Contains(wrappers, w => w.RequestType == typeof(NumberStreamRequest));
        Assert.Contains(wrappers, w => w.RequestType == typeof(EmptyStreamRequest));
    }

    // ── Pipeline behaviors ───────────────────────────────────────────────────

    [Fact]
    public async Task WhenStreamBehaviorRegistered_ThenWrapsEnumeration()
    {
        var behavior = new StreamLoggingBehavior<NumberStreamRequest, int>();

        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        services.AddScoped<IStreamPipelineBehavior<NumberStreamRequest, int>>(_ => behavior);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var results = new List<int>();
        await foreach (var item in mediator.CreateStream(new NumberStreamRequest(2)))
        {
            results.Add(item);
        }

        Assert.Equal([1, 2], results);
        Assert.Equal(["Before: NumberStreamRequest", "After: NumberStreamRequest"], behavior.Logs);
    }

    [Fact]
    public async Task WhenMultipleBehaviors_ThenAppliedOuterToInner()
    {
        var outer = new StreamDoubleWrapBehavior();
        var inner = new StreamLoggingBehavior<NumberStreamRequest, int>();

        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        // Registration order: outer first, inner second → outer wraps inner
        services.AddScoped<IStreamPipelineBehavior<NumberStreamRequest, int>>(_ => outer);
        services.AddScoped<IStreamPipelineBehavior<NumberStreamRequest, int>>(_ => inner);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await foreach (var _ in mediator.CreateStream(new NumberStreamRequest(1)))
        {
        }

        Assert.Equal(["outer-before", "outer-after"], outer.Order);
        Assert.Equal(["Before: NumberStreamRequest", "After: NumberStreamRequest"], inner.Logs);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenCancelledMidStream_ThenEnumerationStops()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        var results = new List<int>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in mediator.CreateStream(new NumberStreamRequest(100), cts.Token))
            {
                results.Add(item);
                if (results.Count == 3)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task WhenAlreadyCancelled_ThenThrowsImmediately()
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(NumberStreamHandler).Assembly);
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream(new NumberStreamRequest(10), cts.Token))
            {
            }
        });
    }
}
