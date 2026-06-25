using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChannelMediator.InMemory;

/// <summary>
/// Provides dependency injection extensions for enabling ChannelMediator in-memory publishing.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds in-memory publishing to ChannelMediator.
	/// </summary>
	/// <param name="configuration">The ChannelMediator configuration being extended.</param>
	/// <param name="configure">Optional action to configure in-memory options.</param>
	/// <returns>The in-memory options for chaining.</returns>
	public static InMemoryOptions UseChannelMediatorInMemory(
		this ChannelMediatorConfiguration configuration,
		Action<InMemoryOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		configuration.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, GlobalInitializerHostedService>());

		InMemoryOptions options = default!;

		var optionsDescriptor = configuration.Services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(InMemoryOptions));
		if (optionsDescriptor is null)
		{
			options = new InMemoryOptions
			{
				Services = configuration.Services
			};
			configuration.Services.TryAddSingleton(options);
		}
		else
		{
			options = (InMemoryOptions)optionsDescriptor.ImplementationInstance!;
		}

		configure?.Invoke(options);

		configuration.Services.TryAddSingleton<IMemoryPublisher, MemoryPublisher>();

		return options;
	}
}
