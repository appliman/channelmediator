using System.Reflection;

using ChannelMediator;
using ChannelMediator.AzureBus;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
	.ConfigureServices((context, services) =>
	{
		var connectionString = context.Configuration.GetConnectionString("AzureBusConnectionString");
		services.AddChannelMediator(config =>
		{
			config.Strategy = NotificationPublishStrategy.Parallel;

			config.UseChannelMediatorAzureBus(opts =>
			{
				opts.Prefix = "sampleapp-";
				opts.ConnectionString = connectionString!;
				opts.TopicSubscriberName = "my-subscriber-name";

				opts.AddAzureQueueRequestReader<MyRequest>();
				opts.AddAllAzureBusTopicNotification();
			});

		}, Assembly.GetExecutingAssembly());

	}).Build();

await host.RunAsync();

