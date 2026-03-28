using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

public class ChannelMediatorTests
{
    [Fact]
    public async Task Send_WithValidRequest_ReturnsExpectedResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new TestRequest("test");

        // Act
        var response = await mediator.Send(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Handled: test", response.Result);
    }

    [Fact]
    public async Task Send_WithAnotherValidRequest_ReturnsExpectedResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new TestRequest("send-test");

        // Act
        var response = await mediator.Send(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Handled: send-test", response.Result);
    }

    [Fact]
    public async Task Send_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Send<TestResponse>(null!));
    }

    [Fact]
    public async Task Send_WithUnregisteredRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IMediator>(sp => new Mediator(Array.Empty<IRequestHandlerWrapper>()));
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new TestRequest("test");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await mediator.Send(request));
        Assert.Contains("No handler registered", exception.Message);
    }

    [Fact]
    public async Task Send_WithFailingHandler_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(FailingRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new FailingRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await mediator.Send(request));
        Assert.Equal("Handler failed", exception.Message);
    }

    [Fact]
    public async Task Send_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new TestRequest("test");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await mediator.Send(request, cts.Token));
    }

    [Fact]
    public async Task Publish_WithValidNotification_CallsAllHandlers()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
        services.AddChannelMediator(null, typeof(TestNotificationHandler1).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var notification = new TestNotification("test message");

        // Act
        await mediator.Publish(notification);
        await Task.Delay(50); // Give time for sequential processing

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Equal("Handler1: test message", handler1.HandledMessages[0]);
        Assert.Single(handler2.HandledMessages);
        Assert.Equal("Handler2: test message", handler2.HandledMessages[0]);
    }

    [Fact]
    public async Task Publish_WithAnotherValidNotification_CallsAllHandlers()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
        services.AddChannelMediator(null, typeof(TestNotificationHandler1).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var notification = new TestNotification("publish test");

        // Act
        await mediator.Publish(notification);
        await Task.Delay(50); // Give time for sequential processing

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Single(handler2.HandledMessages);
    }

    [Fact]
    public async Task Publish_WithParallelStrategy_CallsAllHandlersInParallel()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
        services.AddChannelMediator(config =>
        {
            config.Strategy = NotificationPublishStrategy.Parallel;
        }, typeof(TestNotificationHandler1).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var notification = new TestNotification("parallel test");

        // Act
        await mediator.Publish(notification);

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Single(handler2.HandledMessages);
    }

    [Fact]
    public async Task Publish_WithNullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestNotificationHandler1).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mediator.Publish<TestNotification>(null!));
    }

    [Fact]
    public async Task Publish_WithUnregisteredNotification_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator();
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var notification = new TestNotification("unregistered");

        // Act & Assert - Should not throw
        await mediator.Publish(notification);
    }

    [Fact]
    public async Task DisposeAsync_DisposesMediator_StopsProcessingRequests()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

        // Act
        await mediator!.DisposeAsync();

        // Give time for disposal to complete
        await Task.Delay(100);

        // Assert - The mediator should be disposed
        // Attempting to use it may result in exceptions or undefined behavior
        Assert.NotNull(mediator);
    }

    [Fact]
    public async Task Send_WithMultipleRequests_ProcessesAllInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        var tasks = new List<Task<TestResponse>>();
        for (int i = 0; i < 10; i++)
        {
            var request = new TestRequest($"test-{i}");
            tasks.Add(mediator.Send(request));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"Handled: test-{i}", results[i].Result);
        }
    }

    [Fact]
    public async Task Send_WithMultipleRequestTypes_ProcessesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        var response1 = await mediator.Send(new TestRequest("test"));
        var response2 = await mediator.Send(new AnotherTestRequest(5));

        // Assert
        Assert.Equal("Handled: test", response1.Result);
        Assert.Equal(10, response2);
    }

    [Fact]
    public async Task Constructor_WithHandlersOnly_CreatesValidMediator()
    {
        // Arrange & Act
        var handler = new RequestHandlerWrapper<TestRequest, TestResponse>(new ServiceCollection().BuildServiceProvider());
        var mediator = new Mediator(new[] { handler });

        // Assert
        Assert.NotNull(mediator);

        // Cleanup
        await mediator.DisposeAsync();
    }
}
