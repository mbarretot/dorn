using System.Text.RegularExpressions;
using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="DotnetTestRunner"/>. Exercises the tier-to-project
/// mapping, default-all behavior, IncludeTests=false handling, and Docker
/// preflight warning logic without spawning real processes.
/// </summary>
public class DotnetTestRunnerTests : IDisposable
{
    private readonly string _tempRoot;

    public DotnetTestRunnerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-testrunner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Tier-to-project mapping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithApplicationTier_InvokesDotnetTestOnApplicationProject()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.True(result.AllSucceeded);
        Assert.Single(result.Specs);
        Assert.Matches(@"MyProject\.Application\.Tests", result.Specs[0].Arguments[1]);
    }

    [Fact]
    public async Task RunAsync_WithIntegrationTier_InvokesDotnetTestOnIntegrationProject()
    {
        CreateTestsDir("MyProject.Integration.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Integration],
            CancellationToken.None
        );

        Assert.True(result.AllSucceeded);
        Assert.Single(result.Specs);
        Assert.Matches(@"MyProject\.Integration\.Tests", result.Specs[0].Arguments[1]);
    }

    [Fact]
    public async Task RunAsync_WithAllFiveTiers_InvokesDotnetTestFiveTimes()
    {
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateTestsDir("MyProject.Architecture.Tests");
        CreateTestsDir("MyProject.Functional.Tests");
        CreateTestsDir("MyProject.Unit.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [
                TestTier.Unit,
                TestTier.Application,
                TestTier.Integration,
                TestTier.Architecture,
                TestTier.Functional,
            ],
            CancellationToken.None
        );

        Assert.True(result.AllSucceeded);
        Assert.Equal(5, result.Specs.Count);
    }

    [Fact]
    public async Task RunAsync_WithNoTiers_ReturnsEmptyResultAndDoesNotThrow()
    {
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject"); // tiers list populated, but no tier dirs exist on disk

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [],
            CancellationToken.None
        );

        Assert.True(result.AllSucceeded);
        Assert.Empty(result.Specs);
    }

    [Fact]
    public async Task RunAsync_ContextHasNoTiers_ReturnsEmptyResult()
    {
        // IncludeTests=false scenario: ctx.Tiers is empty.
        var runner = CreateRunner();
        var ctx = new ProjectContext(_tempRoot, "", Orchestrator.Plain, null, []);

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [],
            CancellationToken.None
        );

        Assert.True(result.AllSucceeded);
        Assert.Empty(result.Specs);
    }

    // -------------------------------------------------------------------------
    // Working directory + dotnet arguments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_SetsWorkingDirectoryToProjectRoot()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.Equal(_tempRoot, result.Specs[0].WorkingDirectory);
    }

    [Fact]
    public async Task RunAsync_UsesDotnetExe()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.Equal("dotnet", result.Specs[0].FileName);
    }

    [Fact]
    public async Task RunAsync_InvokesDotnetTestSubcommand()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.Equal("test", result.Specs[0].Arguments[0]);
    }

    [Fact]
    public async Task RunAsync_PassesCollectCoverageFlag()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        // Coverage collection is required so PR4 (dorn coverage) can reuse the result.
        var args = string.Join(" ", result.Specs[0].Arguments);
        Assert.Contains("XPlat Code Coverage", args);
    }

    // -------------------------------------------------------------------------
    // Failure propagation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WhenProcessReturnsNonZero_ReturnsFailedResult()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner(processExitCode: 1);
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Specs);
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellationFromProcessRunner()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new OperationCanceledException());
        var runner = new DotnetTestRunner(processRunner, new TestConsole());
        var ctx = CreateContextWithAllTiers("MyProject");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(
                ctx,
                DatabaseProvider.Sqlite,
                [TestTier.Application],
                CancellationToken.None
            )
        );
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private DotnetTestRunner CreateRunner(int processExitCode = 0)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(processExitCode);
        return new DotnetTestRunner(processRunner, new TestConsole());
    }

    private void CreateTestsDir(string name)
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "tests", name));
    }

    private ProjectContext CreateContextWithAllTiers(string projectName)
    {
        var webApi = Path.Combine(_tempRoot, "src", $"{projectName}.WebApi");
        var tiers = new List<TestTier>
        {
            TestTier.Unit,
            TestTier.Application,
            TestTier.Integration,
            TestTier.Architecture,
            TestTier.Functional,
        };
        return new ProjectContext(
            Root: _tempRoot,
            SolutionPath: Path.Combine(_tempRoot, $"{projectName}.slnx"),
            Orchestrator: Orchestrator.Plain,
            WebApiProject: webApi,
            Tiers: tiers
        );
    }
}
