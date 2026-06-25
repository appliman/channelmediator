using System.Reflection;
using System.Text.Json;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using ChannelMediator;
using ChannelMediator.AzureBus;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args);
host.UseEnvironment(Environments.Development);
host.ConfigureServices((context, services) =>
{
	var connectionString = context.Configuration["ConnectionStrings:AzureBusConnectionString"];
	services.AddChannelMediator(config =>
	{
		config.Strategy = NotificationPublishStrategy.Parallel;

		config.UseChannelMediatorAzureBus(opts =>
		{
			opts.Prefix = "sampleapp-";
			opts.ConnectionString = connectionString!;
			opts.TopicSubscriberName = "my-subscriber-name";
		});

	}, Assembly.GetExecutingAssembly());
});

var app = host.Build();

await app.StartAsync();

var mediator = app.Services.GetRequiredService<IMediator>();
var serviceBusClient = app.Services.GetRequiredService<ServiceBusClient>();
var administrationClient = app.Services.GetRequiredService<ServiceBusAdministrationClient>();

const string queuePrefix = "sampleapp-";
var forcedQueueName = $"{queuePrefix}{ForcedQueueNames.MyRequest}";
var forcedTopicName = $"{queuePrefix}{ForcedQueueNames.ProductAddedNotification}";

if (!await administrationClient.QueueExistsAsync(forcedQueueName))
{
	await administrationClient.CreateQueueAsync(new CreateQueueOptions(forcedQueueName));
}

await using var forcedQueueSender = serviceBusClient.CreateSender(forcedQueueName);
await using var forcedTopicSender = serviceBusClient.CreateSender(forcedTopicName);
var serializerOptions = new JsonSerializerOptions
{
	PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var forcedQueueRequest = new MyRequest("enqueue-test-via-forced-queue");
var forcedQueueMessage = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(forcedQueueRequest, serializerOptions))
{
	ContentType = "application/json",
	Subject = nameof(MyRequest),
	ApplicationProperties =
	{
		["messagetype"] = typeof(MyRequest).AssemblyQualifiedName
	}
};

await forcedQueueSender.SendMessageAsync(forcedQueueMessage);
Console.WriteLine($"[WRITER] Sent MyRequest to forced queue '{forcedQueueName}' even though the queue name differs from the message type.");

if (!await administrationClient.TopicExistsAsync(forcedTopicName))
{
	await administrationClient.CreateTopicAsync(new CreateTopicOptions(forcedTopicName));
}

var forcedTopicNotification = new ProductAddedNotification("p01", 10, 100);
var forcedTopicMessage = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(forcedTopicNotification, serializerOptions))
{
	ContentType = "application/json",
	Subject = nameof(ProductAddedNotification),
	ApplicationProperties =
	{
		["messagetype"] = typeof(ProductAddedNotification).AssemblyQualifiedName
	}
};

await forcedTopicSender.SendMessageAsync(forcedTopicMessage);
Console.WriteLine($"[WRITER] Sent ProductAddedNotification to forced topic '{forcedTopicName}' even though the topic name differs from the message type.");

// Publish multiple OrderShippedNotification messages and verify delivery via logs
var orders = new[]
{
	new OrderShippedNotification("ORD-001", "Paris, France",       DateTimeOffset.UtcNow),
	new OrderShippedNotification("ORD-002", "Lyon, France",        DateTimeOffset.UtcNow.AddSeconds(1)),
	new OrderShippedNotification("ORD-003", "Marseille, France",   DateTimeOffset.UtcNow.AddSeconds(2)),
};

foreach (var order in orders)
{
	await mediator.Notify(order);
	Console.WriteLine($"[WRITER] Sent OrderShippedNotification for order {order.OrderId} → {order.Destination}");
}

Console.WriteLine();
Console.WriteLine("All notifications published. Start AzureBusReaderSampleConsole to verify the topic receives the messages.");
Console.WriteLine("Press any key to exit.");
Console.ReadLine();