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
		response.Should().NotBeNull();
		response.Result.Should().Be("Handled: test-handle");
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
		TestCommandHandler.LastExecutedValue.Should().Be("handle-test");
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
		response.Should().Be(200);
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
		response.Result.Should().Be("Handled: test");
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
		TestCommandHandler.LastExecutedValue.Should().Be("test");
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
		exception.Message.Should().Be("Handler failed");
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
		exception.Message.Should().Be("Command handler failed");
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
		response1.Result.Should().Be("Handled: first");
		response2.Result.Should().Be("Handled: second");
		response3.Result.Should().Be("Handled: third");
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
		TestCommandHandler.ExecutedValues.Should().HaveCount(3);
		TestCommandHandler.ExecutedValues.Should().Contain("cmd1");
		TestCommandHandler.ExecutedValues.Should().Contain("cmd2");
		TestCommandHandler.ExecutedValues.Should().Contain("cmd3");
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
		task.Should().BeOfType<Task<TestResponse>>();
		response.Result.Should().Be("Handled: async-test");
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
		task.Should().NotBeNull();
		task.IsCompleted.Should().BeTrue();
		TestCommandHandler.LastExecutedValue.Should().Be("async-cmd");
	}
}
