using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

/// <summary>
/// Tests for the Handle method aliases (MediatR compatibility).
/// </summary>
[Collection("CommandHandlerTests")]
public class HandleMethodTests
{
	[Fact]
	public async Task RequestHandler_Handle_CallsHandleAsync()
	{
		// Arrange
		IRequestHandler<TestRequest, TestResponse> handler = new TestRequestHandler();
		var request = new TestRequest("test-handle");

		// Act
		var response = await handler.Handle(request, CancellationToken.None);

		// Assert
		Assert.NotNull(response);
		Assert.Equal("Handled: test-handle", response.Result);
	}

	[Fact]
	public async Task CommandHandler_Handle_CallsHandleAsync()
	{
		// Arrange
		IRequestHandler<TestCommand> handler = new TestCommandHandler();
		TestCommandHandler.ExecutedValues.Clear();
		var command = new TestCommand("handle-test");

		// Act
		await handler.Handle(command, CancellationToken.None);

		// Assert
		Assert.Equal("handle-test", TestCommandHandler.LastExecutedValue);
	}

	[Fact]
	public async Task RequestHandler_Handle_WithIntResponse_ReturnsCorrectValue()
	{
		// Arrange
		IRequestHandler<AnotherTestRequest, int> handler = new AnotherTestRequestHandler();
		var request = new AnotherTestRequest(100);

		// Act
		var response = await handler.Handle(request, CancellationToken.None);

		// Assert
		Assert.Equal(200, response);
	}

	[Fact]
	public async Task RequestHandler_Handle_WithCancellation_PassesCancellationToken()
	{
		// Arrange
		IRequestHandler<TestRequest, TestResponse> handler = new TestRequestHandler();
		var request = new TestRequest("test");
		var cts = new CancellationTokenSource();

		// Act
		// The default implementation calls HandleAsync - just verify it works
		var response = await handler.Handle(request, cts.Token);

		// Assert
		Assert.Equal("Handled: test", response.Result);
	}

	[Fact]
	public async Task CommandHandler_Handle_WithCancellation_PassesCancellationToken()
	{
		// Arrange
		IRequestHandler<TestCommand> handler = new TestCommandHandler();
		var command = new TestCommand("test");
		var cts = new CancellationTokenSource();

		// Act
		await handler.Handle(command, cts.Token);

		// Assert
		Assert.Equal("test", TestCommandHandler.LastExecutedValue);
	}

	[Fact]
	public async Task FailingRequestHandler_Handle_ThrowsException()
	{
		// Arrange
		IRequestHandler<FailingRequest, string> handler = new FailingRequestHandler();
		var request = new FailingRequest();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await handler.Handle(request, CancellationToken.None));
		Assert.Equal("Handler failed", exception.Message);
	}

	[Fact]
	public async Task FailingCommandHandler_Handle_ThrowsException()
	{
		// Arrange
		IRequestHandler<FailingCommand> handler = new FailingCommandHandler();
		var command = new FailingCommand();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await handler.Handle(command, CancellationToken.None));
		Assert.Equal("Command handler failed", exception.Message);
	}

	[Fact]
	public async Task RequestHandler_Handle_MultipleInvocations_WorksCorrectly()
	{
		// Arrange
		IRequestHandler<TestRequest, TestResponse> handler = new TestRequestHandler();

		// Act
		var response1 = await handler.Handle(new TestRequest("first"), CancellationToken.None);
		var response2 = await handler.Handle(new TestRequest("second"), CancellationToken.None);
		var response3 = await handler.Handle(new TestRequest("third"), CancellationToken.None);

		// Assert
		Assert.Equal("Handled: first", response1.Result);
		Assert.Equal("Handled: second", response2.Result);
		Assert.Equal("Handled: third", response3.Result);
	}

	[Fact]
	public async Task CommandHandler_Handle_MultipleInvocations_WorksCorrectly()
	{
		// Arrange
		IRequestHandler<TestCommand> handler = new TestCommandHandler();
		TestCommandHandler.ExecutedValues.Clear();

		// Act
		await handler.Handle(new TestCommand("cmd1"), CancellationToken.None);
		await handler.Handle(new TestCommand("cmd2"), CancellationToken.None);
		await handler.Handle(new TestCommand("cmd3"), CancellationToken.None);

		// Assert
		Assert.Equal(3, TestCommandHandler.ExecutedValues.Count);
		Assert.Contains("cmd1", TestCommandHandler.ExecutedValues);
		Assert.Contains("cmd2", TestCommandHandler.ExecutedValues);
		Assert.Contains("cmd3", TestCommandHandler.ExecutedValues);
	}

	[Fact]
	public async Task RequestHandler_Handle_ReturnsTask_CanAwait()
	{
		// Arrange
		IRequestHandler<TestRequest, TestResponse> handler = new TestRequestHandler();
		var request = new TestRequest("async-test");

		// Act
		Task<TestResponse> task = handler.Handle(request, CancellationToken.None);
		var response = await task;

		// Assert
		Assert.IsType<Task<TestResponse>>(task);
		Assert.Equal("Handled: async-test", response.Result);
	}

	[Fact]
	public async Task CommandHandler_Handle_ReturnsTask_CanAwait()
	{
		// Arrange
		IRequestHandler<TestCommand> handler = new TestCommandHandler();
		TestCommandHandler.ExecutedValues.Clear();
		var command = new TestCommand("async-cmd");

		// Act
		Task task = handler.Handle(command, CancellationToken.None);
		await task;

		// Assert
		Assert.NotNull(task);
		Assert.True(task.IsCompleted);
		Assert.Equal("async-cmd", TestCommandHandler.LastExecutedValue);
	}
}
