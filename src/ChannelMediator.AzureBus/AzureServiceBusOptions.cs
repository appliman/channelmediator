namespace ChannelMediator.AzureBus;

/// <summary>
/// Configuration options for Azure Service Bus integration.
/// </summary>
public sealed class AzureServiceBusOptions
{
    internal AzureServiceBusOptions()
    {
    }

    public ChannelMediatorConfiguration ChannelMediatorConfiguration { get; set; } = default!;

	/// <summary>
	/// Gets or sets the Azure Service Bus connection string.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prefix for queue and topic names.
    /// </summary>
    public string Prefix { get; set; } = null!;

    /// <summary>
    /// Gets or sets the name of the subscription associated with this instance.
    /// </summary>
    public string SubscriptionName { get; set; } = System.Environment.MachineName.ToLower();

	/// <summary>
	/// Gets or sets the maximum number of concurrent calls to the message handler.
	/// Default is 1.
	/// </summary>
	public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether to auto-complete messages after processing.
    /// Default is true.
    /// </summary>
    public bool AutoCompleteMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum duration within which the lock will be renewed automatically.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(5);
}
