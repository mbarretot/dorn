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
}
