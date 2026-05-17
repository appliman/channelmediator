using System.Diagnostics;

namespace ChannelMediator.RabbitMQ;

internal sealed class QueueReaderRegistry
{
    private static readonly List<QueueReaderOptions> _registeredOptions = new();

    public static void Register(QueueReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_registeredOptions)
        {
            var existingOption = _registeredOptions.FirstOrDefault(o =>
                o.QueueName.Equals(options.QueueName, StringComparison.OrdinalIgnoreCase) &&
                o.RequestType == options.RequestType);

            if (existingOption is not null)
            {
                Trace.TraceWarning($"A QueueReader for queue '{options.QueueName}' and request type '{options.RequestType.FullName}' is already registered.");
                return;
            }

            _registeredOptions.Add(options);
        }
    }

    public static IReadOnlyList<QueueReaderOptions> GetRegisteredOptions()
    {
        lock (_registeredOptions)
        {
            return _registeredOptions.ToList();
        }
    }
}
