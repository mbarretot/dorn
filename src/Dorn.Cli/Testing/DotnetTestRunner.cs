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
        CancellationToken ct
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
            return new TestRunResult([], AllSucceeded: true);
        }

        var specs = new List<CapturedProcessSpec>();
        var allSucceeded = true;

        if (_theme.LiveRegionsEnabled)
        {
            await _theme
                .CreateProgress()
                .StartAsync(async ctx =>
                {
                    foreach (var plan in plans)
                    {
                        var task = ctx.AddTask(plan.Tier.ToString(), autoStart: false, maxValue: 1);
                        task.StartTask();
                        if (!await RunTierAsync(context, plan.Path, specs, ct))
                            allSucceeded = false;
                        task.Increment(1);
                    }
                });
        }
        else
        {
            foreach (var plan in plans)
            {
                if (!await RunTierAsync(context, plan.Path, specs, ct))
                    allSucceeded = false;
            }
        }

        return new TestRunResult(specs, allSucceeded);
    }

    private async Task<bool> RunTierAsync(
        ProjectContext context,
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
            ],
            context.Root
        );

        specs.Add(new CapturedProcessSpec(spec.FileName, spec.Arguments, spec.WorkingDirectory));

        var exitCode = await _processRunner.RunAsync(spec, ct);
        return exitCode == 0;
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
