using ChannelMediator.InMemory;
using ChannelMediator.Tests.Helpers;

using Microsoft.Extensions.Hosting;

using System.Diagnostics;

namespace ChannelMediator.Tests;

[Collection("MemoryPublisher")]
public class MemoryPublisherTests
{
	[Fact]
	public async Task WhenNotifyIsCalled_NotificationHandlersAreCalled()
	{
		var handler1 = new TestNotificationHandler1();
		var handler2 = new TestNotificationHandler2();

		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
				services.AddSingleton<INotificationHandler<TestNotification>>(handler2);

				services.AddChannelMediator(config =>
				{
					config.UseChannelMediatorInMemory();
				}, typeof(TestNotificationHandler1).Assembly);
			})
			.Build();

		await host.StartAsync();

		var mediator = host.Services.GetRequiredService<IMediator>();

		await mediator.Notify(new TestNotification("memory-test"));

		await WaitUntilAsync(() => handler1.HandledMessages.Count == 1 && handler2.HandledMessages.Count == 1);

		Assert.Single(handler1.HandledMessages);
		Assert.Equal("Handler1: memory-test", handler1.HandledMessages[0]);
		Assert.Single(handler2.HandledMessages);
		Assert.Equal("Handler2: memory-test", handler2.HandledMessages[0]);

		await host.StopAsync();
	}

	[Fact]
	public async Task WhenEnqueueRequestIsCalled_RequestHandlerIsCalled()
	{
		MemoryTestCommandHandler.ExecutedValues.Clear();

		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				services.AddChannelMediator(config =>
				{
					config.UseChannelMediatorInMemory();
				}, typeof(TestCommandHandler).Assembly);
			})
			.Build();

		await host.StartAsync();

		var mediator = host.Services.GetRequiredService<IMediator>();

		await mediator.EnqueueRequest(new MemoryTestCommand("memory-command"));

		await WaitUntilAsync(() => MemoryTestCommandHandler.ExecutedValues.Contains("memory-command"));

		Assert.Contains("memory-command", MemoryTestCommandHandler.ExecutedValues);

		await host.StopAsync();
	}

	[Fact]
	public async Task WhenEnqueueRequestIsCalled_ReturnsBeforeRequestHandlerCompletes()
	{
		SlowMemoryTestCommandHandler.Reset();

		var host = Host.CreateDefaultBuilder()
			.ConfigureServices(services =>
			{
				services.AddChannelMediator(config =>
				{
					config.UseChannelMediatorInMemory();
				}, typeof(SlowMemoryTestCommandHandler).Assembly);
			})
			.Build();

		await host.StartAsync();

		var mediator = host.Services.GetRequiredService<IMediator>();
		var stopwatch = Stopwatch.StartNew();

		await mediator.EnqueueRequest(new SlowMemoryTestCommand("slow-memory-command"));

		stopwatch.Stop();

		Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100));
		Assert.False(SlowMemoryTestCommandHandler.Completed.Task.IsCompleted);

		SlowMemoryTestCommandHandler.Release.SetResult();
		await SlowMemoryTestCommandHandler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

		await host.StopAsync();
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

		while (!condition())
		{
			await Task.Delay(10, cts.Token);
		}
	}
}

public record MemoryTestCommand(string Value) : IRequest;

public class MemoryTestCommandHandler : IRequestHandler<MemoryTestCommand>
{
	public static List<string> ExecutedValues { get; } = new();

	public ValueTask HandleAsync(MemoryTestCommand request, CancellationToken cancellationToken)
	{
		ExecutedValues.Add(request.Value);
		return ValueTask.CompletedTask;
	}

	public Task Handle(MemoryTestCommand request, CancellationToken cancellationToken)
	{
		return HandleAsync(request, cancellationToken).AsTask();
	}
}

public record SlowMemoryTestCommand(string Value) : IRequest;

public class SlowMemoryTestCommandHandler : IRequestHandler<SlowMemoryTestCommand>
{
	public static TaskCompletionSource Release { get; private set; } = CreateTaskCompletionSource();

	public static TaskCompletionSource Completed { get; private set; } = CreateTaskCompletionSource();

	public static void Reset()
	{
		Release = CreateTaskCompletionSource();
		Completed = CreateTaskCompletionSource();
	}

	public async ValueTask HandleAsync(SlowMemoryTestCommand request, CancellationToken cancellationToken)
	{
		await Release.Task.WaitAsync(cancellationToken);
		Completed.SetResult();
	}

	public Task Handle(SlowMemoryTestCommand request, CancellationToken cancellationToken)
	{
		return HandleAsync(request, cancellationToken).AsTask();
	}

	private static TaskCompletionSource CreateTaskCompletionSource()
	{
		return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
