using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using RabbitMQ.Client;

namespace ChannelMediator.RabbitMQ;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ integration to ChannelMediator.
    /// </summary>
    /// <param name="configuration">The mediator configuration.</param>
    /// <param name="configure">Action to configure RabbitMQ options.</param>
    /// <returns>The RabbitMQ options for chaining.</returns>
    public static RabbitMqOptions UseChannelMediatorRabbitMQ(
        this ChannelMediatorConfiguration configuration,
        Action<RabbitMqOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        configuration.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, GlobalInitializerHostedService>());

        RabbitMqOptions options = default!;

        var optionsDescriptor = configuration.Services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(RabbitMqOptions));
        if (optionsDescriptor is null)
        {
            options = new RabbitMqOptions();
            options.Services = configuration.Services;
            configuration.Services.TryAddSingleton(options);
        }
        else
        {
            options = (RabbitMqOptions)optionsDescriptor.ImplementationInstance!;
        }

        configure.Invoke(options);

        if (options.ProcessMode == RabbitMqMode.Mock)
        {
            configuration.Services.TryAddSingleton<IRabbitMqPublisher, MockRabbitMqPublisher>();
        }

        if (options.ProcessMode == RabbitMqMode.Live)
        {
            configuration.Services.TryAddSingleton<IConnection>(sp =>
            {
                var opts = sp.GetRequiredService<RabbitMqOptions>();
                var factory = new ConnectionFactory
                {
                    HostName = opts.HostName,
                    Port = opts.Port,
                    UserName = opts.UserName,
                    Password = opts.Password,
                    VirtualHost = opts.VirtualHost
                };

                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });

            configuration.Services.TryAddSingleton<RabbitMqEntityManager>();
            configuration.Services.TryAddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        }

        return options;
    }
}
