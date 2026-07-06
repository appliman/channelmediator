using System.Reflection;

using ChannelMediator;
using ChannelMediator.AzureBus;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
	.UseEnvironment(Environments.Development)
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

				opts.AddAzureQueueRequestReader<MyRequest>(ForcedQueueNames.MyRequest);
				opts.AddAzureBusTopicNotificationReader<ProductAddedNotification>(opts.TopicSubscriberName, reader =>
				{
					reader.TopicName = $"{opts.Prefix}{ForcedQueueNames.ProductAddedNotification}";
				});
				opts.AddAzureBusTopicNotificationReader<OrderShippedNotification>(opts.TopicSubscriberName);
			});

		}, Assembly.GetExecutingAssembly());

	}).Build();

await host.RunAsync();

