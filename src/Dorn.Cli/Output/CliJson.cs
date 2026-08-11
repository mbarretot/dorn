using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Dorn.Cli.Output;

public static class CliJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            DornJsonContext.Default,
            new DefaultJsonTypeInfoResolver()
        ),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Serialize<TData>(CliEnvelope<TData> envelope) =>
        JsonSerializer.Serialize(envelope, Options);
}
