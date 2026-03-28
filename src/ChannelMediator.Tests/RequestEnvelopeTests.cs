using ChannelMediator.Tests.Helpers;
using System.Collections.Generic;

namespace ChannelMediator.Tests;

public class RequestEnvelopeTests
{
    [Fact]
    public async Task DispatchAsync_WithValidHandler_CompletesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var handlers = new Dictionary<Type, IRequestHandlerWrapper>
        {
            { typeof(TestRequest), wrapper }
        };

        var request = new TestRequest("test");
        var completionSource = new TaskCompletionSource<TestResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = new RequestEnvelope<TestResponse>(request, completionSource, CancellationToken.None);

        // Act
        await envelope.DispatchAsync(handlers, CancellationToken.None);
        var result = await completionSource.Task;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Handled: test", result.Result);
        Assert.True(completionSource.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DispatchAsync_WithMissingHandler_SetsException()
    {
        // Arrange
        var handlers = new Dictionary<Type, IRequestHandlerWrapper>();
        var request = new TestRequest("test");
        var completionSource = new TaskCompletionSource<TestResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = new RequestEnvelope<TestResponse>(request, completionSource, CancellationToken.None);

        // Act
        await envelope.DispatchAsync(handlers, CancellationToken.None);

        // Assert
        Assert.True(completionSource.Task.IsFaulted);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await completionSource.Task);
        Assert.Contains("No handler registered", exception.Message);
    }

    [Fact]
    public async Task DispatchAsync_WithCancelledCallerToken_SetsCancelled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var handlers = new Dictionary<Type, IRequestHandlerWrapper>
        {
            { typeof(TestRequest), wrapper }
        };

        var request = new TestRequest("test");
        var completionSource = new TaskCompletionSource<TestResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var envelope = new RequestEnvelope<TestResponse>(request, completionSource, cts.Token);

        // Act
        await envelope.DispatchAsync(handlers, CancellationToken.None);

        // Assert
        Assert.True(completionSource.Task.IsCanceled);
    }

    [Fact]
    public async Task DispatchAsync_WithFailingHandler_SetsException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<FailingRequest, string>, FailingRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<FailingRequest, string>(serviceProvider);
        var handlers = new Dictionary<Type, IRequestHandlerWrapper>
        {
            { typeof(FailingRequest), wrapper }
        };

        var request = new FailingRequest();
        var completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = new RequestEnvelope<string>(request, completionSource, CancellationToken.None);

        // Act
        await envelope.DispatchAsync(handlers, CancellationToken.None);

        // Assert
        Assert.True(completionSource.Task.IsFaulted);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await completionSource.Task);
        Assert.Equal("Handler failed", exception.Message);
    }

    [Fact]
    public async Task DispatchAsync_WithDispatcherCancellation_SetsCancelled()
    {
        // Arrange
        var services = new ServiceCollection();
        var delayBehavior = new DelayBehavior<TestRequest, TestResponse>(5000);
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => delayBehavior);
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var handlers = new Dictionary<Type, IRequestHandlerWrapper>
        {
            { typeof(TestRequest), wrapper }
        };

        var request = new TestRequest("test");
        var completionSource = new TaskCompletionSource<TestResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = new RequestEnvelope<TestResponse>(request, completionSource, CancellationToken.None);

        var dispatcherCts = new CancellationTokenSource();
        dispatcherCts.CancelAfter(10);

        // Act
        await envelope.DispatchAsync(handlers, dispatcherCts.Token);

        // Assert
        Assert.True(completionSource.Task.IsCanceled);
    }

    [Fact]
    public async Task DispatchAsync_WithLinkedCancellation_HandlesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var handlers = new Dictionary<Type, IRequestHandlerWrapper>
        {
            { typeof(TestRequest), wrapper }
        };

        var request = new TestRequest("test");
        var completionSource = new TaskCompletionSource<TestResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callerCts = new CancellationTokenSource();
        var envelope = new RequestEnvelope<TestResponse>(request, completionSource, callerCts.Token);

        var dispatcherCts = new CancellationTokenSource();

        // Act
        var dispatchTask = envelope.DispatchAsync(handlers, dispatcherCts.Token);
        await Task.Delay(5); // Give it a moment to start
        callerCts.Cancel(); // Cancel from caller side
        await dispatchTask;

        // Assert - Should handle cancellation from either token
        Assert.True(
            completionSource.Task.Status is TaskStatus.Canceled or TaskStatus.RanToCompletion);
    }
}
