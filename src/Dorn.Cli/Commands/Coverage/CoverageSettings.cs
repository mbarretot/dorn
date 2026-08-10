using System.ComponentModel;
using Dorn.Cli.Commands.Shared;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Coverage;

/// <summary>Coverage threshold is fixed (not configurable).</summary>
public sealed class CoverageSettings : ProjectCommandSettings
{
    [CommandOption("--all")]
    [Description("Show every class in the coverage table, ignoring the 80% filter and 15-row cap.")]
    public bool All { get; init; }
}
