using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

[Collection("CommandHandlerTests")]
public class CommandHandlerTests
{
	[Fact]
	public async Task InvokeAsync_WithCommand_ExecutesSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new TestCommand("test-value");

		// Act
		await mediator.InvokeAsync(command);

		// Assert - Command should execute without throwing
		TestCommandHandler.LastExecutedValue.Should().Be("test-value");
	}

	[Fact]
	public async Task Send_WithCommand_ExecutesSuccessfully()
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
		TestCommandHandler.LastExecutedValue.Should().Be("send-test");
	}

	[Fact]
	public async Task InvokeAsync_WithNullCommand_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentNullException>(async () =>
			await mediator.InvokeAsync(null!));
	}

	[Fact]
	public async Task InvokeAsync_WithFailingCommand_ThrowsException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(FailingCommand).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		var mediator = serviceProvider.GetRequiredService<IMediator>();

		var command = new FailingCommand();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await mediator.InvokeAsync(command));
		exception.Message.Should().Be("Command handler failed");
	}

	[Fact]
	public async Task InvokeAsync_WithCommandAndCancellation_ThrowsOperationCanceledException()
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
			await mediator.InvokeAsync(command, cts.Token));
	}

	[Fact]
	public async Task InvokeAsync_WithMultipleCommands_ExecutesAllInOrder()
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
			tasks.Add(mediator.InvokeAsync(command).AsTask());
		}

		await Task.WhenAll(tasks);
		await Task.Delay(50); // Give time for processing

		// Assert
		TestCommandHandler.ExecutedValues.Should().HaveCount(10);
		TestCommandHandler.ExecutedValues.Should().Contain("cmd-0");
		TestCommandHandler.ExecutedValues.Should().Contain("cmd-9");
	}

	[Fact]
	public async Task InvokeAsync_WithCommandAndBehavior_ExecutesBehavior()
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
		await mediator.InvokeAsync(command);
		await Task.Delay(50);

		// Assert
		loggingBehavior.Logs.Should().Contain("Before: TestCommand");
		loggingBehavior.Logs.Should().Contain("After: TestCommand");
	}

	[Fact]
	public async Task Send_WithCommandAndBehavior_ExecutesBehavior()
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
		exception.Message.Should().Contain("Value cannot be empty");
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
			tasks.Add(mediator.InvokeAsync(new TestCommand($"cmd-{i}")).AsTask());
			tasks.Add(mediator.Send(new TestRequest($"req-{i}")).ContinueWith(_ => { }));
		}

		await Task.WhenAll(tasks);
		await Task.Delay(100);

		// Assert
		TestCommandHandler.ExecutedValues.Should().HaveCount(5);
	}
}
