namespace Dorn.Cli.Execution;

/// <summary>
/// Runs external processes such as <c>dotnet test</c> or <c>docker compose</c>.
/// Abstraction layer that allows commands to remain testable via NSubstitute mocks.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the process described by <paramref name="spec"/> and returns the exit code.
    /// </summary>
    /// <param name="spec">The process specification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The process exit code.</returns>
    Task<int> RunAsync(ProcessSpec spec, CancellationToken ct);

    /// <summary>
    /// Runs <paramref name="spec"/> and returns exit code plus fully-drained stdout/stderr.
    /// Intended for short-lived, bounded-output probes (e.g. <c>dotnet --version</c>).
    /// Do NOT use for long-running processes that spawn grandchildren (run/test/compose):
    /// those must keep using <see cref="RunAsync"/>, which never awaits the output streams.
    /// </summary>
    /// <param name="spec">The process specification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The captured exit code, stdout, and stderr.</returns>
    Task<ProcessResult> RunCapturedAsync(ProcessSpec spec, CancellationToken ct);
}
