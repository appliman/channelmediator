namespace ChannelMediator.AzureBus;

/// <summary>
/// Defines the execution mode used by the Azure Service Bus integration.
/// </summary>
public enum AzureServiceBusMode
{
   /// <summary>
	/// Uses live Azure Service Bus resources.
	/// </summary>
	Live,

	/// <summary>
	/// Uses the in-memory mock publisher instead of Azure Service Bus.
	/// </summary>
	Mock
}
