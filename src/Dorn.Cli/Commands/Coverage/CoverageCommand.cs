using Dorn.Cli.Coverage;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Coverage;

/// <summary>
/// <c>dorn coverage</c> — runs all test tiers with coverage collection, parses the merged
/// Cobertura report, applies the fixed 80% threshold gate, and prints a summary.
/// </summary>
public sealed class CoverageCommand : AsyncCommand<CoverageSettings>
{
    private readonly IProjectContextResolver _resolver;
    private readonly IDotnetTestRunner _testRunner;
    private readonly CoverageReporter _reporter;
    private readonly IDornTheme _theme;

    public CoverageCommand(
        IProjectContextResolver resolver,
        IDotnetTestRunner testRunner,
        CoverageReporter reporter,
        IDornTheme theme
    )
    {
        _resolver = resolver;
        _testRunner = testRunner;
        _reporter = reporter;
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

        // Cobertura files are written to TestResults/<guid>/coverage.cobertura.xml.
        var coberturaPath = FindCoberturaReport(projectContext.Root);
        if (string.IsNullOrEmpty(coberturaPath))
        {
            _theme.Message(
                Severity.Error,
                "No coverage report found. Expected at TestResults/**/coverage.cobertura.xml."
            );
            return 1;
        }

        var parsed = _reporter.ParseCobertura(coberturaPath);
        var decision = _reporter.EvaluateThreshold(parsed.LineRate);

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

    private static string? FindCoberturaReport(string root)
    {
        var testResults = Path.Combine(root, "TestResults");
        if (!Directory.Exists(testResults))
            return null;

        return Directory
            .EnumerateFiles(
                testResults,
                "coverage.cobertura.xml",
                new EnumerationOptions { RecurseSubdirectories = true }
            )
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
