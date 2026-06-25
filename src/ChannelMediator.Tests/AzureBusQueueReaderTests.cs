using System.Reflection;
using System.Text.Json;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using ChannelMediator.AzureBus;
using ChannelMediator.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChannelMediator.Tests;

public class AzureBusQueueReaderTests
{
	[Fact]
	public async Task WhenQueueNameDoesNotMatchMessageType_ThenRequestIsStillConsumed()
	{
		// Arrange
		var expectedValue = $"forced-value-{Guid.NewGuid():N}";
		var services = new ServiceCollection();
		services.AddChannelMediator(null, typeof(TestCommandHandler).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		using var loggerFactory = LoggerFactory.Create(_ => { });
		var serializerOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		await using var queueReader = new QueueReader(
			new ServiceBusClient("Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=fake="),
			new AzureServiceBusEntityManager(
				new ServiceBusAdministrationClient("Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=fake="),
				NullLogger<AzureServiceBusEntityManager>.Instance),
			new QueueReaderOptions
			{
				QueueName = "forced-queue-name",
				RequestType = typeof(TestCommand)
			},
			serviceProvider,
			loggerFactory.CreateLogger<QueueReader>());

		var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
			body: new BinaryData(JsonSerializer.SerializeToUtf8Bytes(new TestCommand(expectedValue), serializerOptions)),
			messageId: "forced-message-id",
			properties: new Dictionary<string, object>
			{
				["messagetype"] = typeof(TestCommand).AssemblyQualifiedName!
			});

		var args = new ProcessMessageEventArgs(message, receiver: null!, CancellationToken.None);
		var processMessageAsync = typeof(QueueReader).GetMethod("ProcessMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic);

		// Act
		Assert.NotNull(processMessageAsync);
		var task = processMessageAsync.Invoke(queueReader, new object[] { args }) as Task;
		Assert.NotNull(task);
		await task!;

		// Assert
		Assert.Contains(expectedValue, TestCommandHandler.ExecutedValues);
	}
}
