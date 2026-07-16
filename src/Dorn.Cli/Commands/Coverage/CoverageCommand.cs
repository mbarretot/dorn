using Dorn.Cli.Coverage;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Spectre.Console;
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
    private readonly IAnsiConsole _console;

    public CoverageCommand(
        IProjectContextResolver resolver,
        IDotnetTestRunner testRunner,
        CoverageReporter reporter,
        IAnsiConsole console
    )
    {
        _resolver = resolver;
        _testRunner = testRunner;
        _reporter = reporter;
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CoverageSettings settings)
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        if (projectContext.Tiers.Count == 0)
        {
            _console.MarkupLine(
                "[yellow]No test tiers found.[/] This project was generated with [bold]IncludeTests=false[/]; nothing to measure."
            );
            return 1;
        }

        var testResult = await _testRunner.RunAsync(
            projectContext,
            DatabaseProvider.Sqlite,
            projectContext.Tiers,
            CancellationToken.None
        );

        if (!testResult.AllSucceeded)
        {
            _console.MarkupLine(
                "[red]One or more tier runs failed; coverage report not generated.[/]"
            );
            return 1;
        }

        // Cobertura files are written to TestResults/<guid>/coverage.cobertura.xml.
        var coberturaPath = FindCoberturaReport(projectContext.Root);
        if (string.IsNullOrEmpty(coberturaPath))
        {
            _console.MarkupLine(
                "[red]No coverage report found.[/] Expected at TestResults/**/coverage.cobertura.xml."
            );
            return 1;
        }

        var parsed = _reporter.ParseCobertura(coberturaPath);
        var decision = _reporter.EvaluateThreshold(parsed.LineRate);

        _console.MarkupLine(
            $"[bold]Line coverage:[/] [cyan]{decision.Percentage:F2}%[/] "
                + $"(threshold: {CoverageReporter.Threshold * 100:F0}%)"
        );

        if (!decision.Passed)
        {
            _console.MarkupLine(
                $"[red]Below threshold[/] by {(CoverageReporter.Threshold * 100 - decision.Percentage):F2} percentage points."
            );
            return 1;
        }

        _console.MarkupLine("[green]Threshold met.[/]");
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
