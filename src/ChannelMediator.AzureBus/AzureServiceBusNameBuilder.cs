using System.Text;

namespace ChannelMediator.AzureBus;

internal static class AzureServiceBusNameBuilder
{
    private const string DefaultPrefix = "abmr";

    internal static string Build(string? prefix, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var combined = !string.IsNullOrWhiteSpace(prefix)
            ? $"{prefix}-{name}"
            : $"{DefaultPrefix}-{name}";

        return NormalizeName(combined);
    }

    private static string NormalizeName(string value)
    {
        var lower = value.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);
        var previous = '\0';

        foreach (var character in lower)
        {
            if (character == '.' && previous == '.')
            {
                continue;
            }

            builder.Append(character);
            previous = character;
        }

        var result = builder.ToString().Trim('.', '-');
        result = result.Replace(".-", "-");

		return result;
    }
}
