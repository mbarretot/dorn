using System.ComponentModel;
using Dorn.Cli.Commands.Shared;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Test;

public sealed class TestSettings : ProjectCommandSettings
{
    [CommandOption("-t|--tier")]
    [Description(
        "Run a single tier: unit, integration, architecture, or functional. Default: all tiers."
    )]
    public string? Tier { get; init; }
}
