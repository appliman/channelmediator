using Microsoft.Extensions.Logging;

namespace ChannelMediator.AzureBus;

internal class MockAzurePublisher(
	IMediator mediator,
	ILogger<MockAzurePublisher> logger
	) : IAzurePublisher
{
	public async Task Notify<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
		where TNotification : INotification
	{
		ArgumentNullException.ThrowIfNull(notification);
		await mediator.Publish(notification, cancellationToken);
		logger.LogDebug("Mock Azure publisher processed notification {NotificationType}.", typeof(TNotification).Name);
	}

	public async Task EnqueueRequest<R>(R request, CancellationToken cancellationToken = default)
		where R : IRequest
	{
		ArgumentNullException.ThrowIfNull(request);
		await mediator.Send(request, cancellationToken);
		logger.LogDebug("Mock Azure publisher processed request {RequestType}.", request.GetType().Name);
	}
}
