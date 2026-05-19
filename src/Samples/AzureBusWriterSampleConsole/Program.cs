using System.Reflection;

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

await mediator.EnqueueRequest(new MyRequest("enqueue-test"));
await mediator.Notify(new ProductAddedNotification("p01", 10, 100));

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