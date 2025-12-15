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

			config.UseAzureServiceBus(opts =>
			{
				opts.ConnectionString = connectionString!;
				opts.AddAzureBusTopicNotificationReader<ProductAddedNotification>();
				opts.AddAzureQueueRequestReader<MyRequest>();
				opts.AddAzureQueueReader<NotRequest>("my-custom-queue", async (mediator, message) =>
				{
					// Custom message processing logic here
					Console.WriteLine($"Custom Queue Reader received message: {message.Value}");
					await Task.CompletedTask;
                });
            });

		}, Assembly.GetExecutingAssembly());

	}).Build();

await host.RunAsync();

