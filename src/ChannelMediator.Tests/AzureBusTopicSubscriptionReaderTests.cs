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

public class AzureBusTopicSubscriptionReaderTests
{
	[Fact]
	public async Task WhenTopicNameDoesNotMatchMessageType_ThenNotificationIsStillConsumed()
	{
		// Arrange
		var handler = new TestNotificationHandler1();
		var services = new ServiceCollection();
		services.AddSingleton<INotificationHandler<TestNotification>>(handler);
		services.AddChannelMediator(null, typeof(TestNotificationHandler1).Assembly);
		var serviceProvider = services.BuildServiceProvider();
		using var loggerFactory = LoggerFactory.Create(_ => { });
		var serializerOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		await using var topicReader = new TopicSubscriptionReader(
			new ServiceBusClient("Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=fake="),
			new AzureServiceBusEntityManager(
				new ServiceBusAdministrationClient("Endpoint=sb://unit-test.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=fake="),
				NullLogger<AzureServiceBusEntityManager>.Instance),
			new TopicSubscriptionReaderOptions
			{
				TopicName = "forced-topic-name",
				SubscriptionName = "forced-subscription-name",
				MessageType = typeof(TestNotification)
			},
			serviceProvider,
			loggerFactory.CreateLogger<TopicSubscriptionReader>());

		var expectedMessage = $"forced-topic-{Guid.NewGuid():N}";
		var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
			body: new BinaryData(JsonSerializer.SerializeToUtf8Bytes(new TestNotification(expectedMessage), serializerOptions)),
			messageId: "forced-topic-message-id",
			properties: new Dictionary<string, object>
			{
				["messagetype"] = typeof(TestNotification).AssemblyQualifiedName!
			});

		var args = new ProcessMessageEventArgs(message, receiver: null!, CancellationToken.None);
		var processMessageAsync = typeof(TopicSubscriptionReader).GetMethod("ProcessMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic);

		// Act
		Assert.NotNull(processMessageAsync);
		var task = processMessageAsync.Invoke(topicReader, new object[] { args }) as Task;
		Assert.NotNull(task);
		await task!;

		// Assert
		Assert.Contains($"Handler1: {expectedMessage}", handler.HandledMessages);
	}
}
