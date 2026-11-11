using System.Runtime.CompilerServices;

namespace ChannelMediator.Tests;

public class WrapperLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenRequestCompletesOrFails_ThenAsyncDependencyIsDisposed(bool fail)
    {
        var state = new LifecycleState();
        var services = CreateServices(state);
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        if (fail)
        {
            await Assert.ThrowsAsync<ApplicationException>(() => mediator.Send(new LifecycleRequest(true)));
        }
        else
        {
            Assert.Equal(42, await mediator.Send(new LifecycleRequest(false)));
        }

        Assert.Equal(1, state.Disposals);
    }

    [Theory]
    [InlineData(NotificationPublishStrategy.Sequential)]
    [InlineData(NotificationPublishStrategy.Parallel)]
    public async Task WhenNotificationCompletes_ThenAsyncDependencyIsDisposed(NotificationPublishStrategy strategy)
    {
        var state = new LifecycleState();
        var services = CreateServices(state, strategy);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediator>().Publish(new LifecycleNotification());

        Assert.Equal(1, state.Notifications);
        Assert.Equal(1, state.Disposals);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task WhenStreamCompletesFailsOrStopsEarly_ThenAsyncDependencyIsDisposed(bool fail, bool stopEarly)
    {
        var state = new LifecycleState();
        var services = CreateServices(state);
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        async Task EnumerateAsync()
        {
            await foreach (var item in mediator.CreateStream(new LifecycleStream(fail)))
            {
                Assert.Equal(42, item);
                Assert.Equal(0, state.Disposals);
                if (stopEarly)
                {
                    break;
                }
            }
        }

        if (fail)
        {
            await Assert.ThrowsAsync<ApplicationException>(EnumerateAsync);
        }
        else
        {
            await EnumerateAsync();
        }

        Assert.Equal(1, state.Disposals);
    }

    [Fact]
    public async Task WhenRegistrationIsRepeated_ThenEachNotificationHandlerRunsOnce()
    {
        var state = new LifecycleState();
        var services = CreateServices(state);
        services.AddChannelMediator(null, typeof(WrapperLifecycleTests).Assembly, typeof(WrapperLifecycleTests).Assembly);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediator>().Publish(new LifecycleNotification());

        Assert.Equal(1, state.Notifications);
        Assert.Equal(1, state.Disposals);
    }

    [Fact]
    public async Task WhenCommandIsScanned_ThenBothInterfacesResolveTheSameHandler()
    {
        var services = CreateServices(new LifecycleState());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var commandHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<LifecycleCommand>>();
        var genericHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<LifecycleCommand, Unit>>();

        Assert.Same(commandHandler, genericHandler);
        await provider.GetRequiredService<IMediator>().Send(new LifecycleCommand());
    }

    [Fact]
    public async Task WhenPipelineHasMultipleBehaviors_ThenRegistrationOrderAndUnwindingArePreserved()
    {
        var state = new LifecycleState();
        var services = CreateServices(state);
        services.AddScoped<IPipelineBehavior<LifecycleRequest, int>>(_ => new RecordingBehavior(state, "outer"));
        services.AddScoped<IPipelineBehavior<LifecycleRequest, int>>(_ => new RecordingBehavior(state, "inner"));
        await using var provider = services.BuildServiceProvider();

        Assert.Equal(42, await provider.GetRequiredService<IMediator>().Send(new LifecycleRequest(false)));

        Assert.Equal(new[] { "outer-before", "inner-before", "handler", "inner-after", "outer-after" }, state.Events);
        Assert.Equal(1, state.Disposals);
    }

    [Fact]
    public async Task WhenBehaviorShortCircuits_ThenHandlerIsNotConstructed()
    {
        var state = new LifecycleState();
        var services = CreateServices(state);
        services.AddScoped<IPipelineBehavior<LifecycleRequest, int>>(_ => new RecordingBehavior(state, "cache", true));
        await using var provider = services.BuildServiceProvider();

        Assert.Equal(17, await provider.GetRequiredService<IMediator>().Send(new LifecycleRequest(false)));

        Assert.Equal(0, state.Dependencies);
        Assert.Equal(0, state.Disposals);
        Assert.Equal(new[] { "cache-before" }, state.Events);
    }

    [Fact]
    public async Task WhenParallelHandlerThrowsSynchronously_ThenOtherHandlersFinishBeforeScopeDisposal()
    {
        var state = new ParallelLifecycleState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<ParallelDependency>();
        services.AddChannelMediator(c => c.Strategy = NotificationPublishStrategy.Parallel, typeof(IMediator).Assembly);
        services.AddSingleton<INotificationHandlerWrapper>(new NotificationHandlerWrapper<ParallelNotice>());
        services.AddScoped<INotificationHandler<ParallelNotice>, WaitingNotificationHandler>();
        services.AddScoped<INotificationHandler<ParallelNotice>, ThrowingNotificationHandler>();
        await using var provider = services.BuildServiceProvider();
        var publish = provider.GetRequiredService<IMediator>().Publish(new ParallelNotice());

        try
        {
            await state.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(state.ThrowingHandlerCalled);
            Assert.False(publish.IsCompleted);
            Assert.False(state.DisposalStarted);
        }
        finally
        {
            state.Release.TrySetResult();
        }

        await Assert.ThrowsAsync<ApplicationException>(() => publish.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(state.WaitingHandlerFinished);
        Assert.True(state.DisposalCompleted);
    }
    private static ServiceCollection CreateServices(LifecycleState state, NotificationPublishStrategy strategy = NotificationPublishStrategy.Sequential)
    {
        var result = new ServiceCollection();
        result.AddSingleton(state);
        result.AddScoped<AsyncDependency>();
        result.AddChannelMediator(c => c.Strategy = strategy, typeof(WrapperLifecycleTests).Assembly);
        return result;
    }

    public sealed class LifecycleState
    {
        public int Dependencies;
        public int Disposals;
        public int Notifications;
        public List<string> Events { get; } = [];
    }

    public sealed class AsyncDependency : IAsyncDisposable
    {
        private readonly LifecycleState _state;

        public AsyncDependency(LifecycleState state)
        {
            _state = state;
            Interlocked.Increment(ref state.Dependencies);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            Interlocked.Increment(ref _state.Disposals);
        }
    }

    public sealed record LifecycleRequest(bool Fail) : IRequest<int>;

    public sealed class LifecycleHandler(AsyncDependency dependency, LifecycleState state) : IRequestHandler<LifecycleRequest, int>
    {
        public Task<int> Handle(LifecycleRequest request, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            state.Events.Add("handler");
            if (request.Fail)
            {
                throw new ApplicationException("Expected handler failure.");
            }

            var result = Task.FromResult(42);
            return result;
        }
    }

    public sealed record LifecycleNotification : INotification;

    public sealed class LifecycleNotificationHandler(AsyncDependency dependency, LifecycleState state) : INotificationHandler<LifecycleNotification>
    {
        public Task Handle(LifecycleNotification notification, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            Interlocked.Increment(ref state.Notifications);
            return Task.CompletedTask;
        }
    }

    public sealed record LifecycleStream(bool Fail) : IStreamRequest<int>;

    public sealed class LifecycleStreamHandler(AsyncDependency dependency) : IStreamRequestHandler<LifecycleStream, int>
    {
        public async IAsyncEnumerable<int> Handle(LifecycleStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            yield return 42;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Fail)
            {
                throw new ApplicationException("Expected stream failure.");
            }
        }
    }

    public sealed record LifecycleCommand : IRequest;

    public sealed class LifecycleCommandHandler : IRequestHandler<LifecycleCommand>
    {
        public Task Handle(LifecycleCommand request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class ParallelLifecycleState
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ThrowingHandlerCalled;
        public bool WaitingHandlerFinished;
        public bool DisposalStarted;
        public bool DisposalCompleted;
    }

    public sealed class ParallelDependency(ParallelLifecycleState state) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            state.DisposalStarted = true;
            await Task.Yield();
            state.DisposalCompleted = true;
        }
    }

    public sealed record ParallelNotice : INotification;

    public sealed class WaitingNotificationHandler(ParallelDependency dependency, ParallelLifecycleState state) : INotificationHandler<ParallelNotice>
    {
        public async Task Handle(ParallelNotice notification, CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            state.Started.TrySetResult();
            await state.Release.Task.WaitAsync(cancellationToken);
            Assert.False(state.DisposalStarted);
            state.WaitingHandlerFinished = true;
        }
    }

    public sealed class ThrowingNotificationHandler : INotificationHandler<ParallelNotice>
    {
        private readonly ParallelLifecycleState _state;

        public ThrowingNotificationHandler(ParallelLifecycleState state)
        {
            _state = state;
        }

        public Task Handle(ParallelNotice notification, CancellationToken cancellationToken)
        {
            _state.ThrowingHandlerCalled = true;
            throw new ApplicationException("Expected synchronous notification failure.");
        }
    }
    private sealed class RecordingBehavior(LifecycleState state, string name, bool shortCircuit = false) : IPipelineBehavior<LifecycleRequest, int>
    {
        public async ValueTask<int> HandleAsync(LifecycleRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
        {
            state.Events.Add(name + "-before");
            if (shortCircuit)
            {
                return 17;
            }

            var result = await next();
            state.Events.Add(name + "-after");
            return result;
        }
    }
}
