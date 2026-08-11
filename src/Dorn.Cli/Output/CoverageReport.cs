namespace Dorn.Cli.Output;

/// <summary>
/// <c>outcome</c>: ok | below-threshold | no-test-tiers | test-run-failed | no-report.
/// <c>lineRate</c> is a 0-1 fraction (not a percentage); <c>classes</c> always lists every class — <c>--all</c> only affects the table.
/// </summary>
public sealed record CoverageReport(
    string Outcome,
    double? LineRate,
    int? CoveredLines,
    int? TotalLines,
    double Threshold,
    bool ThresholdPassed,
    IReadOnlyList<string> MissingTierDirs,
    IReadOnlyList<CoverageClassDto> Classes
);

public sealed record CoverageClassDto(
    string Assembly,
    string Class,
    string File,
    int CoveredLines,
    int TotalLines
);
