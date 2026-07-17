namespace Dorn.Cli.Testing;

/// <summary>
/// Captured process invocation captured by a test runner — file name, args, working directory.
/// </summary>
public sealed record CapturedProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory
);

/// <summary>
/// Outcome of running one or more tier tests via <see cref="DotnetTestRunner"/>.
/// </summary>
public sealed record TestRunResult(IReadOnlyList<CapturedProcessSpec> Specs, bool AllSucceeded);
