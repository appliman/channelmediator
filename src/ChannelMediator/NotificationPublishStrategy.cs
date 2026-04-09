namespace ChannelMediator;

/// <summary>
/// Defines how notification handlers are invoked.
/// </summary>
public enum NotificationPublishStrategy
{
 /// <summary>
	/// Invokes notification handlers one after another.
	/// </summary>
	Sequential,

	/// <summary>
	/// Invokes notification handlers concurrently.
	/// </summary>
	Parallel
}