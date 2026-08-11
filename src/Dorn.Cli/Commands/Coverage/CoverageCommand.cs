using Dorn.Cli.Output;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using Spectre.Console;
using Spectre.Console.Cli;
using CoverageDomain = Dorn.Cli.Coverage;

namespace Dorn.Cli.Commands.Coverage;

/// <summary><c>dorn coverage</c> — runs all tiers, merges each tier's freshest Cobertura report, gates at 80%, and prints a per-class table.</summary>
public sealed class CoverageCommand : AsyncCommand<CoverageSettings>
{
    private const int MaxRows = 15;

    private readonly IProjectContextResolver _resolver;
    private readonly IDotnetTestRunner _testRunner;
    private readonly CoverageDomain.CoverageReporter _reporter;
    private readonly IAnsiConsole _console;
    private readonly IDornTheme _theme;
    private readonly ICliOutputWriter _writer;

    public CoverageCommand(
        IProjectContextResolver resolver,
        IDotnetTestRunner testRunner,
        CoverageDomain.CoverageReporter reporter,
        IAnsiConsole console,
        IDornTheme theme,
        ICliOutputWriter writer
    )
    {
        _resolver = resolver;
        _testRunner = testRunner;
        _reporter = reporter;
        _console = console;
        _theme = theme;
        _writer = writer;
    }

    // Spectre.Console.Cli 0.55.0 changed ExecuteAsync from public to protected (and added
    // CancellationToken); logic lives in the public RunAsync below, and tests call it
    // directly (CommandAppTester was removed in 0.55.0).
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        CoverageSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the coverage command logic. Public so unit tests can drive the command
    /// directly without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(CoverageSettings settings, CancellationToken cancellationToken)
    {
        var formatResult = OutputFormatValidator.Validate(settings.Format);
        if (!formatResult.IsValid)
        {
            _theme.Message(Severity.Error, Markup.Escape(formatResult.ErrorMessage!));
            return 1;
        }

        var format = formatResult.Format;
        var result = await ComputeAsync(settings, format, cancellationToken);
        var exitCode = result.Outcome == CoverageOutcome.Ok ? 0 : 1;

        if (format == OutputFormat.Json)
        {
            EmitJson(result, exitCode);
        }
        else
        {
            RenderTable(result, settings.All);
        }

        return exitCode;
    }

    /// <summary>Runs the full tier→discovery→merge→threshold pipeline with zero rendering calls, so both the table and JSON branches derive their exit code from one computed outcome.</summary>
    private async Task<CoverageRunResult> ComputeAsync(
        CoverageSettings settings,
        OutputFormat format,
        CancellationToken cancellationToken
    )
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        if (projectContext.Tiers.Count == 0)
        {
            return new CoverageRunResult(CoverageOutcome.NoTestTiers, null, null, []);
        }

        // -2s slack covers coarse-granularity filesystems around freshly written reports.
        var runStartedUtc = DateTime.UtcNow.AddSeconds(-2);

        // JSON mode must never let the progress region touch stdout ahead of the envelope.
        var testResult = await _testRunner.RunAsync(
            projectContext,
            DatabaseProvider.Sqlite,
            projectContext.Tiers,
            cancellationToken,
            suppressLiveOutput: format == OutputFormat.Json
        );

        if (!testResult.AllSucceeded)
        {
            return new CoverageRunResult(CoverageOutcome.TestRunFailed, null, null, []);
        }

        var testResultsRoot = Path.Combine(projectContext.Root, "TestResults");
        var discovery = FindCoberturaReports(testResultsRoot, runStartedUtc);

        if (discovery.ReportPaths.Count == 0)
        {
            return new CoverageRunResult(CoverageOutcome.NoReport, null, null, []);
        }

        var expectedTierDirs = ExtractExpectedTierDirs(testResult.Specs);
        var missingTierDirs = expectedTierDirs.Except(discovery.CoveredTierDirs).ToList();

        var summary = _reporter.MergeCobertura(discovery.ReportPaths);
        var decision = _reporter.EvaluateThreshold(summary.LineRate);

        var outcome = decision.Passed ? CoverageOutcome.Ok : CoverageOutcome.BelowThreshold;
        return new CoverageRunResult(outcome, summary, decision, missingTierDirs);
    }

    private void RenderTable(CoverageRunResult result, bool all)
    {
        switch (result.Outcome)
        {
            case CoverageOutcome.NoTestTiers:
                _theme.Message(
                    Severity.Warning,
                    "No test tiers found. This project was generated with [bold]IncludeTests=false[/]; nothing to measure."
                );
                return;
            case CoverageOutcome.TestRunFailed:
                _theme.Message(
                    Severity.Error,
                    "One or more tier runs failed; coverage report not generated."
                );
                return;
            case CoverageOutcome.NoReport:
                _theme.Message(
                    Severity.Error,
                    "No coverage report found. Expected at TestResults/**/coverage.cobertura.xml."
                );
                return;
        }

        if (result.MissingTierDirs.Count > 0)
        {
            _theme.Message(
                Severity.Warning,
                $"No fresh coverage report for: {string.Join(", ", result.MissingTierDirs)}."
            );
        }

        var summary = result.Summary!;
        var decision = result.Threshold!;
        RenderClassTable(summary, all);

        _theme.Message(
            Severity.Info,
            $"Line coverage: {decision.Percentage:F2}% (threshold: {CoverageDomain.CoverageReporter.Threshold * 100:F0}%)"
        );

        if (result.Outcome == CoverageOutcome.BelowThreshold)
        {
            _theme.Message(
                Severity.Error,
                $"Below threshold by {(CoverageDomain.CoverageReporter.Threshold * 100 - decision.Percentage):F2} percentage points."
            );
            return;
        }

        _theme.Message(Severity.Success, "Threshold met.");
    }

    private void EmitJson(CoverageRunResult result, int exitCode)
    {
        var report = new CoverageReport(
            Outcome: OutcomeToken(result.Outcome),
            LineRate: result.Summary is not null ? Math.Round(result.Summary.LineRate, 6) : null,
            CoveredLines: result.Summary?.CoveredLines,
            TotalLines: result.Summary?.TotalLines,
            Threshold: CoverageDomain.CoverageReporter.Threshold,
            ThresholdPassed: result.Threshold?.Passed ?? false,
            MissingTierDirs: result.MissingTierDirs,
            Classes: (result.Summary?.Classes ?? [])
                .Select(c => new CoverageClassDto(
                    c.Assembly,
                    c.Class,
                    c.File,
                    c.CoveredLines,
                    c.TotalLines
                ))
                .ToList()
        );
        var envelope = new CliEnvelope<CoverageReport>(
            SchemaVersion: 1,
            Command: "coverage",
            Success: exitCode == 0,
            ExitCode: exitCode,
            Data: report
        );
        _writer.WriteLine(CliJson.Serialize(envelope));
    }

    private static string OutcomeToken(CoverageOutcome outcome) =>
        outcome switch
        {
            CoverageOutcome.NoTestTiers => "no-test-tiers",
            CoverageOutcome.TestRunFailed => "test-run-failed",
            CoverageOutcome.NoReport => "no-report",
            CoverageOutcome.BelowThreshold => "below-threshold",
            CoverageOutcome.Ok => "ok",
            _ => outcome.ToString().ToLowerInvariant(),
        };

    /// <summary>Groups by <c>TestResults/&lt;tier&gt;</c> immediate child dir, keeps the newest report per group, and drops any older than <paramref name="runStartedUtc"/>.</summary>
    private static CoverageDiscovery FindCoberturaReports(
        string testResultsRoot,
        DateTime runStartedUtc
    )
    {
        if (!Directory.Exists(testResultsRoot))
            return new CoverageDiscovery([], []);

        var reportPaths = new List<string>();
        var coveredTierDirs = new List<string>();

        foreach (var tierDir in Directory.EnumerateDirectories(testResultsRoot))
        {
            var newest = Directory
                .EnumerateFiles(
                    tierDir,
                    "coverage.cobertura.xml",
                    new EnumerationOptions { RecurseSubdirectories = true }
                )
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (newest is null || File.GetLastWriteTimeUtc(newest) < runStartedUtc)
                continue;

            reportPaths.Add(newest);
            coveredTierDirs.Add(Path.GetFileName(tierDir));
        }

        return new CoverageDiscovery(reportPaths, coveredTierDirs);
    }

    /// <summary>Reads the tier result directory names dotnet test was invoked with, from each captured <c>--results-directory</c> argument.</summary>
    private static IReadOnlyList<string> ExtractExpectedTierDirs(
        IReadOnlyList<CapturedProcessSpec> specs
    )
    {
        var tierDirs = new List<string>();
        foreach (var spec in specs)
        {
            for (var i = 0; i < spec.Arguments.Count - 1; i++)
            {
                if (spec.Arguments[i] == "--results-directory")
                {
                    tierDirs.Add(Path.GetFileName(spec.Arguments[i + 1]));
                    break;
                }
            }
        }

        return tierDirs;
    }

    private void RenderClassTable(CoverageDomain.CoverageSummary summary, bool all)
    {
        if (summary.Classes.Count == 0)
            return;

        var rows = all
            ? summary.Classes
            : summary
                .Classes.Where(c => c.LineRate < CoverageDomain.CoverageReporter.Threshold)
                .ToList();

        if (rows.Count == 0)
        {
            _theme.Message(Severity.Success, "All classes at or above 80%.");
            return;
        }

        var table = _theme.CreateTable("Coverage by class");
        table.AddColumn("Assembly");
        table.AddColumn("Class");
        table.AddColumn("Coverage %");
        table.AddColumn("Covered/Total");
        table.AddColumn("Uncovered");

        var displayed = all ? rows : rows.Take(MaxRows).ToList();
        foreach (var c in displayed)
        {
            table.AddRow(
                Markup.Escape(c.Assembly),
                Markup.Escape(DisplayClassName(c)),
                $"{c.LineRate * 100:F2}%",
                $"{c.CoveredLines}/{c.TotalLines}",
                (c.TotalLines - c.CoveredLines).ToString()
            );
        }

        if (!all && rows.Count > MaxRows)
        {
            table.Caption($"+{rows.Count - MaxRows} more below threshold — run with --all");
        }

        _console.Write(table);
    }

    private static string DisplayClassName(CoverageDomain.ClassCoverage c) =>
        c.Class.StartsWith(c.Assembly + ".", StringComparison.Ordinal)
            ? c.Class[(c.Assembly.Length + 1)..]
            : c.Class;

    private sealed record CoverageDiscovery(
        IReadOnlyList<string> ReportPaths,
        IReadOnlyList<string> CoveredTierDirs
    );

    /// <summary>Every early-return branch the pre-JSON command had, collapsed into one computed result.</summary>
    private enum CoverageOutcome
    {
        NoTestTiers,
        TestRunFailed,
        NoReport,
        BelowThreshold,
        Ok,
    }

    private sealed record CoverageRunResult(
        CoverageOutcome Outcome,
        CoverageDomain.CoverageSummary? Summary,
        CoverageDomain.ThresholdDecision? Threshold,
        IReadOnlyList<string> MissingTierDirs
    );
}
