namespace ChannelMediator.ApiGenerators.Abstraction;

/// <summary>
/// Specifies which <c>System.Text.Json.JsonSerializerOptions</c> preset to use
/// for JSON serialization and deserialization in generated API clients and Minimal API stream endpoints.
/// </summary>
/// <remarks>
/// Use the same value on both <see cref="ApiClientAttribute"/> and <see cref="MapApiExtensionAttribute"/>
/// so that the client and the server share identical serialization behaviour (e.g. enum representation).
/// </remarks>
public enum JsonSerializerOptionsPreset
{
    /// <summary>
    /// Uses <c>JsonSerializerOptions.Web</c>:
    /// enums serialized as strings, camelCase property names, case-insensitive deserialization.
    /// </summary>
    Web = 0,

    /// <summary>
    /// Uses the runtime default <c>JsonSerializerOptions</c>:
    /// enums serialized as integers, PascalCase property names.
    /// </summary>
    Default = 1
}
