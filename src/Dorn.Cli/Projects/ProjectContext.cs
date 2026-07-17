namespace Dorn.Cli.Projects;

/// <summary>
/// Captures everything a command needs to know about the target generated project:
/// project root, solution path, orchestrator, web API project, and available test tiers.
/// </summary>
/// <param name="Root">Absolute path to the project root directory.</param>
/// <param name="SolutionPath">Absolute path to the .slnx solution file, or empty if none found.</param>
/// <param name="Orchestrator">The orchestrator to use when running the project.</param>
/// <param name="WebApiProject">Absolute path to the WebApi project folder, or null if none found.</param>
/// <param name="Tiers">The set of test tiers discovered in the project.</param>
public sealed record ProjectContext(
    string Root,
    string SolutionPath,
    Orchestrator Orchestrator,
    string? WebApiProject,
    IReadOnlyList<TestTier> Tiers
);
