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

    [CommandOption("--orm")]
    [Description(
        "ORM: efcore (default) or dapper. Prompted interactively if omitted and the session is interactive."
    )]
    public string? Orm { get; init; }

    [CommandOption("--database")]
    [Description(
        "Database provider: sqlite (default) or sqlserver. Prompted interactively if omitted and the session is interactive."
    )]
    public string? Database { get; init; }

    [CommandOption("--orchestrator")]
    [Description(
        "Orchestrator: aspire (default) or docker-compose. Prompted interactively if omitted and the session is interactive."
    )]
    public string? Orchestrator { get; init; }

    [CommandOption("--no-restore")]
    [Description(
        "Skip the automatic `dotnet tool restore` after generation. By default, dorn restores local tools (e.g. dorn.cli) so `dotnet dorn <verb>` works immediately."
    )]
    public bool NoRestore { get; init; }
}
