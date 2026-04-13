using ChannelMediator.Tests.Helpers;
using System.Threading.Channels;

namespace ChannelMediator.Tests;

public class MediatorTests
{
	[Fact]
	public async Task Dispose_CompletesChannelAndStopsPump()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

		// Act
		mediator!.Dispose();

		// Assert - After dispose, sending should fail
		var request = new TestRequest("test");
		await Assert.ThrowsAsync<ChannelClosedException>(async () =>
			await mediator.Send(request));
	}

	[Fact]
	public async Task Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

		// Act & Assert - Should not throw
		mediator!.Dispose();
		mediator.Dispose();
	}

	[Fact]
	public async Task DisposeAsync_CompletesChannelAndStopsPump()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

		// Act
		await mediator!.DisposeAsync();

		// Assert - After dispose, sending should fail
		var request = new TestRequest("test");
		await Assert.ThrowsAsync<ChannelClosedException>(async () =>
			await mediator.Send(request));
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

		// Act & Assert - Should not throw
		await mediator!.DisposeAsync();
		await mediator.DisposeAsync();
	}

	[Fact]
	public async Task ServiceProvider_CanDisposeMediator_Synchronously()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();

		// Act - Get mediator and let scope dispose it synchronously
		using (var scope = serviceProvider.CreateScope())
		{
			var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
			var request = new TestRequest("dispose-test");
			var response = await mediator.Send(request);
			Assert.Equal("Handled: dispose-test", response.Result);
		}
		// Scope.Dispose() is called here - should not throw

		// Assert - We got here without exception
		Assert.True(true);
	}

	[Fact]
	public async Task ServiceProvider_CanDisposeMediator_Asynchronously()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();

		// Act - Get mediator and let scope dispose it asynchronously
		await using (var scope = serviceProvider.CreateAsyncScope())
		{
			var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
			var request = new TestRequest("async-dispose-test");
			var response = await mediator.Send(request);
			Assert.Equal("Handled: async-dispose-test", response.Result);
		}
		// Scope.DisposeAsync() is called here - should not throw

		// Assert - We got here without exception
		Assert.True(true);
	}

	[Fact]
	public void Constructor_WithEnumerable_CreatesDictionaryFromWrappers()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var wrappers = serviceProvider.GetServices<IRequestHandlerWrapper>().ToList();

		// Act
		var mediator = new Mediator(wrappers);

		// Assert
		Assert.NotNull(mediator);
	}

	[Fact]
	public void Constructor_WithNullHandlers_ThrowsArgumentNullException()
	{
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() =>
			new Mediator((FrozenDictionary<Type, IRequestHandlerWrapper>)null!));
	}

	[Fact]
	public async Task Dispose_AfterProcessingRequests_CompletesGracefully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>() as Mediator;

		// Process some requests first
		var request1 = new TestRequest("test1");
		var request2 = new TestRequest("test2");
		await mediator!.Send(request1);
		await mediator.Send(request2);

		// Act
		mediator.Dispose();

		// Assert - Should complete without hanging
		Assert.True(true);
	}
}
