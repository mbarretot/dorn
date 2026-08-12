using Dorn.Cli.Output;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Dorn.Cli.Commands.Test;

/// <summary>
/// <c>dorn test</c> — runs all (or a filtered subset of) test tiers in the generated project.
/// </summary>
public sealed class TestCommand : AsyncCommand<TestSettings>
{
    private readonly IProjectContextResolver _resolver;
    private readonly IDotnetTestRunner _runner;
    private readonly IDornTheme _theme;
    private readonly ICliOutputWriter _writer;

    public TestCommand(
        IProjectContextResolver resolver,
        IDotnetTestRunner runner,
        IDornTheme theme,
        ICliOutputWriter writer
    )
    {
        _resolver = resolver;
        _runner = runner;
        _theme = theme;
        _writer = writer;
    }

    // Spectre.Console.Cli 0.55.0 changed ExecuteAsync from public to protected (and added
    // CancellationToken); logic lives in the public RunAsync below, and tests call it
    // directly (CommandAppTester was removed in 0.55.0).
    protected override Task<int> ExecuteAsync(
        CommandContext context,
        TestSettings settings,
        CancellationToken cancellationToken
    ) => RunAsync(settings, cancellationToken);

    /// <summary>
    /// Runs the test command logic. Public so unit tests can drive the command directly
    /// without going through the Spectre.Console.Cli command pipeline.
    /// </summary>
    public async Task<int> RunAsync(TestSettings settings, CancellationToken cancellationToken)
    {
        var formatResult = OutputFormatValidator.Validate(settings.Format);
        if (!formatResult.IsValid)
        {
            _theme.Message(Severity.Error, Markup.Escape(formatResult.ErrorMessage!));
            return 1;
        }

        var format = formatResult.Format;
        var result = await ComputeAsync(settings, format, cancellationToken);
        var exitCode = result.Outcome == TestOutcome.TestsFailed ? 1 : 0;

        if (format == OutputFormat.Json)
        {
            EmitJson(result, exitCode);
        }
        else
        {
            Render(result);
        }

        return exitCode;
    }

    /// <summary>Runs tier resolution + dispatch with zero rendering calls, so both the table and JSON branches derive their exit code from one computed outcome.</summary>
    private async Task<TestRunOutcome> ComputeAsync(
        TestSettings settings,
        OutputFormat format,
        CancellationToken cancellationToken
    )
    {
        var root = settings.Project ?? Directory.GetCurrentDirectory();
        var projectContext = _resolver.Resolve(root);

        // Empty tiers = IncludeTests=false — surface a clear non-crash message instead of
        // silently exiting 0.
        if (projectContext.Tiers.Count == 0)
        {
            var (_, noTiersRecognized) = ResolveTiers(settings.Tier, []);
            return new TestRunOutcome(
                TestOutcome.NoTestTiers,
                settings.Tier,
                noTiersRecognized,
                []
            );
        }

        var (tiers, recognized) = ResolveTiers(settings.Tier, projectContext.Tiers);

        // JSON mode must never let the progress region touch stdout ahead of the envelope.
        var result = await _runner.RunAsync(
            projectContext,
            DatabaseProvider.Sqlite,
            tiers,
            cancellationToken,
            suppressLiveOutput: format == OutputFormat.Json
        );

        var outcome = result.AllSucceeded ? TestOutcome.Ok : TestOutcome.TestsFailed;
        return new TestRunOutcome(outcome, settings.Tier, recognized, result.TierResults);
    }

    private void Render(TestRunOutcome result)
    {
        switch (result.Outcome)
        {
            case TestOutcome.NoTestTiers:
                _theme.Message(
                    Severity.Warning,
                    "No test tiers found. This project was generated with [bold]IncludeTests=false[/]; nothing to test."
                );
                return;
            case TestOutcome.TestsFailed:
                _theme.Message(Severity.Error, "One or more tier runs failed.");
                return;
        }
    }

    private void EmitJson(TestRunOutcome result, int exitCode)
    {
        var report = BuildReport(result);
        var envelope = new CliEnvelope<TestReport>(
            SchemaVersion: 1,
            Command: "test",
            Success: exitCode == 0,
            ExitCode: exitCode,
            Data: report
        );
        _writer.WriteLine(CliJson.Serialize(envelope));
    }

    private static TestReport BuildReport(TestRunOutcome result)
    {
        var tiers = result
            .TierResults.Select(t => new TestTierDto(
                t.Tier.ToString(),
                t.Succeeded ? "passed" : "failed",
                CountsAvailable: t.Total is not null,
                t.Total,
                t.Passed,
                t.Failed,
                t.Skipped,
                t.DurationSeconds
            ))
            .ToList();

        var reportUnavailableTiers = tiers
            .Where(t => !t.CountsAvailable)
            .Select(t => t.Tier)
            .ToList();
        var available = tiers.Where(t => t.CountsAvailable).ToList();

        return new TestReport(
            Outcome: OutcomeToken(result.Outcome),
            TierFilter: result.TierFilter,
            TierFilterRecognized: result.TierFilterRecognized,
            TotalTests: available.Count > 0 ? available.Sum(t => t.Total) : null,
            PassedTests: available.Count > 0 ? available.Sum(t => t.Passed) : null,
            FailedTests: available.Count > 0 ? available.Sum(t => t.Failed) : null,
            SkippedTests: available.Count > 0 ? available.Sum(t => t.Skipped) : null,
            DurationSeconds: available.Count > 0 ? available.Sum(t => t.DurationSeconds) : null,
            ReportUnavailableTiers: reportUnavailableTiers,
            Tiers: tiers
        );
    }

    private static string OutcomeToken(TestOutcome outcome) =>
        outcome switch
        {
            TestOutcome.NoTestTiers => "no-test-tiers",
            TestOutcome.TestsFailed => "tests-failed",
            TestOutcome.Ok => "ok",
            _ => outcome.ToString().ToLowerInvariant(),
        };

    /// <summary>
    /// Resolves the raw <c>--tier</c> value to a concrete tier list and whether it matched a
    /// known alias. Unrecognized values fall back to <paramref name="all"/> — silently, exactly
    /// as before this change — and <c>null</c> filter means "no opinion", not "unrecognized".
    /// </summary>
    private static (IReadOnlyList<TestTier> Tiers, bool? Recognized) ResolveTiers(
        string? tierFilter,
        IReadOnlyList<TestTier> all
    )
    {
        if (string.IsNullOrWhiteSpace(tierFilter))
            return (all, null);

        IReadOnlyList<TestTier>? resolved = tierFilter.ToLowerInvariant() switch
        {
            // ADR 0012: Application.Tests IS the unit-level tier; "unit" is the documented alias.
            "unit" or "application" => [TestTier.Application],
            "integration" => [TestTier.Integration],
            "architecture" => [TestTier.Architecture],
            "functional" => [TestTier.Functional],
            _ => null,
        };

        return resolved is not null ? (resolved, true) : (all, false);
    }

    private enum TestOutcome
    {
        NoTestTiers,
        TestsFailed,
        Ok,
    }

    private sealed record TestRunOutcome(
        TestOutcome Outcome,
        string? TierFilter,
        bool? TierFilterRecognized,
        IReadOnlyList<TierRunResult> TierResults
    );
}
