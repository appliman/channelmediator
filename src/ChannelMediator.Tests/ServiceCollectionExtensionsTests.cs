using ChannelMediator.Tests.Helpers;
using System.Reflection;

namespace ChannelMediator.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddChannelMediator_RegistersMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var mediator = serviceProvider.GetService<IMediator>();
        Assert.NotNull(mediator);
        Assert.IsType<Mediator>(mediator);
    }

    [Fact]
    public void AddChannelMediator_WithoutAssemblies_UsesCallingAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChannelMediator();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var mediator = serviceProvider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void AddChannelMediator_WithNotificationConfiguration_ConfiguresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        NotificationPublishStrategy? configuredStrategy = null;

        // Act
        services.AddChannelMediator(config =>
        {
            config.Strategy = NotificationPublishStrategy.Parallel;
            configuredStrategy = config.Strategy;
        }, typeof(TestRequestHandler).Assembly);

        // Assert
        Assert.Equal(NotificationPublishStrategy.Parallel, configuredStrategy);
    }

    [Fact]
    public void AddChannelMediator_RegistersRequestHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handler = serviceProvider.GetService<IRequestHandler<TestRequest, TestResponse>>();
        Assert.NotNull(handler);
        Assert.IsType<TestRequestHandler>(handler);
    }

    [Fact]
    public void AddChannelMediator_RegistersNotificationHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChannelMediator(null, typeof(TestNotificationHandler1).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handlers = serviceProvider.GetServices<INotificationHandler<TestNotification>>();
        Assert.NotNull(handlers);
        Assert.True(handlers.Count() > 1);
    }

    [Fact]
    public void AddChannelMediator_RegistersWrappers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var wrappers = serviceProvider.GetServices<IRequestHandlerWrapper>();
        Assert.NotEmpty(wrappers);
    }

    [Fact]
    public void AddRequestHandler_RegistersHandlerAndWrapper()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRequestHandler<TestRequest, TestResponse, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handler = serviceProvider.GetService<IRequestHandler<TestRequest, TestResponse>>();
        Assert.NotNull(handler);
        Assert.IsType<TestRequestHandler>(handler);

        var wrapper = serviceProvider.GetService<IRequestHandlerWrapper>();
        Assert.NotNull(wrapper);
    }

    [Fact]
    public void AddPipelineBehavior_Generic_RegistersBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPipelineBehavior<TestRequest, TestResponse, LoggingBehavior<TestRequest, TestResponse>>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var behavior = serviceProvider.GetService<IPipelineBehavior<TestRequest, TestResponse>>();
        Assert.NotNull(behavior);
        Assert.IsType<LoggingBehavior<TestRequest, TestResponse>>(behavior);
    }

    [Fact]
    public void AddPipelineBehavior_WithType_RegistersBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // The behavior should be registered in the service collection
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void AddOpenPipelineBehavior_WithOpenGeneric_RegistersBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));

        // Assert
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void AddOpenPipelineBehavior_WithClosedGeneric_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddOpenPipelineBehavior(typeof(LoggingBehavior<TestRequest, TestResponse>)));
        Assert.Contains("open generic type", exception.Message);
    }

    [Fact]
    public void AddOpenPipelineBehavior_WithNonBehaviorType_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddOpenPipelineBehavior(typeof(TestRequestHandler)));
        Assert.Contains("Behavior type must", exception.Message);
    }

    [Fact]
    public async Task AddChannelMediator_WithPipelineBehavior_ExecutesBehavior()
    {
        // Arrange
        var loggingBehavior = new LoggingBehavior<TestRequest, TestResponse>();
        var services = new ServiceCollection();
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(loggingBehavior);
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new TestRequest("test");

        // Act
        await mediator.Send(request);
        await Task.Delay(50); // Give time for processing

        // Assert
        Assert.Contains("Before: TestRequest", loggingBehavior.Logs);
        Assert.Contains("After: TestRequest", loggingBehavior.Logs);
    }

    [Fact]
    public async Task AddChannelMediator_WithMultipleBehaviors_ExecutesInOrder()
    {
        // Arrange
        var loggingBehavior = new LoggingBehavior<TestRequest, TestResponse>();
        var validationBehavior = new ValidationBehavior<TestRequest, TestResponse>();

        var services = new ServiceCollection();
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(loggingBehavior);
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(validationBehavior);
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var request = new TestRequest("test");

        // Act
        await mediator.Send(request);
        await Task.Delay(50);

        // Assert
        Assert.Equal(2, loggingBehavior.Logs.Count);
    }

    [Fact]
    public void AddChannelMediator_WithMultipleAssemblies_RegistersAllHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Both handlers are in the same assembly
        services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handler1 = serviceProvider.GetService<IRequestHandler<TestRequest, TestResponse>>();
        var handler2 = serviceProvider.GetService<IRequestHandler<AnotherTestRequest, int>>();

        Assert.NotNull(handler1);
        Assert.NotNull(handler2);
    }


    [Fact]
    public void AddRequestHandler_RegistersHandlerAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRequestHandler<TestRequest, TestResponse, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var handler1 = scope1.ServiceProvider.GetRequiredService<IRequestHandler<TestRequest, TestResponse>>();
        var handler2 = scope2.ServiceProvider.GetRequiredService<IRequestHandler<TestRequest, TestResponse>>();

        Assert.NotSame(handler1, handler2);
    }
}
