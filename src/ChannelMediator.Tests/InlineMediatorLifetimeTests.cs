using System.Threading.Channels;

namespace ChannelMediator.Tests;

public class InlineMediatorLifetimeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenInlineMediatorIsDisposed_ThenItCancelsAndWaitsForHandlerAndAsyncScope(bool ignoreCancellation)
    {
        await using var provider = CreateProvider();
        var root = provider.GetRequiredService<IMediator>();
        var child = provider.GetRequiredService<IMediator>();
        var request = new InlineWork(ignoreCancellation);
        var rootTask = root.Send(new SharedRequestLimitTests.Work(async token =>
        {
            var result = await child.Send(request, token);
            return result;
        }));
        await request.Started.Task.WaitAsync(Timeout);
        var disposeTask = ((IAsyncDisposable)child).DisposeAsync().AsTask();

        try
        {
            if (ignoreCancellation)
            {
                await request.CancellationObserved.Task.WaitAsync(Timeout);
                Assert.False(disposeTask.IsCompleted);
                Assert.False(rootTask.IsCompleted);
                request.Release.TrySetResult();
            }
            // For cooperative handlers, cancellation itself enters scope disposal.
            // Observing a separate token callback races with its unregistration by Handle.
            await request.DisposalStarted.Task.WaitAsync(Timeout);
            Assert.False(disposeTask.IsCompleted);
            Assert.False(rootTask.IsCompleted);
        }
        finally
        {
            request.Release.TrySetResult();
            request.DisposalRelease.TrySetResult();
        }

        if (ignoreCancellation)
        {
            Assert.Equal(42, await rootTask.WaitAsync(Timeout));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rootTask.WaitAsync(Timeout));
        }

        await disposeTask.WaitAsync(Timeout);
        Assert.True(request.Disposed);
    }

    [Fact]
    public async Task WhenDisposedMediatorIsUsedByNestedSend_ThenItDoesNotStartAnotherHandler()
    {
        await using var provider = CreateProvider();
        var root = provider.GetRequiredService<IMediator>();
        var child = provider.GetRequiredService<IMediator>();
        await ((IAsyncDisposable)child).DisposeAsync();
        var request = new InlineWork(false);

        try
        {
            await Assert.ThrowsAsync<ChannelClosedException>(() => root.Send(new SharedRequestLimitTests.Work(async token =>
            {
                var result = await child.Send(request, token);
                return result;
            })).WaitAsync(Timeout));
        }
        finally
        {
            request.Release.TrySetResult();
            request.DisposalRelease.TrySetResult();
        }

        Assert.False(request.Started.Task.IsCompleted);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddRequestHandler<SharedRequestLimitTests.Work, int, SharedRequestLimitTests.WorkHandler>();
        services.AddRequestHandler<InlineWork, int, InlineWorkHandler>();
        services.AddChannelMediator(configuration =>
        {
            configuration.QueueCapacity = 1;
            configuration.MaxConcurrentRequestChains = 1;
        }, typeof(IMediator).Assembly);
        var result = services.BuildServiceProvider();
        return result;
    }

    public sealed record InlineWork(bool IgnoreCancellation) : IRequest<int>
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposalRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; set; }
    }

    public sealed class InlineWorkHandler : IRequestHandler<InlineWork, int>, IAsyncDisposable
    {
        private InlineWork? _request;

        public async Task<int> Handle(InlineWork request, CancellationToken cancellationToken)
        {
            _request = request;
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

        public async ValueTask DisposeAsync()
        {
            if (_request is not null)
            {
                _request.DisposalStarted.TrySetResult();
                await _request.DisposalRelease.Task;
                _request.Disposed = true;
            }
        }
    }
}
