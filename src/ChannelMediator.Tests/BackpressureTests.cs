using System.Threading.Channels;

namespace ChannelMediator.Tests;

public class BackpressureTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task WhenQueueIsBounded_ThenNestedInjectedMediatorsStillCompleteWithoutSharedLimit()
    {
        await using var provider = CreateProvider(1);
        var mediator = provider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new NestedRequest(3)).WaitAsync(Timeout);
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task WhenQueuedRequestIsCanceled_ThenItCompletesBeforeHeadAndNeverRuns()
    {
        await using var provider = CreateProvider(1);
        var mediator = provider.GetRequiredService<IMediator>();
        var head = new ControlledRequest();
        var first = mediator.Send(head);
        await head.Started.Task.WaitAsync(Timeout);
        using var cancellation = new CancellationTokenSource();
        var next = new ControlledRequest();
        var queued = mediator.Send(next, cancellation.Token);
        try
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Timeout));
            Assert.False(first.IsCompleted);
        }
        finally
        {
            head.Release.TrySetResult();
        }
        await first.WaitAsync(Timeout);
        var barrier = new ControlledRequest();
        barrier.Release.SetResult();
        await mediator.Send(barrier).WaitAsync(Timeout);
        Assert.False(next.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task WhenRunningHandlerIgnoresCancellation_ThenSendWaitsForItsCompletion()
    {
        await using var provider = CreateProvider(1);
        var mediator = provider.GetRequiredService<IMediator>();
        using var cancellation = new CancellationTokenSource();
        var request = new ControlledRequest(IgnoreCancellation: true);
        var task = mediator.Send(request, cancellation.Token);
        await request.Started.Task.WaitAsync(Timeout);
        try
        {
            cancellation.Cancel();
            await request.CancellationObserved.Task.WaitAsync(Timeout);
            Assert.False(task.IsCompleted);
        }
        finally
        {
            request.Release.TrySetResult();
        }
        Assert.Equal(42, await task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task WhenBoundedQueueIsFull_ThenShutdownCancelsQueuedAndRejectsWaitingWriter()
    {
        await using var provider = CreateProvider(1);
        var mediator = (Mediator)provider.GetRequiredService<IMediator>();
        var head = new ControlledRequest();
        var active = mediator.Send(head);
        await head.Started.Task.WaitAsync(Timeout);
        var next = new ControlledRequest();
        var queued = mediator.Send(next);
        var overflow = new ControlledRequest();
        var waitingWriter = mediator.Send(overflow);
        await mediator.DisposeAsync().AsTask().WaitAsync(Timeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        await Assert.ThrowsAsync<ChannelClosedException>(() => waitingWriter);
        Assert.False(next.Started.Task.IsCompleted);
        Assert.False(overflow.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task WhenWaitingWriterIsCanceled_ThenItNeverExecutesAndQueueRemainsUsable()
    {
        await using var provider = CreateProvider(1);
        var mediator = provider.GetRequiredService<IMediator>();
        var head = new ControlledRequest();
        var active = mediator.Send(head);
        await head.Started.Task.WaitAsync(Timeout);
        var next = new ControlledRequest();
        next.Release.SetResult();
        var queued = mediator.Send(next);
        using var cancellation = new CancellationTokenSource();
        var overflow = new ControlledRequest();
        var waiting = mediator.Send(overflow, cancellation.Token);
        try
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Timeout));
        }
        finally
        {
            head.Release.TrySetResult();
        }
        await Task.WhenAll(active, queued).WaitAsync(Timeout);
        Assert.False(overflow.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task WhenDisposalIsConcurrent_ThenAllCallersWaitForRunningHandler()
    {
        await using var provider = CreateProvider(1);
        var mediator = (Mediator)provider.GetRequiredService<IMediator>();
        var head = new ControlledRequest(IgnoreCancellation: true);
        var active = mediator.Send(head);
        await head.Started.Task.WaitAsync(Timeout);
        var first = mediator.DisposeAsync().AsTask();
        var second = mediator.DisposeAsync().AsTask();
        try
        {
            await head.CancellationObserved.Task.WaitAsync(Timeout);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            head.Release.TrySetResult();
        }
        await Task.WhenAll(first, second, active).WaitAsync(Timeout);
    }

    [Fact]
    public async Task WhenDisposedImmediately_ThenEveryAcceptedRequestCompletes()
    {
        for (var iteration = 0; iteration < 20; iteration++)
        {
            await using var provider = CreateProvider(null);
            var mediator = (Mediator)provider.GetRequiredService<IMediator>();
            var requests = Enumerable.Range(0, 20).Select(_ => mediator.Send(new ControlledRequest())).ToArray();
            await mediator.DisposeAsync().AsTask().WaitAsync(Timeout);
            foreach (var request in requests)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(Timeout));
            }
        }
    }

    private static ServiceProvider CreateProvider(int? capacity)
    {
        var services = new ServiceCollection();
        services.AddChannelMediator(c => c.QueueCapacity = capacity, typeof(ControlledHandler).Assembly);
        var result = services.BuildServiceProvider();
        return result;
    }

    public sealed record ControlledRequest(bool IgnoreCancellation = false) : IRequest<int>
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed record NestedRequest(int Depth) : IRequest<int>;

    public sealed class NestedHandler(IMediator mediator) : IRequestHandler<NestedRequest, int>
    {
        public async Task<int> Handle(NestedRequest request, CancellationToken cancellationToken)
        {
            if (request.Depth == 0)
            {
                return 0;
            }
            var result = 1 + await mediator.Send(new NestedRequest(request.Depth - 1), cancellationToken);
            return result;
        }
    }

    public sealed class ControlledHandler : IRequestHandler<ControlledRequest, int>
    {
        public async Task<int> Handle(ControlledRequest request, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => request.CancellationObserved.TrySetResult());
            request.Started.TrySetResult();
            if (request.IgnoreCancellation)
            {
                await request.Release.Task;
            }
            else
            {
                await request.Release.Task.WaitAsync(cancellationToken);
            }
            return 42;
        }
    }
}
