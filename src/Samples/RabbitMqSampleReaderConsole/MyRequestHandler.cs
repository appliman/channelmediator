using ChannelMediator;

using ChannelMediatorSampleShared;

namespace RabbitMqSampleReaderConsole;

internal sealed class MyRequestHandler : IRequestHandler<MyRequest>
{
    public Task Handle(MyRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[REQUEST] Received: {request.Message}");
        return Task.CompletedTask;
    }
}
