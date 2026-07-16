namespace Dorn.Cli.Projects;

/// <summary>
/// Represents the orchestrator used to run the webapi project.
/// </summary>
public enum Orchestrator
{
    /// <summary>
    /// .NET Aspire AppHost orchestration.
    /// </summary>
    Aspire,

    /// <summary>
    /// Docker Compose orchestration.
    /// </summary>
    Compose,

    /// <summary>
    /// Plain dotnet run with no container orchestration.
    /// </summary>
    Plain,
}
