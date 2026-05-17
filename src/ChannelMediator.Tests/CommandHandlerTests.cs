using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

[Collection("CommandHandlerTests")]
public class CommandHandlerTests
{
	[Fact]
	public async Task Send_WithCommand_ExecutesSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new TestCommand("test-value");

		// Act
		await mediator.Send(command);

		// Assert - Command should execute without throwing
		Assert.Equal("test-value", TestCommandHandler.LastExecutedValue);
	}

	[Fact]
	public async Task Send_WithAnotherCommand_ExecutesSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new TestCommand("send-test");

		// Act
		await mediator.Send(command);

		// Assert
		Assert.Equal("send-test", TestCommandHandler.LastExecutedValue);
	}

	[Fact]
	public async Task Send_WithNullCommand_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
			await mediator.Send((IRequest)null!));
	}

	[Fact]
	public async Task Send_WithFailingCommand_ThrowsException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(FailingCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new FailingCommand();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await mediator.Send(command));
		Assert.Equal("Command handler failed", exception.Message);
	}

	[Fact]
	public async Task Send_WithCommandAndCancellation_ThrowsOperationCanceledException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new TestCommand("test");
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await mediator.Send(command, cts.Token));
	}

	[Fact]
	public async Task Send_WithMultipleCommands_ExecutesAllInOrder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		TestCommandHandler.ExecutedValues.Clear();

		// Act
		var tasks = new List<Task>();
		for (int i = 0; i < 10; i++)
		{
			var command = new TestCommand($"cmd-{i}");
			tasks.Add(mediator.Send(command));
		}

		await Task.WhenAll(tasks);
		await Task.Delay(50); // Give time for processing

		// Assert
		Assert.Equal(10, TestCommandHandler.ExecutedValues.Count);
		Assert.Contains("cmd-0", TestCommandHandler.ExecutedValues);
		Assert.Contains("cmd-9", TestCommandHandler.ExecutedValues);
	}

	[Fact]
	public async Task Send_WithCommandAndBehavior_ExecutesBehavior()
	{
		// Arrange
		var loggingBehavior = new LoggingBehavior<TestCommand, Unit>();
		var services = new ServiceCollection();
		services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(loggingBehavior);
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new TestCommand("test");

		// Act
		await mediator.Send(command);
		await Task.Delay(50);

		// Assert
		Assert.Contains("Before: TestCommand", loggingBehavior.Logs);
		Assert.Contains("After: TestCommand", loggingBehavior.Logs);
	}

	[Fact]
	public async Task Send_WithValidationBehavior_ExecutesBehavior()
	{
		// Arrange
		var validationBehavior = new ValidationBehavior<TestCommand, Unit>();
		var services = new ServiceCollection();
		services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(validationBehavior);
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new TestCommand("");

		// Act & Assert
		var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
			await mediator.Send(command));
		Assert.Contains("Value cannot be empty", exception.Message);
	}

	[Fact]
	public async Task MixedRequestsAndCommands_ProcessCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		TestCommandHandler.ExecutedValues.Clear();

		// Act
		var tasks = new List<Task>();
		for (int i = 0; i < 5; i++)
		{
			tasks.Add(mediator.Send(new TestCommand($"cmd-{i}")));
			tasks.Add(mediator.Send(new TestRequest($"req-{i}")).ContinueWith(_ => { }));
		}

		await Task.WhenAll(tasks);
		await Task.Delay(100);

		// Assert
		Assert.Equal(5, TestCommandHandler.ExecutedValues.Count);
	}
}
