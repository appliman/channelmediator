using System.Reflection;

using ChannelMediator;
using ChannelMediator.RabbitMQ;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddChannelMediator(config =>
        {
            config.Strategy = NotificationPublishStrategy.Parallel;

            config.UseChannelMediatorRabbitMQ(opts =>
            {
                opts.Prefix = "sampleapp";
                opts.HostName = "192.168.10.2";
                opts.Port = 5672;
                opts.UserName = "guest";
                opts.Password = "guest";
                opts.TopicSubscriberName = "my-subscriber-name";

                opts.AddRabbitMqQueueRequestReader<MyRequest>();
                opts.AddAllRabbitMqTopicNotification();
            });

        }, Assembly.GetExecutingAssembly());

    }).Build();

await host.RunAsync();
