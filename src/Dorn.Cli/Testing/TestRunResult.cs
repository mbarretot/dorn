using Dorn.Cli.Projects;

namespace Dorn.Cli.Testing;

public sealed record CapturedProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory
);

/// <summary>
/// Per-tier outcome. <c>Succeeded</c> always reflects the tier's process exit code; the count
/// fields plus <c>DurationSeconds</c> are null when that tier's TRX report was missing, stale,
/// or malformed.
/// </summary>
public sealed record TierRunResult(
    TestTier Tier,
    bool Succeeded,
    int? Total,
    int? Passed,
    int? Failed,
    int? Skipped,
    double? DurationSeconds
);

/// <summary>
/// Outcome of running one or more tier tests via <see cref="DotnetTestRunner"/>.
/// </summary>
public sealed record TestRunResult(
    IReadOnlyList<CapturedProcessSpec> Specs,
    bool AllSucceeded,
    IReadOnlyList<TierRunResult> TierResults
);
