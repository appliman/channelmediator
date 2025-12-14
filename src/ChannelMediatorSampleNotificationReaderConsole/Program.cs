// See https://aka.ms/new-console-template for more information
using System.Reflection;
using System.Threading;

using ChannelMediator;
using ChannelMediator.AzureBus;

using ChannelMediatorSampleNotificationReaderConsole;

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
                opts.AddAzureBusTopicReader<ProductAddedNotification>();
            });

        }, Assembly.GetExecutingAssembly());

    }).Build();

await host.RunAsync();

