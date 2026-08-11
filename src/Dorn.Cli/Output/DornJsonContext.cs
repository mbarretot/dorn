using System.Text.Json.Serialization;

namespace Dorn.Cli.Output;

/// <summary>
/// Source-generated metadata for envelope payload types. Commands register their concrete
/// <c>CliEnvelope&lt;TData&gt;</c> closed type here as JSON support lands; anything not yet
/// registered falls back to reflection via <see cref="CliJson.Options"/>. <c>string</c>/<c>int</c>
/// cover the envelope-shape round-trip test until Doctor/Coverage DTOs land.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false
)]
[JsonSerializable(typeof(CliEnvelope<string>))]
[JsonSerializable(typeof(CliEnvelope<int>))]
internal partial class DornJsonContext : JsonSerializerContext { }
