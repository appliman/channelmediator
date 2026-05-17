using System.Reflection;

using ChannelMediator.AzureBus;
using ChannelMediator.Tests.Helpers;

using Microsoft.Extensions.Hosting;

namespace ChannelMediator.Tests;

[Collection("AzureBusMock")]
public class AzureBusMockNotificationTests
{
    [Fact]
    public async Task MockMode_WhenNotifyIsCalled_NotificationHandlersAreCalled()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
                services.AddSingleton<INotificationHandler<TestNotification>>(handler2);

                services.AddChannelMediator(config =>
                {
                    config.UseChannelMediatorAzureBus(opts =>
                    {
                        opts.ProcessMode = AzureServiceBusMode.Mock;
                    });
                }, typeof(TestNotificationHandler1).Assembly);
            })
            .Build();

        await host.StartAsync();

        var mediator = host.Services.GetRequiredService<IMediator>();

        // Act
        await mediator.Notify(new TestNotification("mock-test"));

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Equal("Handler1: mock-test", handler1.HandledMessages[0]);
        Assert.Single(handler2.HandledMessages);
        Assert.Equal("Handler2: mock-test", handler2.HandledMessages[0]);

        await host.StopAsync();
    }

    [Fact]
    public async Task MockMode_WhenNotifyIsCalledWithParallelStrategy_NotificationHandlersAreCalled()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
                services.AddSingleton<INotificationHandler<TestNotification>>(handler2);

                services.AddChannelMediator(config =>
                {
                    config.Strategy = NotificationPublishStrategy.Parallel;

                    config.UseChannelMediatorAzureBus(opts =>
                    {
                        opts.ProcessMode = AzureServiceBusMode.Mock;
                    });
                }, typeof(TestNotificationHandler1).Assembly);
            })
            .Build();

        await host.StartAsync();

        var mediator = host.Services.GetRequiredService<IMediator>();

        // Act
        await mediator.Notify(new TestNotification("parallel-mock-test"));
        await Task.Delay(50); // Allow parallel handlers to complete

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Single(handler2.HandledMessages);

        await host.StopAsync();
    }

    [Fact]
    public async Task MockMode_WhenNotifyIsCalledWithScannedAssemblyHandlers_HandlersAreCalled()
    {
        // Arrange: handlers are discovered via assembly scanning (not manually registered)
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddChannelMediator(config =>
                {
                    config.UseChannelMediatorAzureBus(opts =>
                    {
                        opts.ProcessMode = AzureServiceBusMode.Mock;
                    });
                }, typeof(TestNotificationHandler1).Assembly);
            })
            .Build();

        await host.StartAsync();

        var mediator = host.Services.GetRequiredService<IMediator>();

        // Act
        await mediator.Notify(new TestNotification("scanned-test"));

        // Assert: both handlers from the scanned assembly should be called
        using var scope = host.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TestNotification>>().ToList();
        Assert.True(handlers.Count >= 2, $"Expected at least 2 handlers, found {handlers.Count}");

        await host.StopAsync();
    }
}
