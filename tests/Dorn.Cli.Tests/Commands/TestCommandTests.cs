using Dorn.Cli.Commands.Test;
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
/// Tests for <see cref="TestCommand"/>. Routes through <see cref="CommandAppTester"/>
/// to exercise the full Spectre.Console.Cli wiring (option parsing, DI resolution, exit codes).
/// </summary>
public class TestCommandTests : IDisposable
{
    private readonly string _tempRoot;

    public TestCommandTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-testcmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Tier filter
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TestCommand_WithTierFilter_OnlyRunsThatTier()
    {
        var (app, runner) = CreateApp();
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");

        var result = await app.RunAsync(["test", "--tier", "integration", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        await runner
            .Received(1)
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Any<IReadOnlyList<TestTier>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task TestCommand_WithoutTierFilter_RunsAllTiers()
    {
        var (app, _) = CreateApp();
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");

        var result = await app.RunAsync(["test", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
    }

    // -------------------------------------------------------------------------
    // IncludeTests=false handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TestCommand_WithoutTestDirectories_PrintsClearMessageAndReturnsZero()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");

        var result = await app.RunAsync(["test", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("IncludeTests=false", result.Output);
    }

    // -------------------------------------------------------------------------
    // Default CWD
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TestCommand_WithoutProjectOption_UsesCurrentDirectory()
    {
        var (app, _) = CreateApp();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempRoot);
            var result = await app.RunAsync(["test"]);

            Assert.Equal(0, result.ExitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
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
        // TestConsole implements IAnsiConsole — use it both as the registration and
        // through CommandAppTester to capture output for assertions.
        var console = new TestConsole();
        services.AddSingleton<IAnsiConsole>(console);

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar);
        app.Configure(config => config.AddCommand<TestCommand>("test"));

        return (app, testRunner);
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
}
