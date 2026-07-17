using Dorn.Cli.Projects;

namespace Dorn.Cli.Testing;

/// <summary>
/// Runs <c>dotnet test</c> against one or more test tiers of a generated webapi project.
/// Pure orchestration: it does not parse test output, it only invokes the process and
/// tracks exit codes per tier.
/// </summary>
public interface IDotnetTestRunner
{
    /// <summary>
    /// Runs <c>dotnet test</c> against the given tiers in the target project.
    /// </summary>
    /// <param name="context">The resolved project context.</param>
    /// <param name="database">Database provider — controls the Docker preflight warning.</param>
    /// <param name="tiers">Tiers to run. Empty list = IncludeTests=false scenario.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TestRunResult> RunAsync(
        ProjectContext context,
        DatabaseProvider database,
        IReadOnlyList<TestTier> tiers,
        CancellationToken ct
    );
}
