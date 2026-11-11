namespace ChannelMediator.Tests;

public class SharedRequestLimitTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhenSharedLimitIsNotPositive_ThenFactoryResolutionRejectsConfiguration(int limit)
    {
        using var provider = CreateProvider(limit);

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetRequiredService<IMediatorFactory>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhenQueueCapacityIsNotPositive_ThenFactoryResolutionRejectsConfiguration(int capacity)
    {
        using var provider = CreateProvider(1, capacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetRequiredService<IMediatorFactory>());
    }

    [Fact]
    public async Task WhenSharedLimitIsReached_ThenAnotherMediatorWaitsForAChainToComplete()
    {
        await using var provider = CreateProvider(2);
        var first = new BlockingWork();
        var second = new BlockingWork();
        var third = new BlockingWork();
        var firstTask = provider.GetRequiredService<IMediator>().Send(first.Request);
        var secondTask = provider.GetRequiredService<IMediator>().Send(second.Request);
        await Task.WhenAll(first.Started.Task, second.Started.Task).WaitAsync(Timeout);
        var thirdTask = provider.GetRequiredService<IMediator>().Send(third.Request);

        try
        {
            await AssertNotStartedAsync(third.Started.Task);
            first.Release.TrySetResult();
            await third.Started.Task.WaitAsync(Timeout);
        }
        finally
        {
            first.Release.TrySetResult();
            second.Release.TrySetResult();
            third.Release.TrySetResult();
            await Task.WhenAll(firstTask, secondTask, thirdTask).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task WhenSharedLimitIsOne_ThenNestedSendOnSameMediatorCompletes()
    {
        await using var provider = CreateProvider(1, 1);
        var mediator = provider.GetRequiredService<IMediator>();
        using var cancellation = new CancellationTokenSource(Timeout);
        var request = new Work(async token =>
        {
            var result = await mediator.Send(new Work(_ => Task.FromResult(42)), token);
            return result;
        });

        var result = await mediator.Send(request, cancellation.Token).WaitAsync(Timeout);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task WhenSharedLimitIsOne_ThenNestedSendOnAnotherMediatorCompletes()
    {
        await using var provider = CreateProvider(1, 1);
        var mediator = provider.GetRequiredService<IMediator>();
        using var cancellation = new CancellationTokenSource(Timeout);
        var request = new Work(async token =>
        {
            var nested = provider.GetRequiredService<IMediator>();
            var result = await nested.Send(new Work(_ => Task.FromResult(42)), token);
            return result;
        });

        var result = await mediator.Send(request, cancellation.Token).WaitAsync(Timeout);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task WhenMediatorIsCreatedInsideAChain_ThenExternalSendStillAcquiresItsOwnPermit()
    {
        await using var provider = CreateProvider(1);
        var created = new TaskCompletionSource<IMediator>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = NewSignal();
        var outerTask = provider.GetRequiredService<IMediator>().Send(new Work(async token =>
        {
            created.SetResult(provider.GetRequiredService<IMediator>());
            await release.Task.WaitAsync(token);
            return 1;
        }));
        var nested = await created.Task.WaitAsync(Timeout);
        var external = new BlockingWork();
        var externalTask = nested.Send(external.Request);

        try
        {
            await AssertNotStartedAsync(external.Started.Task);
            release.TrySetResult();
            await external.Started.Task.WaitAsync(Timeout);
        }
        finally
        {
            release.TrySetResult();
            external.Release.TrySetResult();
            await Task.WhenAll(outerTask, externalTask).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task WhenExecutionContextOutlivesItsChain_ThenItCannotReuseTheReleasedPermit()
    {
        await using var provider = CreateProvider(1);
        var delayedMediator = provider.GetRequiredService<IMediator>();
        var trigger = NewSignal();
        var delayed = new BlockingWork();
        Task<int>? delayedTask = null;
        await provider.GetRequiredService<IMediator>().Send(new Work(_ =>
        {
            // Deliberately retain a child task to exercise a captured, expired lease.
            delayedTask = Task.Run(async () =>
            {
                await trigger.Task;
                var result = await delayedMediator.Send(delayed.Request);
                return result;
            });
            return Task.FromResult(1);
        })).WaitAsync(Timeout);

        var blocker = new BlockingWork();
        var blockingTask = provider.GetRequiredService<IMediator>().Send(blocker.Request);
        await blocker.Started.Task.WaitAsync(Timeout);
        trigger.TrySetResult();
        try
        {
            await AssertNotStartedAsync(delayed.Started.Task);
            blocker.Release.TrySetResult();
            await delayed.Started.Task.WaitAsync(Timeout);
        }
        finally
        {
            blocker.Release.TrySetResult();
            delayed.Release.TrySetResult();
            await Task.WhenAll(blockingTask, delayedTask!).WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task WhenFactoriesAreIndependent_ThenTheirLimitsAreIndependent()
    {
        await using var firstProvider = CreateProvider(1);
        await using var secondProvider = CreateProvider(1);
        var blocker = new BlockingWork();
        var blockingTask = firstProvider.GetRequiredService<IMediator>().Send(blocker.Request);
        await blocker.Started.Task.WaitAsync(Timeout);

        try
        {
            var result = await secondProvider.GetRequiredService<IMediator>()
                .Send(new Work(_ => Task.FromResult(42))).WaitAsync(Timeout);
            Assert.Equal(42, result);
        }
        finally
        {
            blocker.Release.TrySetResult();
            await blockingTask.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task WhenWaitingForSharedPermitIsCanceled_ThenHandlerIsNotInvoked()
    {
        await using var provider = CreateProvider(1);
        var blocker = new BlockingWork();
        var blockingTask = provider.GetRequiredService<IMediator>().Send(blocker.Request);
        await blocker.Started.Task.WaitAsync(Timeout);
        using var cancellation = new CancellationTokenSource();
        var invocationCount = 0;
        var canceledTask = provider.GetRequiredService<IMediator>().Send(new Work(_ =>
        {
            Interlocked.Increment(ref invocationCount);
            return Task.FromResult(1);
        }), cancellation.Token);

        try
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledTask.WaitAsync(Timeout));
        }
        finally
        {
            blocker.Release.TrySetResult();
            await blockingTask.WaitAsync(Timeout);
        }

        Assert.Equal(0, invocationCount);
    }

    [Fact]
    public async Task WhenHandlerThrows_ThenItsPermitIsReleased()
    {
        await using var provider = CreateProvider(1);
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(
            new Work(_ => throw new InvalidOperationException("Expected failure"))).WaitAsync(Timeout));
        var result = await provider.GetRequiredService<IMediator>()
            .Send(new Work(_ => Task.FromResult(42))).WaitAsync(Timeout);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task WhenNestedSendUsesAnotherFactory_ThenItRespectsThatFactorysLimit()
    {
        await using var firstProvider = CreateProvider(1);
        await using var secondProvider = CreateProvider(1);
        var blocker = new BlockingWork();
        var blockingTask = secondProvider.GetRequiredService<IMediator>().Send(blocker.Request);
        await blocker.Started.Task.WaitAsync(Timeout);
        var nestedMediator = secondProvider.GetRequiredService<IMediator>();
        var nested = new BlockingWork();
        var outerTask = firstProvider.GetRequiredService<IMediator>().Send(new Work(async token =>
        {
            var result = await nestedMediator.Send(nested.Request, token);
            return result;
        }));

        try
        {
            await AssertNotStartedAsync(nested.Started.Task);
            blocker.Release.TrySetResult();
            await nested.Started.Task.WaitAsync(Timeout);
        }
        finally
        {
            blocker.Release.TrySetResult();
            nested.Release.TrySetResult();
            await Task.WhenAll(blockingTask, outerTask).WaitAsync(Timeout);
        }
    }

    private static ServiceProvider CreateProvider(int? limit, int? capacity = null)
    {
        var services = new ServiceCollection();
        services.AddRequestHandler<Work, int, WorkHandler>();
        services.AddChannelMediator(configuration =>
        {
            configuration.MaxConcurrentRequestChains = limit;
            configuration.QueueCapacity = capacity;
        }, typeof(IMediator).Assembly);
        var result = services.BuildServiceProvider();
        return result;
    }

    private static async Task AssertNotStartedAsync(Task started)
    {
        // Only a negative liveness guard; successful progress uses explicit signals.
        await Assert.ThrowsAsync<TimeoutException>(() => started.WaitAsync(TimeSpan.FromMilliseconds(100)));
    }

    private static TaskCompletionSource NewSignal()
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return result;
    }

    public sealed record Work(Func<CancellationToken, Task<int>> Execute) : IRequest<int>;

    public sealed class WorkHandler : IRequestHandler<Work, int>
    {
        public async Task<int> Handle(Work request, CancellationToken cancellationToken)
        {
            var result = await request.Execute(cancellationToken);
            return result;
        }
    }

    private sealed class BlockingWork
    {
        public TaskCompletionSource Started { get; } = NewSignal();
        public TaskCompletionSource Release { get; } = NewSignal();
        public Work Request => new(async token =>
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(token);
            return 1;
        });
    }
}
