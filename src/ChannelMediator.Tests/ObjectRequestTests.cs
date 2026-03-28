using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

[Collection("CommandHandlerTests")]
public class ObjectRequestTests
{
	[Fact]
	public async Task Send_WithObjectRequest_ReturnsExpectedResponse()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new TestRequest("test-object");

		// Act
		var response = await mediator.Send(request);

		// Assert
		Assert.NotNull(response);
		Assert.IsType<TestResponse>(response);
		Assert.Equal("Handled: test-object", ((TestResponse)response!).Result);
	}

	[Fact]
	public async Task Send_WithAnotherObjectRequest_ReturnsExpectedResponse()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new TestRequest("send-object");

		// Act
		var response = await mediator.Send(request);

		// Assert
		Assert.NotNull(response);
		Assert.IsType<TestResponse>(response);
		Assert.Equal("Handled: send-object", ((TestResponse)response!).Result);
	}

	[Fact]
	public async Task Send_WithObjectCommand_ReturnsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		TestCommandHandler.ExecutedValues.Clear();
		object command = new TestCommand("test-command");

		// Act
		var response = await mediator.Send(command);

		// Assert
		Assert.Null(response);
		Assert.Equal("test-command", TestCommandHandler.LastExecutedValue);
	}

	[Fact]
	public async Task Send_WithAnotherObjectCommand_ReturnsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		TestCommandHandler.ExecutedValues.Clear();
		object command = new TestCommand("send-command");

		// Act
		var response = await mediator.Send(command);

		// Assert
		Assert.Null(response);
		Assert.Equal("send-command", TestCommandHandler.LastExecutedValue);
	}

	[Fact]
	public async Task Send_WithNullObjectRequest_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
			await mediator.Send((object)null!));
	}

	[Fact]
	public async Task Send_WithAnotherNullObjectRequest_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
			await mediator.Send((object)null!));
	}

	[Fact]
	public async Task Send_WithInvalidObjectType_ThrowsArgumentException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object invalidRequest = "not a request";

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await mediator.Send(invalidRequest));
		Assert.Contains("does not implement IRequest", exception.Message);
	}

	[Fact]
	public async Task Send_WithAnotherInvalidObjectType_ThrowsArgumentException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object invalidRequest = 123;

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await mediator.Send(invalidRequest));
		Assert.Contains("does not implement IRequest", exception.Message);
	}

	[Fact]
	public async Task Send_WithObjectRequestWithIntResponse_ReturnsExpectedResponse()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(AnotherTestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new AnotherTestRequest(42);

		// Act
		var response = await mediator.Send(request);

		// Assert
		Assert.NotNull(response);
		Assert.Equal(84, response);
	}

	[Fact]
	public async Task Send_WithAnotherObjectRequestWithIntResponse_ReturnsExpectedResponse()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(AnotherTestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new AnotherTestRequest(10);

		// Act
		var response = await mediator.Send(request);

		// Assert
		Assert.NotNull(response);
		Assert.Equal(20, response);
	}

	[Fact]
	public async Task Send_WithObjectRequestAndCancellation_ThrowsOperationCanceledException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new TestRequest("test");
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await mediator.Send(request, cts.Token));
	}

	[Fact]
	public async Task Send_WithAnotherObjectRequestAndCancellation_ThrowsOperationCanceledException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new TestRequest("test");
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await mediator.Send(request, cts.Token));
	}

	[Fact]
	public async Task Send_WithMultipleObjectRequests_ProcessesAllCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		// Act
		var tasks = new List<Task<object?>>();
		for (int i = 0; i < 10; i++)
		{
			object request = new TestRequest($"test-{i}");
			tasks.Add(mediator.Send(request));
		}

		var results = await Task.WhenAll(tasks);

		// Assert
		Assert.Equal(10, results.Length);
		for (int i = 0; i < 10; i++)
		{
			Assert.IsType<TestResponse>(results[i]);
			Assert.Equal($"Handled: test-{i}", ((TestResponse)results[i]!).Result);
		}
	}

	[Fact]
	public async Task Send_WithAnotherMultipleObjectRequests_ProcessesAllCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		// Act
		var tasks = new List<Task<object?>>();
		for (int i = 0; i < 5; i++)
		{
			object request = new TestRequest($"send-{i}");
			tasks.Add(mediator.Send(request));
		}

		var results = await Task.WhenAll(tasks);

		// Assert
		Assert.Equal(5, results.Length);
		for (int i = 0; i < 5; i++)
		{
			Assert.IsType<TestResponse>(results[i]);
			Assert.Equal($"Handled: send-{i}", ((TestResponse)results[i]!).Result);
		}
	}

	[Fact]
	public async Task Send_WithMixedObjectRequestTypes_ProcessesCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		TestCommandHandler.ExecutedValues.Clear();

		// Act
		object request1 = new TestRequest("test1");
		object request2 = new AnotherTestRequest(15);
		object command = new TestCommand("cmd1");

		var response1 = await mediator.Send(request1);
		var response2 = await mediator.Send(request2);
		var response3 = await mediator.Send(command);

		await Task.Delay(50); // Give time for command processing

		// Assert
		Assert.IsType<TestResponse>(response1);
		Assert.Equal("Handled: test1", ((TestResponse)response1!).Result);

		Assert.Equal(30, response2);

		Assert.Null(response3);
		Assert.Contains("cmd1", TestCommandHandler.ExecutedValues);
	}

	[Fact]
	public async Task Send_WithAnotherMixedObjectRequestTypes_ProcessesCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		TestCommandHandler.ExecutedValues.Clear();

		// Act
		object request1 = new TestRequest("send-test");
		object request2 = new AnotherTestRequest(25);
		object command = new TestCommand("send-cmd");

		var response1 = await mediator.Send(request1);
		var response2 = await mediator.Send(request2);
		var response3 = await mediator.Send(command);

		await Task.Delay(50); // Give time for command processing

		// Assert
		Assert.IsType<TestResponse>(response1);
		Assert.Equal("Handled: send-test", ((TestResponse)response1!).Result);

		Assert.Equal(50, response2);

		Assert.Null(response3);
		Assert.Contains("send-cmd", TestCommandHandler.ExecutedValues);
	}

	[Fact]
	public async Task Send_WithFailingObjectRequest_ThrowsException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(FailingRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new FailingRequest();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await mediator.Send(request));
		Assert.Equal("Handler failed", exception.Message);
	}

	[Fact]
	public async Task Send_WithAnotherFailingObjectRequest_ThrowsException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(FailingRequestHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		object request = new FailingRequest();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await mediator.Send(request));
		Assert.Equal("Handler failed", exception.Message);
	}
}
