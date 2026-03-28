using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

public class NotificationHandlerWrapperTests
{
    [Fact]
    public void NotificationType_ReturnsCorrectType()
    {
        // Arrange
        var wrapper = new NotificationHandlerWrapper<TestNotification>();

        // Act
        var notificationType = wrapper.NotificationType;

        // Assert
        Assert.Equal(typeof(TestNotification), notificationType);
    }

    [Fact]
    public async Task HandleAsync_WithSingleHandler_CallsHandler()
    {
        // Arrange
        var handler = new TestNotificationHandler1();
        var services = new ServiceCollection();
        services.AddScoped<INotificationHandler<TestNotification>>(sp => handler);
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new NotificationHandlerWrapper<TestNotification>();
        var notification = new TestNotification("test");

        // Act
        await wrapper.HandleAsync(notification, serviceProvider, CancellationToken.None);

        // Assert
        Assert.Single(handler.HandledMessages);
        Assert.Equal("Handler1: test", handler.HandledMessages[0]);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleHandlers_CallsAllHandlers()
    {
        // Arrange
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();
        var services = new ServiceCollection();
        services.AddScoped<INotificationHandler<TestNotification>>(sp => handler1);
        services.AddScoped<INotificationHandler<TestNotification>>(sp => handler2);
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new NotificationHandlerWrapper<TestNotification>();
        var notification = new TestNotification("test");

        // Act
        await wrapper.HandleAsync(notification, serviceProvider, CancellationToken.None);

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Single(handler2.HandledMessages);
    }

    [Fact]
    public async Task HandleAsync_WithNoHandlers_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new NotificationHandlerWrapper<TestNotification>();
        var notification = new TestNotification("test");

        // Act & Assert - Should not throw
        await wrapper.HandleAsync(notification, serviceProvider, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_ExecutesHandlersSequentially()
    {
        // Arrange
        var executionOrder = new List<int>();
        var handler1 = new TestNotificationHandler1();
        var handler2 = new TestNotificationHandler2();

        var services = new ServiceCollection();
        services.AddScoped<INotificationHandler<TestNotification>>(sp =>
        {
            executionOrder.Add(1);
            return handler1;
        });
        services.AddScoped<INotificationHandler<TestNotification>>(sp =>
        {
            executionOrder.Add(2);
            return handler2;
        });
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new NotificationHandlerWrapper<TestNotification>();
        var notification = new TestNotification("test");

        // Act
        await wrapper.HandleAsync(notification, serviceProvider, CancellationToken.None);

        // Assert
        Assert.Single(handler1.HandledMessages);
        Assert.Single(handler2.HandledMessages);
    }
}
