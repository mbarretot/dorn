using Dorn.Cli.Coverage;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Coverage;

/// <summary><c>dorn coverage</c> — runs all tiers, merges each tier's freshest Cobertura report, gates at 80%, and prints a per-class table.</summary>
public sealed class CoverageCommand : AsyncCommand<CoverageSettings>
{
    private const int MaxRows = 15;

    private readonly IProjectContextResolver _resolver;
    private readonly IDotnetTestRunner _testRunner;
    private readonly CoverageReporter _reporter;
    private readonly IAnsiConsole _console;
    private readonly IDornTheme _theme;

    public CoverageCommand(
        IProjectContextResolver resolver,
        IDotnetTestRunner testRunner,
        CoverageReporter reporter,
        IAnsiConsole console,
        IDornTheme theme
    )
    {
        _resolver = resolver;
        _testRunner = testRunner;
        _reporter = reporter;
        _console = console;
        _theme = theme;
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
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        if (projectContext.Tiers.Count == 0)
        {
            _theme.Message(
                Severity.Warning,
                "No test tiers found. This project was generated with [bold]IncludeTests=false[/]; nothing to measure."
            );
            return 1;
        }

        // -2s slack covers coarse-granularity filesystems around freshly written reports.
        var runStartedUtc = DateTime.UtcNow.AddSeconds(-2);

        var testResult = await _testRunner.RunAsync(
            projectContext,
            DatabaseProvider.Sqlite,
            projectContext.Tiers,
            cancellationToken
        );

        if (!testResult.AllSucceeded)
        {
            _theme.Message(
                Severity.Error,
                "One or more tier runs failed; coverage report not generated."
            );
            return 1;
        }

        var testResultsRoot = Path.Combine(projectContext.Root, "TestResults");
        var discovery = FindCoberturaReports(testResultsRoot, runStartedUtc);

        if (discovery.ReportPaths.Count == 0)
        {
            _theme.Message(
                Severity.Error,
                "No coverage report found. Expected at TestResults/**/coverage.cobertura.xml."
            );
            return 1;
        }

        var expectedTierDirs = ExtractExpectedTierDirs(testResult.Specs);
        var missingTierDirs = expectedTierDirs.Except(discovery.CoveredTierDirs).ToList();
        if (missingTierDirs.Count > 0)
        {
            _theme.Message(
                Severity.Warning,
                $"No fresh coverage report for: {string.Join(", ", missingTierDirs)}."
            );
        }

        var summary = _reporter.MergeCobertura(discovery.ReportPaths);
        RenderClassTable(summary, settings.All);

        var decision = _reporter.EvaluateThreshold(summary.LineRate);

        _theme.Message(
            Severity.Info,
            $"Line coverage: {decision.Percentage:F2}% (threshold: {CoverageReporter.Threshold * 100:F0}%)"
        );

        if (!decision.Passed)
        {
            _theme.Message(
                Severity.Error,
                $"Below threshold by {(CoverageReporter.Threshold * 100 - decision.Percentage):F2} percentage points."
            );
            return 1;
        }

        _theme.Message(Severity.Success, "Threshold met.");
        return 0;
    }

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

    private void RenderClassTable(CoverageSummary summary, bool all)
    {
        if (summary.Classes.Count == 0)
            return;

        var rows = all
            ? summary.Classes
            : summary.Classes.Where(c => c.LineRate < CoverageReporter.Threshold).ToList();

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

    private static string DisplayClassName(ClassCoverage c) =>
        c.Class.StartsWith(c.Assembly + ".", StringComparison.Ordinal)
            ? c.Class[(c.Assembly.Length + 1)..]
            : c.Class;

    private sealed record CoverageDiscovery(
        IReadOnlyList<string> ReportPaths,
        IReadOnlyList<string> CoveredTierDirs
    );
}
