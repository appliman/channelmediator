namespace ChannelMediatorSampleShared;

public record OrderResult(string OrderId, CartItem Item, bool EmailSent, bool Logged);
