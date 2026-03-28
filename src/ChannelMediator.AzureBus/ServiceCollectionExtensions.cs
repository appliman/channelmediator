using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChannelMediator.AzureBus;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Service Bus integration using Azure Service Bus options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure Azure Service Bus options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static AzureServiceBusOptions UseChannelMediatorAzureBus(
		this ChannelMediatorConfiguration configuration,
		Action<AzureServiceBusOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);

		configuration.Services.TryAddEnumerable(
			// IMPORTANT: Register GlobalInitializerHostedService before any TopicSubscriptionReadersHostedService registrations.
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, GlobalInitializerHostedService>());

		AzureServiceBusOptions options = default!;

		var optionsDescriptor = configuration.Services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(AzureServiceBusOptions));
		if (optionsDescriptor is null)
		{
			options = new AzureServiceBusOptions();
			options.Services = configuration.Services;
			configuration.Services.TryAddSingleton(options);
		}
		else
		{
			options = (AzureServiceBusOptions)optionsDescriptor.ImplementationInstance!;
		}

		configure.Invoke(options);

		if (options.ProcessMode == AzureServiceBusMode.Mock)
		{
			configuration.Services.TryAddSingleton<IAzurePublisher, MockAzurePublisher>();
		}

		if (options.ProcessMode == AzureServiceBusMode.Live)
		{
			configuration.Services.TryAddSingleton(sp =>
			{
				var opts = sp.GetRequiredService<AzureServiceBusOptions>();
				return new ServiceBusClient(opts.ConnectionString);
			});

			configuration.Services.TryAddSingleton(sp =>
			{
				var opts = sp.GetRequiredService<AzureServiceBusOptions>();
				return new ServiceBusAdministrationClient(opts.ConnectionString);
			});

			configuration.Services.TryAddSingleton<AzureServiceBusEntityManager>();
			configuration.Services.TryAddSingleton<IAzurePublisher, AzureServiceBusPublisher>();
		}

		return options;
	}
}
