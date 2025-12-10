namespace ChannelMediator.Tests.Helpers;

public record TestRequest(string Value) : IRequest<TestResponse>;

public record TestResponse(string Result);

public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
{
    public ValueTask<TestResponse> HandleAsync(TestRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new TestResponse($"Handled: {request.Value}"));
    }
}

public record AnotherTestRequest(int Number) : IRequest<int>;

public class AnotherTestRequestHandler : IRequestHandler<AnotherTestRequest, int>
{
    public ValueTask<int> HandleAsync(AnotherTestRequest request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(request.Number * 2);
    }
}

public record FailingRequest : IRequest<string>;

public class FailingRequestHandler : IRequestHandler<FailingRequest, string>
{
    public ValueTask<string> HandleAsync(FailingRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Handler failed");
    }
}

public record TestNotification(string Message) : INotification;

public class TestNotificationHandler1 : INotificationHandler<TestNotification>
{
    public List<string> HandledMessages { get; } = new();

    public ValueTask HandleAsync(TestNotification notification, CancellationToken cancellationToken)
    {
        HandledMessages.Add($"Handler1: {notification.Message}");
        return ValueTask.CompletedTask;
    }
}

public class TestNotificationHandler2 : INotificationHandler<TestNotification>
{
    public List<string> HandledMessages { get; } = new();

    public ValueTask HandleAsync(TestNotification notification, CancellationToken cancellationToken)
    {
        HandledMessages.Add($"Handler2: {notification.Message}");
        return ValueTask.CompletedTask;
    }
}

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public List<string> Logs { get; } = new();

    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Logs.Add($"Before: {request.GetType().Name}");
        var response = await next();
        Logs.Add($"After: {request.GetType().Name}");
        return response;
    }
}

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is TestRequest tr && string.IsNullOrEmpty(tr.Value))
        {
            throw new ArgumentException("Value cannot be empty");
        }
        return await next();
    }
}

public class DelayBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly int _delayMs;

    public DelayBehavior(int delayMs = 10)
    {
        _delayMs = delayMs;
    }

    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await Task.Delay(_delayMs, cancellationToken);
        return await next();
    }
}
