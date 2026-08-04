using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
using Spectre.Console;

namespace Dorn.Cli.Testing;

/// <summary>
/// Runs <c>dotnet test</c> for generated webapi test tiers without parsing output; exit codes are tracked per tier.
/// </summary>
public sealed class DotnetTestRunner : IDotnetTestRunner
{
    private readonly IProcessRunner _processRunner;
    private readonly IAnsiConsole _console;

    public DotnetTestRunner(IProcessRunner processRunner, IAnsiConsole console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    /// <summary>
    /// Runs <c>dotnet test</c> against the given tiers in the target project.
    /// </summary>
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
        var specs = new List<CapturedProcessSpec>();

        // No tiers requested — early exit without warning or invocation.
        if (tiers.Count == 0)
        {
            return new TestRunResult(specs, AllSucceeded: true);
        }

        var allSucceeded = true;

        foreach (var tier in tiers)
        {
            // Skip tiers that don't actually exist on disk (resolver found them, but the
            // directory may have been deleted between resolve and run).
            var tierPath = ResolveTierPath(context, tier);
            if (string.IsNullOrEmpty(tierPath))
                continue;

            if (tier == TestTier.Integration && database != DatabaseProvider.Sqlite)
            {
                WarnDockerRequired($"integration tests with {DescribeProvider(database)}");
            }

            var spec = new ProcessSpec(
                "dotnet",
                ["test", tierPath, "--collect:\"XPlat Code Coverage\"", "--no-build"],
                context.Root
            );

            specs.Add(
                new CapturedProcessSpec(spec.FileName, spec.Arguments, spec.WorkingDirectory)
            );

            var exitCode = await _processRunner.RunAsync(spec, ct);
            if (exitCode != 0)
                allSucceeded = false;
        }

        return new TestRunResult(specs, allSucceeded);
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
        _console.MarkupLine(
            $"[yellow]Warning[/]: [bold]{operation}[/] requires Docker. Ensure the Docker daemon is running before continuing."
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
