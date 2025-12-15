using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChannelMediator.AzureBus;

internal sealed class QueueReaderRegistry
{
    private static readonly List<QueueReaderOptions> _registeredOptions = new();

    public static void Register(QueueReaderOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        lock (_registeredOptions)
        {
            if (_registeredOptions.Any(o =>
                o.QueueName.Equals(options.QueueName, StringComparison.OrdinalIgnoreCase) &&
                o.RequestType == options.RequestType))
            {
                throw new InvalidOperationException($"A QueueReader for queue '{options.QueueName}' and request type '{options.RequestType.FullName}' is already registered.");
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
