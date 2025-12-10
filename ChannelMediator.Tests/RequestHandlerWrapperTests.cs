using ChannelMediator.Tests.Helpers;

namespace ChannelMediator.Tests;

public class RequestHandlerWrapperTests
{
    [Fact]
    public async Task HandleAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var request = new TestRequest("test");

        // Act
        var response = await wrapper.HandleAsync(request, CancellationToken.None);

        // Assert
        response.Should().NotBeNull();
        response.Should().BeOfType<TestResponse>();
        ((TestResponse)response).Result.Should().Be("Handled: test");
    }

    [Fact]
    public void RequestType_ReturnsCorrectType()
    {
        // Arrange
        var serviceProvider = Mock.Of<IServiceProvider>();
        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);

        // Act
        var requestType = wrapper.RequestType;

        // Assert
        requestType.Should().Be(typeof(TestRequest));
    }

    [Fact]
    public async Task HandleAsync_WithPipelineBehavior_ExecutesBehavior()
    {
        // Arrange
        var loggingBehavior = new LoggingBehavior<TestRequest, TestResponse>();
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => loggingBehavior);
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var request = new TestRequest("test");

        // Act
        await wrapper.HandleAsync(request, CancellationToken.None);

        // Assert
        loggingBehavior.Logs.Should().Contain("Before: TestRequest");
        loggingBehavior.Logs.Should().Contain("After: TestRequest");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleBehaviors_ExecutesInReverseOrder()
    {
        // Arrange
        var behavior1 = new LoggingBehavior<TestRequest, TestResponse>();
        var behavior2 = new ValidationBehavior<TestRequest, TestResponse>();

        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => behavior1);
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => behavior2);
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var request = new TestRequest("test");

        // Act
        await wrapper.HandleAsync(request, CancellationToken.None);

        // Assert - Behaviors execute in reverse order (last registered, first executed)
        behavior1.Logs.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var delayBehavior = new DelayBehavior<TestRequest, TestResponse>(1000);
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TestRequest, TestResponse>, TestRequestHandler>();
        services.AddScoped<IPipelineBehavior<TestRequest, TestResponse>>(sp => delayBehavior);
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<TestRequest, TestResponse>(serviceProvider);
        var request = new TestRequest("test");
        var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await wrapper.HandleAsync(request, cts.Token));
    }

    [Fact]
    public async Task HandleAsync_WithFailingHandler_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<FailingRequest, string>, FailingRequestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        var wrapper = new RequestHandlerWrapper<FailingRequest, string>(serviceProvider);
        var request = new FailingRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await wrapper.HandleAsync(request, CancellationToken.None));
        exception.Message.Should().Be("Handler failed");
    }
}
