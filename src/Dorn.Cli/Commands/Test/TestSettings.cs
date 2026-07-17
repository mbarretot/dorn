using System.ComponentModel;
using Dorn.Cli.Commands.Shared;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Test;

/// <summary>
/// Settings for the <c>dorn test</c> command.
/// </summary>
public sealed class TestSettings : ProjectCommandSettings
{
    /// <summary>
    /// Optional tier filter. <c>unit</c>, <c>integration</c>, <c>architecture</c>,
    /// or <c>functional</c>. When omitted, all discovered tiers run.
    /// </summary>
    [CommandOption("-t|--tier")]
    [Description(
        "Run a single tier: unit, integration, architecture, or functional. Default: all tiers."
    )]
    public string? Tier { get; init; }
}
