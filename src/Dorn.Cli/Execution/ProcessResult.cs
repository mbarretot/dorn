namespace Dorn.Cli.Execution;

/// <summary>
/// The captured outcome of a short-lived, bounded-output process run via
/// <see cref="IProcessRunner.RunCapturedAsync"/>.
/// </summary>
/// <param name="ExitCode">Process exit code; 127 when the executable was not found.</param>
/// <param name="StandardOutput">Fully-drained standard output.</param>
/// <param name="StandardError">Fully-drained standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
