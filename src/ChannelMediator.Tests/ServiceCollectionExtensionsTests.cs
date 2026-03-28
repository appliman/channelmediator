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
        mediator.Should().NotBeNull();
        mediator.Should().BeOfType<Mediator>();
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
        mediator.Should().NotBeNull();
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
        configuredStrategy.Should().Be(NotificationPublishStrategy.Parallel);
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
        handler.Should().NotBeNull();
        handler.Should().BeOfType<TestRequestHandler>();
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
        handlers.Should().NotBeNull();
        handlers.Should().HaveCountGreaterThan(1);
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
        wrappers.Should().NotBeEmpty();
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
        handler.Should().NotBeNull();
        handler.Should().BeOfType<TestRequestHandler>();

        var wrapper = serviceProvider.GetService<IRequestHandlerWrapper>();
        wrapper.Should().NotBeNull();
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
        behavior.Should().NotBeNull();
        behavior.Should().BeOfType<LoggingBehavior<TestRequest, TestResponse>>();
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
        services.Should().Contain(sd => sd.ServiceType == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void AddOpenPipelineBehavior_WithOpenGeneric_RegistersBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenPipelineBehavior(typeof(LoggingBehavior<,>));

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(IPipelineBehavior<,>));
    }

    [Fact]
    public void AddOpenPipelineBehavior_WithClosedGeneric_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddOpenPipelineBehavior(typeof(LoggingBehavior<TestRequest, TestResponse>)));
        exception.Message.Should().Contain("open generic type");
    }

    [Fact]
    public void AddOpenPipelineBehavior_WithNonBehaviorType_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddOpenPipelineBehavior(typeof(TestRequestHandler)));
        exception.Message.Should().Contain("Behavior type must");
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
        loggingBehavior.Logs.Should().Contain("Before: TestRequest");
        loggingBehavior.Logs.Should().Contain("After: TestRequest");
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
        loggingBehavior.Logs.Should().HaveCount(2);
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

        handler1.Should().NotBeNull();
        handler2.Should().NotBeNull();
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

        handler1.Should().NotBeSameAs(handler2);
    }
}
