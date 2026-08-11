using System.ComponentModel;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Shared;

/// <summary>Adds the shared <c>--format</c> option on top of <see cref="ProjectCommandSettings"/>.</summary>
public abstract class FormattedCommandSettings : ProjectCommandSettings
{
    [CommandOption("--format <FORMAT>")]
    [Description("Output format: table (default) or json.")]
    public string? Format { get; init; }
}
