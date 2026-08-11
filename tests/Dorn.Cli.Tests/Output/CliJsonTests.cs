using System.Text.Json;
using Dorn.Cli.Output;
using Xunit;

namespace Dorn.Cli.Tests.Output;

public class CliJsonTests
{
    [Fact]
    public void Serialize_TrivialEnvelope_WritesSingleLineCompactJson()
    {
        var envelope = new CliEnvelope<string>(1, "doctor", true, 0, "hello world");

        var json = CliJson.Serialize(envelope);

        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain("  ", json);
        Assert.Equal(
            "{\"schemaVersion\":1,\"command\":\"doctor\",\"success\":true,\"exitCode\":0,\"data\":\"hello world\"}",
            json
        );
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsToEquivalentEnvelope()
    {
        var envelope = new CliEnvelope<int>(1, "coverage", false, 1, 42);

        var json = CliJson.Serialize(envelope);
        var roundTripped = JsonSerializer.Deserialize<CliEnvelope<int>>(json, CliJson.Options);

        Assert.Equal(envelope, roundTripped);
    }
}
