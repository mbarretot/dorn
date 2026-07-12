using System.ComponentModel;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewWebApiSettings : CommandSettings
{
    [CommandArgument(0, "<name>")]
    [Description("Name of the project to generate.")]
    public required string Name { get; init; }

    [CommandOption("-o|--output")]
    [Description("Output directory for the generated project. Defaults to ./<name>.")]
    public string? Output { get; init; }

    [CommandOption("--force")]
    [Description("Overwrite existing files in the output directory.")]
    public bool Force { get; init; }
}
