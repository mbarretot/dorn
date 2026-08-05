namespace Dorn.Cli.Testing;

public sealed record CapturedProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory
);

/// <summary>
/// Outcome of running one or more tier tests via <see cref="DotnetTestRunner"/>.
/// </summary>
public sealed record TestRunResult(IReadOnlyList<CapturedProcessSpec> Specs, bool AllSucceeded);
