// See https://aka.ms/new-console-template for more information
using System.Reflection;

using ChannelMediator;
using ChannelMediator.AzureBus;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args);

host.ConfigureServices((context, services) =>
{
	var connectionString = context.Configuration.GetConnectionString("AzureBusConnectionString");
	services.AddChannelMediator(config =>
	{
		config.Strategy = NotificationPublishStrategy.Parallel;

		config.UseAzureServiceBus(opts =>
		{
			opts.ConnectionString = connectionString!;
		});

	}, Assembly.GetExecutingAssembly());
});

var app = host.Build();

await app.StartAsync();

var mediator = app.Services.GetRequiredService<IMediator>();

// await mediator.GlobalPublish(new ProductAddedNotification("test", 123, 9.99m));

await mediator.EnqueueRequest(new MyRequest("enqueue-test"));
await mediator.Enqueue("my-custom-queue", new NotRequest() { Value = 5 });

Console.WriteLine("Notification published. Press any key to exit.");
Console.ReadLine();