namespace ChannelMediator;

/// <summary>
/// Factory interface for creating mediator instances.
/// Use this when you need to make nested mediator calls from within handlers
/// to avoid deadlocks with the singleton mediator's channel pump.
/// </summary>
public interface IMediatorFactory
{
	/// <summary>
	/// Creates a new mediator instance with its own channel and pump.
	/// The caller is responsible for disposing the returned instance.
	/// </summary>
	IMediator CreateMediator();
}
