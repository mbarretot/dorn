namespace Dorn.Cli.Output;

/// <summary>
/// <c>outcome</c>: ok | tests-failed | no-test-tiers.
/// Per-tier <c>outcome</c>: passed | failed — always derived from the tier's process exit code.
/// Counts are null and <c>countsAvailable</c> is false when that tier's TRX report was
/// missing, stale, or malformed; a reporting gap NEVER changes the exit code or the tier's
/// <c>outcome</c>. Unavailable tier names are listed in <c>reportUnavailableTiers</c>.
/// Top-level totals sum only tiers with available counts, so they are partial whenever
/// <c>reportUnavailableTiers</c> is non-empty, and null when no tier reported counts.
/// <c>tierFilterRecognized</c> is null when no <c>--tier</c> was supplied, true when it
/// matched a known alias, and false when it did not (all tiers still ran in that case).
/// </summary>
public sealed record TestReport(
    string Outcome,
    string? TierFilter,
    bool? TierFilterRecognized,
    int? TotalTests,
    int? PassedTests,
    int? FailedTests,
    int? SkippedTests,
    double? DurationSeconds,
    IReadOnlyList<string> ReportUnavailableTiers,
    IReadOnlyList<TestTierDto> Tiers
);

public sealed record TestTierDto(
    string Tier,
    string Outcome,
    bool CountsAvailable,
    int? Total,
    int? Passed,
    int? Failed,
    int? Skipped,
    double? DurationSeconds
);
