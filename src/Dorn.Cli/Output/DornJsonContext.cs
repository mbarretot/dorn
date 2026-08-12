using System.Text.Json.Serialization;

namespace Dorn.Cli.Output;

/// <summary>
/// Source-generated metadata for envelope payload types. Commands register their concrete
/// <c>CliEnvelope&lt;TData&gt;</c> closed type here as JSON support lands; anything not yet
/// registered falls back to reflection via <see cref="CliJson.Options"/>. <c>string</c>/<c>int</c>
/// cover the envelope-shape round-trip test.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false
)]
[JsonSerializable(typeof(CliEnvelope<string>))]
[JsonSerializable(typeof(CliEnvelope<int>))]
[JsonSerializable(typeof(CliEnvelope<DoctorReport>))]
[JsonSerializable(typeof(CliEnvelope<CoverageReport>))]
[JsonSerializable(typeof(CliEnvelope<TestReport>))]
internal partial class DornJsonContext : JsonSerializerContext { }
