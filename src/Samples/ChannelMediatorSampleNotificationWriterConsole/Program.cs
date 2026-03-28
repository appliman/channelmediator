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

		config.UseChannelMediatorAzureBus(opts =>
		{
			opts.Prefix = "sampleapp";
			opts.ConnectionString = connectionString!;
			opts.TopicSubscriberName = "my-subscriber-name";
		});

	}, Assembly.GetExecutingAssembly());
});

var app = host.Build();

await app.StartAsync();

var mediator = app.Services.GetRequiredService<IMediator>();

await mediator.EnqueueRequest(new MyRequest("enqueue-test"));
await mediator.Notify(new ProductAddedNotification("p01",10, 100));

Console.WriteLine("Notification published. Press any key to exit.");
Console.ReadLine();