using System.ComponentModel;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.New;

public sealed class NewBlazorServerSettings : CommandSettings
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

    [CommandOption("-t|--theme")]
    [Description(
        "Boot-default theme: slate (default), rose, neutral, linear, primer, or lightning. Prompted interactively if omitted and the session is interactive."
    )]
    public string? Theme { get; init; }

    [CommandOption("--no-playground")]
    [Description(
        "Exclude the /playground showcase pages, producing a lean project. Included by default."
    )]
    public bool NoPlayground { get; init; }

    [CommandOption("--no-restore")]
    [Description(
        "Skip the automatic `dotnet tool restore` after generation. By default, dorn restores local tools (e.g. dorn.cli) so `dotnet dorn <verb>` works immediately."
    )]
    public bool NoRestore { get; init; }
}
