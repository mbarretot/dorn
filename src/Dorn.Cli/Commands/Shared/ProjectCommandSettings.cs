using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Shared;

/// <summary>
/// Abstract base settings for project-bound commands. Provides the shared
/// <c>--project</c> option that defaults to the current working directory.
/// </summary>
public abstract class ProjectCommandSettings : CommandSettings
{
    /// <summary>
    /// Path to the target generated project root. Defaults to the current working directory.
    /// </summary>
    [CommandOption("-p|--project")]
    public string? Project { get; init; }
}
