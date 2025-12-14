using ChannelMediator;

namespace ChannelMediatorConsole;

/// <summary>
/// Request to process a complete order (orchestrates multiple operations)
/// </summary>
public record ProcessOrderRequest(string OrderId, string ProductCode, string CustomerEmail) 
    : IRequest<OrderResult>;
