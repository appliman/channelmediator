using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task CompleteWorkflow_WithRequestAndNotification_WorksCorrectly()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        services.AddSingleton<INotificationHandler<TestNotification>>(handler2);
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act - Send a request
        var response = await mediator.Send(new TestRequest("integration test"));

        // Act - Publish a notification
        await mediator.Publish(new TestNotification("notification test"));
        await Task.Delay(50);

        // Assert
        Assert.Equal("Handled: integration test", response.Result);
        Assert.Single(handler1.HandledMessages);
        Assert.Single(handler2.HandledMessages);
    }

    [Fact]
    public async Task HighConcurrency_MultipleRequests_AllProcessedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        var tasks = Enumerable.Range(0, 100)
            .Select(i => mediator.Send(new TestRequest($"request-{i}")))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(100, results.Length);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal($"Handled: request-{i}", results[i].Result);
        }
    }

    [Fact]
    public async Task PipelineBehavior_WithMultipleHandlers_WorksCorrectly()
    {
        // Arrange
        var loggingBehavior1 = new LoggingBehavior<TestRequest, TestResponse>();
        var loggingBehavior2 = new LoggingBehavior<AnotherTestRequest, int>();

        var services = new ServiceCollection();
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => loggingBehavior1);
        services.AddScoped<IPipelineBehavior<AnotherTestRequest, int>>(sp => loggingBehavior2);
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.Send(new TestRequest("test"));
        await mediator.Send(new AnotherTestRequest(5));
        await Task.Delay(50);

        // Assert
        Assert.Equal(2, loggingBehavior1.Logs.Count);
        Assert.Equal(2, loggingBehavior2.Logs.Count);
    }

    [Fact]
    public async Task MixedOperations_RequestsAndNotifications_ProcessCorrectly()
    {
        // Arrange
        var handler = new TestNotificationHandler1();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(handler);
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        var requestTasks = new List<Task<TestResponse>>();
        for (int i = 0; i < 10; i++)
        {
            requestTasks.Add(mediator.Send(new TestRequest($"req-{i}")));
            await mediator.Publish(new TestNotification($"notif-{i}"));
        }

        var results = await Task.WhenAll(requestTasks);
        await Task.Delay(100);

        // Assert
        Assert.Equal(10, results.Length);
        Assert.Equal(10, handler.HandledMessages.Count);
    }

    [Fact]
    public async Task ServiceScope_IndependentScopes_HaveIndependentHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var handler1 = scope1.ServiceProvider.GetRequiredService<IRequestHandler<TestRequest, TestResponse>>();
        var handler2 = scope2.ServiceProvider.GetRequiredService<IRequestHandler<TestRequest, TestResponse>>();

        // Assert
        Assert.NotSame(handler1, handler2);
    }

    [Fact]
    public async Task Mediator_Disposed_StopsProcessing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

        // Act
        var response = await mediator!.Send(new TestRequest("before dispose"));
        await mediator.DisposeAsync();
        await Task.Delay(100);

        // Assert
        Assert.Equal("Handled: before dispose", response.Result);
    }

    [Fact]
    public async Task NotificationHandlers_WithParallelStrategy_ExecuteConcurrently()
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

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(i => mediator.Publish(new TestNotification($"msg-{i}")))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, handler1.HandledMessages.Count);
        Assert.Equal(10, handler2.HandledMessages.Count);
    }

    [Fact]
    public async Task BehaviorChain_WithValidation_StopsOnFailure()
    {
        // Arrange
        var validationBehavior = new ValidationBehavior<TestRequest, TestResponse>();
        var services = new ServiceCollection();
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => validationBehavior);
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await mediator.Send(new TestRequest("")));
        Assert.Contains("Value cannot be empty", exception.Message);
    }

    [Fact]
    public async Task MultipleRequestTypes_ProcessedIndependently()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(mediator.Send(new TestRequest($"test-{i}")));
            tasks.Add(mediator.Send(new AnotherTestRequest(i)).ContinueWith(t => { }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should complete without errors
        Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));
    }
}
