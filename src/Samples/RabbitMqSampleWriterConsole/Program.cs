using System.Reflection;

using ChannelMediator;
using ChannelMediator.RabbitMQ;

using ChannelMediatorSampleShared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args);

host.ConfigureServices((context, services) =>
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
        });

    }, Assembly.GetExecutingAssembly());
});

var app = host.Build();

await app.StartAsync();

var mediator = app.Services.GetRequiredService<IMediator>();

await mediator.EnqueueRequest(new MyRequest("enqueue-test-via-rabbitmq"));
await mediator.Notify(new ProductAddedNotification("p01", 10, 100));

Console.WriteLine("Notification published to RabbitMQ. Press any key to exit.");
Console.ReadLine();
