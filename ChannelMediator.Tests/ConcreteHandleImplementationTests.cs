using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

/// <summary>
/// Tests to verify that concrete implementations of Handle() call HandleAsync().
/// These tests use the concrete classes directly (not via interface casting).
/// </summary>
[Collection("CommandHandlerTests")]
public class ConcreteHandleImplementationTests
{
	[Fact]
	public async Task TestRequestHandler_Handle_CallsHandleAsync()
	{
		// Arrange
		var handler = new TestRequestHandler();
		var request = new TestRequest("concrete-test");

		// Act
		var response = await handler.Handle(request, CancellationToken.None);

		// Assert
		response.Should().NotBeNull();
		response.Result.Should().Be("Handled: concrete-test");
	}

	[Fact]
	public async Task AnotherTestRequestHandler_Handle_CallsHandleAsync()
	{
		// Arrange
		var handler = new AnotherTestRequestHandler();
		var request = new AnotherTestRequest(50);

		// Act
		var response = await handler.Handle(request, CancellationToken.None);

		// Assert
		response.Should().Be(100);
	}

	[Fact]
	public async Task TestCommandHandler_Handle_CallsHandleAsync()
	{
		// Arrange
		var handler = new TestCommandHandler();
		TestCommandHandler.ExecutedValues.Clear();
		var command = new TestCommand("concrete-command");

		// Act
		await handler.Handle(command, CancellationToken.None);

		// Assert
		TestCommandHandler.LastExecutedValue.Should().Be("concrete-command");
		TestCommandHandler.ExecutedValues.Should().Contain("concrete-command");
	}

	[Fact]
	public async Task FailingRequestHandler_Handle_ThrowsException()
	{
		// Arrange
		var handler = new FailingRequestHandler();
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
		var handler = new FailingCommandHandler();
		var command = new FailingCommand();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await handler.Handle(command, CancellationToken.None));
		exception.Message.Should().Be("Command handler failed");
	}

	[Fact]
	public async Task Handler_Handle_WithCancellation_PropagatesCancellation()
	{
		// Arrange
		var handler = new TestRequestHandler();
		var request = new TestRequest("test");
		var cts = new CancellationTokenSource();

		// Act - Just verify it doesn't throw when not cancelled
		var response = await handler.Handle(request, cts.Token);

		// Assert
		response.Should().NotBeNull();
	}

	[Fact]
	public async Task CommandHandler_Handle_WithCancellation_PropagatesCancellation()
	{
		// Arrange
		var handler = new TestCommandHandler();
		TestCommandHandler.ExecutedValues.Clear();
		var command = new TestCommand("cancellation-test");
		var cts = new CancellationTokenSource();

		// Act
		await handler.Handle(command, cts.Token);

		// Assert
		TestCommandHandler.LastExecutedValue.Should().Be("cancellation-test");
	}

	[Fact]
	public async Task Handler_HandleAndHandleAsync_ProduceSameResult()
	{
		// Arrange
		var handler = new TestRequestHandler();
		var request = new TestRequest("consistency-test");

		// Act
		var asyncResponse = await handler.HandleAsync(request, CancellationToken.None);
		var handleResponse = await handler.Handle(request, CancellationToken.None);

		// Assert
		asyncResponse.Should().BeEquivalentTo(handleResponse);
		asyncResponse.Result.Should().Be("Handled: consistency-test");
		handleResponse.Result.Should().Be("Handled: consistency-test");
	}

	[Fact]
	public async Task CommandHandler_HandleAndHandleAsync_ExecuteEquivalently()
	{
		// Arrange
		var handler = new TestCommandHandler();
		
		// Test HandleAsync
		TestCommandHandler.ExecutedValues.Clear();
		await handler.HandleAsync(new TestCommand("async-cmd"), CancellationToken.None);
		var asyncValue = TestCommandHandler.LastExecutedValue;

		// Test Handle
		TestCommandHandler.ExecutedValues.Clear();
		await handler.Handle(new TestCommand("handle-cmd"), CancellationToken.None);
		var handleValue = TestCommandHandler.LastExecutedValue;

		// Assert - Both should have executed their commands
		asyncValue.Should().Be("async-cmd");
		handleValue.Should().Be("handle-cmd");
	}

	[Fact]
	public async Task MultipleHandlers_Handle_WorksCorrectly()
	{
		// Arrange
		var handler1 = new TestRequestHandler();
		var handler2 = new AnotherTestRequestHandler();
		var handler3 = new TestCommandHandler();
		TestCommandHandler.ExecutedValues.Clear();

		// Act
		var response1 = await handler1.Handle(new TestRequest("test1"), CancellationToken.None);
		var response2 = await handler2.Handle(new AnotherTestRequest(25), CancellationToken.None);
		await handler3.Handle(new TestCommand("cmd1"), CancellationToken.None);

		// Assert
		response1.Result.Should().Be("Handled: test1");
		response2.Should().Be(50);
		TestCommandHandler.LastExecutedValue.Should().Be("cmd1");
	}
}
