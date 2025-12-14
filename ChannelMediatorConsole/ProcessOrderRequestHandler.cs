using ChannelMediator;

namespace ChannelMediatorConsole;

public record OrderResult(string OrderId, CartItem Item, bool EmailSent, bool Logged);

/// <summary>
/// Handler that orchestrates multiple requests using IMediator injection
/// This demonstrates the Mediator pattern where one handler calls other handlers
/// </summary>
public class ProcessOrderRequestHandler : IRequestHandler<ProcessOrderRequest, OrderResult>
{
	private readonly IMediator _mediator;

	public ProcessOrderRequestHandler(IMediator mediator)
	{
		_mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
	}

	public async ValueTask<OrderResult> HandleAsync(ProcessOrderRequest request, CancellationToken cancellationToken)
	{
		Console.WriteLine($"[ProcessOrderRequestHandler] Starting order processing for {request.OrderId}");
		
		// Step 1: Add product to cart (calling another request handler)
		Console.WriteLine($"[ProcessOrderRequestHandler] Step 1: Adding product to cart...");
		var cartItem = await _mediator.Send(new AddToCartRequest(request.ProductCode), cancellationToken);
		Console.WriteLine($"[ProcessOrderRequestHandler] Product added: {cartItem.ProductCode} - {cartItem.Total:C}");

		// Step 2: Log the order (calling a command handler)
		Console.WriteLine($"[ProcessOrderRequestHandler] Step 2: Logging order...");
		await _mediator.Send(new LogOrderCommand(request.OrderId, cartItem.Total), cancellationToken);
		
		// Step 3: Send confirmation email (calling another command handler)
		Console.WriteLine($"[ProcessOrderRequestHandler] Step 3: Sending confirmation email...");
		var emailCommand = new SendEmailCommand(
			request.CustomerEmail,
			$"Order Confirmation - {request.OrderId}",
			$"Your order for {cartItem.ProductCode} has been processed. Total: {cartItem.Total:C}");
		await _mediator.Send(emailCommand, cancellationToken);

		// Step 4: Publish notification (optional - demonstrates event publishing)
		Console.WriteLine($"[ProcessOrderRequestHandler] Step 4: Publishing notification...");
		await _mediator.Publish(
			new ProductAddedNotification(cartItem.ProductCode, cartItem.Quantity, cartItem.Total), 
			cancellationToken);

		Console.WriteLine($"[ProcessOrderRequestHandler] Order processing completed for {request.OrderId}");
		
		return new OrderResult(request.OrderId, cartItem, EmailSent: true, Logged: true);
	}

	public async Task<OrderResult> Handle(ProcessOrderRequest request, CancellationToken cancellationToken)
	{
		return await HandleAsync(request, cancellationToken).ConfigureAwait(false);
	}
}
