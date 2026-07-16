using Dorn.Cli.Commands.Coverage;
using Dorn.Cli.Coverage;
using Dorn.Cli.Execution;
using Dorn.Cli.Infrastructure;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

/// <summary>
/// Tests for <see cref="CoverageCommand"/>. Exercises the full pipeline:
/// tier dispatch → test run → Cobertura parsing → threshold gate.
/// </summary>
public class CoverageCommandTests : IDisposable
{
    private readonly string _tempRoot;

    public CoverageCommandTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-covcmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task CoverageCommand_WithoutTiers_ReturnsExitOneWithClearMessage()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");

        var result = await app.RunAsync(["coverage", "--project", _tempRoot]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("IncludeTests=false", result.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenAllTiersPassAndAboveThreshold_ReturnsZero()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateCoberturaReport(lineRate: 0.85);

        var result = await app.RunAsync(["coverage", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("85", result.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenBelowThreshold_ReturnsExitOne()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateCoberturaReport(lineRate: 0.50);

        var result = await app.RunAsync(["coverage", "--project", _tempRoot]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Below threshold", result.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenAtThreshold_ReturnsZero()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateCoberturaReport(lineRate: 0.80);

        var result = await app.RunAsync(["coverage", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Threshold met", result.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenTestsFail_ReturnsExitOneWithoutThreshold()
    {
        var (app, _, testRunner) = CreateAppWithFailingRunner();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");

        var result = await app.RunAsync(["coverage", "--project", _tempRoot]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("failed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoverageCommand_WhenNoCoverageReport_ReturnsExitOne()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        // No Cobertura report created.

        var result = await app.RunAsync(["coverage", "--project", _tempRoot]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No coverage report", result.Output);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private (CommandAppTester App, IDotnetTestRunner Runner) CreateApp()
    {
        var testRunner = Substitute.For<IDotnetTestRunner>();
        testRunner
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Any<IReadOnlyList<TestTier>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new TestRunResult([], AllSucceeded: true));

        var services = new ServiceCollection();
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IDotnetTestRunner>(testRunner);
        services.AddSingleton<CoverageReporter>();
        services.AddSingleton<IAnsiConsole>(new TestConsole());

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar);
        app.Configure(config => config.AddCommand<CoverageCommand>("coverage"));

        return (app, testRunner);
    }

    private (
        CommandAppTester App,
        object Unused,
        IDotnetTestRunner Runner
    ) CreateAppWithFailingRunner()
    {
        var testRunner = Substitute.For<IDotnetTestRunner>();
        testRunner
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Any<IReadOnlyList<TestTier>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new TestRunResult([], AllSucceeded: false));

        var services = new ServiceCollection();
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IDotnetTestRunner>(testRunner);
        services.AddSingleton<CoverageReporter>();
        services.AddSingleton<IAnsiConsole>(new TestConsole());

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar);
        app.Configure(config => config.AddCommand<CoverageCommand>("coverage"));

        return (app, new object(), testRunner);
    }

    private void CreateTestsDir(string name)
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "tests", name));
    }

    private void CreateSolution(string name)
    {
        File.WriteAllText(Path.Combine(_tempRoot, name), "<Solution />");
    }

    private void CreateWebApi(string name)
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", name));
    }

    private void CreateCoberturaReport(double lineRate)
    {
        var guid = Guid.NewGuid().ToString();
        var dir = Path.Combine(_tempRoot, "TestResults", guid);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "coverage.cobertura.xml"),
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + $"<coverage line-rate=\"{lineRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" "
                + $"branch-rate=\"0.5\" version=\"1.9\" timestamp=\"0\" lines-covered=\"0\" lines-valid=\"0\" "
                + $"branches-covered=\"0\" branches-valid=\"0\"></coverage>"
        );
    }
}
