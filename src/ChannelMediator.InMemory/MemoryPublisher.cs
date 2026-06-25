using Microsoft.Extensions.Logging;

namespace ChannelMediator.InMemory;

internal sealed class MemoryPublisher(
	IMediator mediator,
	ILogger<MemoryPublisher> logger) : IMemoryPublisher
{
	public async Task Notify<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		ArgumentNullException.ThrowIfNull(notification);

		await mediator.Publish(notification, cancellationToken);
		logger.LogDebug("Memory publisher processed notification {NotificationType}.", typeof(TNotification).Name);
	}

	public async Task EnqueueRequest<R>(R request, CancellationToken cancellationToken = default)
		where R : IRequest
	{
		ArgumentNullException.ThrowIfNull(request);

		await mediator.Send(request, cancellationToken);
		logger.LogDebug("Memory publisher processed request {RequestType}.", request.GetType().Name);
	}
}
