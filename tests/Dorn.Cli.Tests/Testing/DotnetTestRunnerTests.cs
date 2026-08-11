using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using NSubstitute;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Testing;

/// <summary>
/// Tests tier mapping, default-all and IncludeTests=false behavior, and Docker preflight warnings without real processes.
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

    // Tier-to-project mapping

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
    public async Task RunAsync_WithAllFourTiers_InvokesDotnetTestFourTimes()
    {
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateTestsDir("MyProject.Architecture.Tests");
        CreateTestsDir("MyProject.Functional.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [
                TestTier.Application,
                TestTier.Integration,
                TestTier.Architecture,
                TestTier.Functional,
            ],
            CancellationToken.None
        );

        Assert.True(result.AllSucceeded);
        Assert.Equal(4, result.Specs.Count);
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

    // Working directory + dotnet arguments

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

    [Fact]
    public async Task RunAsync_CollectCoverageFlag_HasNoEmbeddedQuoteCharacters()
    {
        // ArgumentList has no shell — a literal '"' here reaches MSBuild and fails with MSB4177.
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.Equal("--collect:XPlat Code Coverage", result.Specs[0].Arguments[2]);
    }

    [Fact]
    public async Task RunAsync_PassesResultsDirectoryUnderProjectRoot()
    {
        // FindCoberturaReport only searches under context.Root, not the tier project's own dir.
        CreateTestsDir("MyProject.Application.Tests");
        var runner = CreateRunner();
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        var args = result.Specs[0].Arguments;
        var resultsDirIndex = Array.IndexOf(args.ToArray(), "--results-directory");
        Assert.True(resultsDirIndex >= 0, "Expected --results-directory in the arguments.");
        var resultsDir = args[resultsDirIndex + 1];
        Assert.StartsWith(Path.Combine(ctx.Root, "TestResults"), resultsDir);
    }

    // Failure propagation

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
        var runner = new DotnetTestRunner(processRunner, new DornTheme(CreateConsole()));
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

    [Fact]
    public async Task RunAsync_InteractiveVsNonInteractive_InvokesProcessRunnerIdenticallyPerTier()
    {
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateTestsDir("MyProject.Architecture.Tests");
        CreateTestsDir("MyProject.Functional.Tests");
        var tiers = new[]
        {
            TestTier.Application,
            TestTier.Integration,
            TestTier.Architecture,
            TestTier.Functional,
        };
        var ctx = CreateContextWithAllTiers("MyProject");

        var nonInteractiveProcessRunner = Substitute.For<IProcessRunner>();
        nonInteractiveProcessRunner
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(0);
        var nonInteractiveRunner = new DotnetTestRunner(
            nonInteractiveProcessRunner,
            new DornTheme(CreateConsole(interactive: false))
        );

        var interactiveProcessRunner = Substitute.For<IProcessRunner>();
        interactiveProcessRunner
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(0);
        var interactiveRunner = new DotnetTestRunner(
            interactiveProcessRunner,
            new DornTheme(CreateConsole(interactive: true))
        );

        var nonInteractiveResult = await nonInteractiveRunner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            tiers,
            CancellationToken.None
        );
        var interactiveResult = await interactiveRunner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            tiers,
            CancellationToken.None
        );

        await nonInteractiveProcessRunner
            .Received(4)
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>());
        await interactiveProcessRunner
            .Received(4)
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>());
        Assert.Equal(nonInteractiveResult.AllSucceeded, interactiveResult.AllSucceeded);
        AssertSameSpecs(nonInteractiveResult.Specs, interactiveResult.Specs);
    }

    [Fact]
    public async Task RunAsync_Interactive_ProgressTaskLabelIsUnambiguousAboutExecution()
    {
        // 100% here means "tier finished running", not "code coverage" shown right below.
        CreateTestsDir("MyProject.Application.Tests");
        var console = CreateConsole(interactive: true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);
        var runner = new DotnetTestRunner(processRunner, new DornTheme(console));
        var ctx = CreateContextWithAllTiers("MyProject");

        await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.Contains("Running Application tests", console.Output);
    }

    [Fact]
    public async Task RunAsync_SuppressLiveOutputTrueWithLiveRegionsEnabled_DoesNotRenderProgressRegion()
    {
        CreateTestsDir("MyProject.Application.Tests");
        var console = CreateConsole(interactive: true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);
        var runner = new DotnetTestRunner(processRunner, new DornTheme(console));
        var ctx = CreateContextWithAllTiers("MyProject");

        var result = await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None,
            suppressLiveOutput: true
        );

        Assert.DoesNotContain("Running Application tests", console.Output);
        Assert.True(result.AllSucceeded);
        Assert.Single(result.Specs);
    }

    [Fact]
    public async Task RunAsync_SuppressLiveOutputOmitted_StillRendersProgressRegionWhenInteractive()
    {
        // Regression proof: default (`false`) preserves today's exact behavior for existing callers.
        CreateTestsDir("MyProject.Application.Tests");
        var console = CreateConsole(interactive: true);
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);
        var runner = new DotnetTestRunner(processRunner, new DornTheme(console));
        var ctx = CreateContextWithAllTiers("MyProject");

        await runner.RunAsync(
            ctx,
            DatabaseProvider.Sqlite,
            [TestTier.Application],
            CancellationToken.None
        );

        Assert.Contains("Running Application tests", console.Output);
    }

    // Helpers

    private DotnetTestRunner CreateRunner(int processExitCode = 0, bool interactive = false)
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(processExitCode);
        return new DotnetTestRunner(processRunner, new DornTheme(CreateConsole(interactive)));
    }

    private static TestConsole CreateConsole(bool interactive = false)
    {
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.Interactive = interactive;
        return console;
    }

    private static void AssertSameSpecs(
        IReadOnlyList<CapturedProcessSpec> expected,
        IReadOnlyList<CapturedProcessSpec> actual
    )
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].FileName, actual[i].FileName);
            Assert.Equal(expected[i].WorkingDirectory, actual[i].WorkingDirectory);
            Assert.Equal(
                string.Join(" ", expected[i].Arguments),
                string.Join(" ", actual[i].Arguments)
            );
        }
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
