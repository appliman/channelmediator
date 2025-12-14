using ChannelMediator;

namespace ChannelMediatorSampleConsole;

// Command without return value (inherits from IRequest instead of IRequest<T>)
public record LogOrderCommand(string OrderId, decimal Amount) : IRequest;

public class LogOrderCommandHandler : IRequestHandler<LogOrderCommand>
{
	public ValueTask HandleAsync(LogOrderCommand command, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[LogOrderCommandHandler] Logging order {command.OrderId} with amount {command.Amount:C}");
		return ValueTask.CompletedTask;
	}

	public async Task Handle(LogOrderCommand command, CancellationToken cancellationToken)
	{
		await HandleAsync(command, cancellationToken).ConfigureAwait(false);
	}
}

public record SendEmailCommand(string To, string Subject, string Body) : IRequest;

public class SendEmailCommandHandler : IRequestHandler<SendEmailCommand>
{
	public async ValueTask HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[SendEmailCommandHandler] Sending email to {command.To}");
		Console.WriteLine($"  Subject: {command.Subject}");
		Console.WriteLine($"  Body: {command.Body}");
		await Task.Delay(100, cancellationToken); // Simulate email sending
		Console.WriteLine($"[SendEmailCommandHandler] Email sent successfully!");
	}

	public async Task Handle(SendEmailCommand command, CancellationToken cancellationToken)
	{
		await HandleAsync(command, cancellationToken).ConfigureAwait(false);
	}
}
