using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

public class MediatorFactoryTests
{
	[Fact]
	public void CreateMediator_ReturnsNewMediatorInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IMediatorFactory>();

		// Act
		var mediator1 = factory.CreateMediator();
		var mediator2 = factory.CreateMediator();

		// Assert
		mediator1.Should().NotBeNull();
		mediator2.Should().NotBeNull();
		mediator1.Should().NotBeSameAs(mediator2);
	}

	[Fact]
	public async Task CreateMediator_ReturnsFunctionalMediator()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IMediatorFactory>();
		var mediator = factory.CreateMediator();

		var request = new TestRequest("factory-test");

		// Act
		var response = await mediator.Send(request);

		// Assert
		response.Should().NotBeNull();
		response.Result.Should().Be("Handled: factory-test");
	}

	[Fact]
	public async Task CreateMediator_MultipleMediators_WorkIndependently()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IMediatorFactory>();

		var mediator1 = factory.CreateMediator();
		var mediator2 = factory.CreateMediator();

		// Act
		var task1 = mediator1.Send(new TestRequest("mediator1-test"));
		var task2 = mediator2.Send(new TestRequest("mediator2-test"));

		var results = await Task.WhenAll(task1, task2);

		// Assert
		results[0].Result.Should().Be("Handled: mediator1-test");
		results[1].Result.Should().Be("Handled: mediator2-test");
	}

	[Fact]
	public void Constructor_WithNullHandlers_ThrowsArgumentNullException()
	{
		// Arrange
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		var notificationHandlers = new Dictionary<Type, INotificationHandlerWrapper>();
		var config = new ChannelMediatorConfiguration();

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() =>
			new MediatorFactory(null!, notificationHandlers, serviceProvider, config));
	}

	[Fact]
	public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
	{
		// Arrange
		var handlers = new Dictionary<Type, IRequestHandlerWrapper>();
		var notificationHandlers = new Dictionary<Type, INotificationHandlerWrapper>();
		var config = new ChannelMediatorConfiguration();

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() =>
			new MediatorFactory(handlers, notificationHandlers, null!, config));
	}

	[Fact]
	public void Constructor_WithNullNotificationHandlers_UsesEmptyDictionary()
	{
		// Arrange
		var handlers = new Dictionary<Type, IRequestHandlerWrapper>();
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		var config = new ChannelMediatorConfiguration();

		// Act
		var factory = new MediatorFactory(handlers, null!, serviceProvider, config);

		// Assert
		factory.Should().NotBeNull();
		var mediator = factory.CreateMediator();
		mediator.Should().NotBeNull();
	}

	[Fact]
	public void Constructor_WithNullNotificationConfiguration_UsesDefaultConfiguration()
	{
		// Arrange
		var handlers = new Dictionary<Type, IRequestHandlerWrapper>();
		var notificationHandlers = new Dictionary<Type, INotificationHandlerWrapper>();
		var serviceProvider = new ServiceCollection().BuildServiceProvider();

		// Act
		var factory = new MediatorFactory(handlers, notificationHandlers, serviceProvider, null!);

		// Assert
		factory.Should().NotBeNull();
		var mediator = factory.CreateMediator();
		mediator.Should().NotBeNull();
	}

	[Fact]
	public async Task CreateMediator_WithNotificationHandlers_CanPublishNotifications()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(config =>
		{
			config.Strategy = NotificationPublishStrategy.Sequential;
		}, typeof(TestNotificationHandler1).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IMediatorFactory>();
		var mediator = factory.CreateMediator();

		var notification = new TestNotification("factory-notification-test");

		// Act & Assert - Should not throw
		await mediator.Publish(notification);
	}

	[Fact]
	public async Task CreateMediator_DisposingOneMediator_DoesNotAffectOthers()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IMediatorFactory>();

		var mediator1 = factory.CreateMediator() as Mediator;
		var mediator2 = factory.CreateMediator();

		// Act - Dispose mediator1
		await mediator1!.DisposeAsync();

		// Assert - mediator2 should still work
		var response = await mediator2.Send(new TestRequest("still-working"));
		response.Result.Should().Be("Handled: still-working");
	}

	[Fact]
	public void Factory_IsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();

		// Act
		var factory1 = serviceProvider.GetRequiredService<IMediatorFactory>();
		var factory2 = serviceProvider.GetRequiredService<IMediatorFactory>();

		// Assert
		factory1.Should().BeSameAs(factory2);
	}

	[Fact]
	public async Task CreateMediator_CanHandleCommands()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommandHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IMediatorFactory>();
		var mediator = factory.CreateMediator();

		var command = new TestCommand("factory-command-test");

		// Act
		await mediator.Send(command);

		// Assert
		TestCommandHandler.LastExecutedValue.Should().Be("factory-command-test");
	}
}
