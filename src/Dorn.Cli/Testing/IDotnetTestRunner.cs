using Dorn.Cli.Projects;

namespace Dorn.Cli.Testing;

/// <summary>
/// Runs <c>dotnet test</c> for generated webapi test tiers. Exit codes are tracked per tier, and
/// each tier's TRX report is read for pass/fail counts when available.
/// </summary>
public interface IDotnetTestRunner
{
    /// <param name="context">The resolved project context.</param>
    /// <param name="database">Database provider — controls the Docker preflight warning.</param>
    /// <param name="tiers">Tiers to run. Empty list = IncludeTests=false scenario.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="suppressLiveOutput">
    /// When true, skips the Spectre progress region even if live regions are enabled — required
    /// by callers (e.g. JSON output) that must keep stdout free of interleaved render output.
    /// </param>
    Task<TestRunResult> RunAsync(
        ProjectContext context,
        DatabaseProvider database,
        IReadOnlyList<TestTier> tiers,
        CancellationToken ct,
        bool suppressLiveOutput = false
    );
}
