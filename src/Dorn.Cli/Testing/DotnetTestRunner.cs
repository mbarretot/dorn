using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
using Dorn.Cli.Theming;

namespace Dorn.Cli.Testing;

public sealed class DotnetTestRunner : IDotnetTestRunner
{
    private readonly IProcessRunner _processRunner;
    private readonly IDornTheme _theme;

    public DotnetTestRunner(IProcessRunner processRunner, IDornTheme theme)
    {
        _processRunner = processRunner;
        _theme = theme;
    }

    /// <param name="context">The resolved project context.</param>
    /// <param name="database">Database provider — controls the Docker preflight warning.</param>
    /// <param name="tiers">Tiers to run. Empty list = IncludeTests=false scenario.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TestRunResult> RunAsync(
        ProjectContext context,
        DatabaseProvider database,
        IReadOnlyList<TestTier> tiers,
        CancellationToken ct,
        bool suppressLiveOutput = false
    )
    {
        // Resolve tier plans and emit the Docker warning up front — writes inside a live
        // region interleave badly with the progress renderer.
        var plans = new List<(TestTier Tier, string Path)>();
        foreach (var tier in tiers)
        {
            var tierPath = ResolveTierPath(context, tier);
            if (string.IsNullOrEmpty(tierPath))
                continue;

            if (tier == TestTier.Integration && database != DatabaseProvider.Sqlite)
            {
                WarnDockerRequired($"integration tests with {DescribeProvider(database)}");
            }

            plans.Add((tier, tierPath));
        }

        if (plans.Count == 0)
        {
            return new TestRunResult([], AllSucceeded: true, TierResults: []);
        }

        var specs = new List<CapturedProcessSpec>();
        var tierResults = new List<TierRunResult>();
        var allSucceeded = true;

        if (_theme.LiveRegionsEnabled && !suppressLiveOutput)
        {
            await _theme
                .CreateProgress()
                .StartAsync(async ctx =>
                {
                    foreach (var plan in plans)
                    {
                        var task = ctx.AddTask(
                            $"Running {plan.Tier} tests",
                            autoStart: false,
                            maxValue: 1
                        );
                        task.StartTask();
                        var tierResult = await RunTierAsync(
                            context,
                            plan.Tier,
                            plan.Path,
                            specs,
                            ct
                        );
                        tierResults.Add(tierResult);
                        if (!tierResult.Succeeded)
                            allSucceeded = false;
                        task.Increment(1);
                    }
                });
        }
        else
        {
            foreach (var plan in plans)
            {
                var tierResult = await RunTierAsync(context, plan.Tier, plan.Path, specs, ct);
                tierResults.Add(tierResult);
                if (!tierResult.Succeeded)
                    allSucceeded = false;
            }
        }

        return new TestRunResult(specs, allSucceeded, tierResults);
    }

    private async Task<TierRunResult> RunTierAsync(
        ProjectContext context,
        TestTier tier,
        string tierPath,
        List<CapturedProcessSpec> specs,
        CancellationToken ct
    )
    {
        // FindCoberturaReport only searches under context.Root, not the tier project's own dir.
        var resultsDirectory = Path.Combine(
            context.Root,
            "TestResults",
            Path.GetFileName(tierPath)
        );

        var spec = new ProcessSpec(
            "dotnet",
            [
                "test",
                tierPath,
                "--collect:XPlat Code Coverage",
                "--results-directory",
                resultsDirectory,
                "--no-build",
                "--logger",
                "trx;LogFileName=dorn.trx",
            ],
            context.Root
        );

        specs.Add(new CapturedProcessSpec(spec.FileName, spec.Arguments, spec.WorkingDirectory));

        // -2s slack covers coarse-granularity filesystems and rejects a stale TRX left over
        // from a previous run when dotnet itself fails to start.
        var tierStartedUtc = DateTime.UtcNow.AddSeconds(-2);
        var exitCode = await _processRunner.RunAsync(spec, ct);
        var succeeded = exitCode == 0;

        var trxPath = Path.Combine(resultsDirectory, "dorn.trx");
        var summary =
            File.Exists(trxPath) && File.GetLastWriteTimeUtc(trxPath) >= tierStartedUtc
                ? TrxSummaryReader.TryRead(trxPath)
                : null;

        return new TierRunResult(
            tier,
            succeeded,
            summary?.Total,
            summary?.Passed,
            summary?.Failed,
            summary?.Skipped,
            summary?.DurationSeconds
        );
    }

    private static string? ResolveTierPath(ProjectContext context, TestTier tier)
    {
        var testsDir = Path.Combine(context.Root, "tests");
        if (!Directory.Exists(testsDir))
            return null;

        var suffix = tier switch
        {
            TestTier.Application => ".Application.Tests",
            TestTier.Integration => ".Integration.Tests",
            TestTier.Architecture => ".Architecture.Tests",
            TestTier.Functional => ".Functional.Tests",
            _ => null,
        };

        if (suffix is null)
            return null;

        return Directory
            .EnumerateDirectories(
                testsDir,
                "*" + suffix,
                new EnumerationOptions { RecurseSubdirectories = false }
            )
            .FirstOrDefault();
    }

    private void WarnDockerRequired(string operation)
    {
        _theme.Message(
            Severity.Warning,
            $"[bold]{operation}[/] requires Docker. Ensure the Docker daemon is running before continuing."
        );
    }

    private static string DescribeProvider(DatabaseProvider database) =>
        database switch
        {
            DatabaseProvider.SqlServer => "sqlserver",
            DatabaseProvider.Postgres => "postgres",
            _ => database.ToString().ToLowerInvariant(),
        };
}
