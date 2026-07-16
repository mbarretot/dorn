using System.Runtime.InteropServices;
using Dorn.Cli.Commands.Run;
using Dorn.Cli.Execution;
using Dorn.Cli.Infrastructure;
using Dorn.Cli.Projects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;
using PosixSignalContext = Dorn.Cli.Execution.PosixSignalContext;

namespace Dorn.Cli.Tests.Commands;

/// <summary>
/// Tests for <see cref="RunCommand"/>. Exercises orchestrator dispatch (Aspire / Compose / Plain)
/// and the Compose Ctrl+C teardown contract.
/// </summary>
public class RunCommandTests : IDisposable
{
    private readonly string _tempRoot;

    public RunCommandTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-runcmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Orchestrator dispatch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunCommand_WithAppHost_RunsViaAspire()
    {
        var (app, runner) = CreateApp();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.AppHost"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        var result = await app.RunAsync(["run", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        await runner
            .Received()
            .RunAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "dotnet"
                    && s.Arguments.Contains("run")
                    && s.Arguments.Any(a => a.Contains("AppHost"))
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunCommand_WithoutAppHostButWithCompose_RunsViaDockerCompose()
    {
        var (app, runner) = CreateApp();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "version: '3.9'");
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        var result = await app.RunAsync(["run", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        await runner
            .Received()
            .RunAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "docker" && s.Arguments[0] == "compose" && s.Arguments[1] == "up"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunCommand_WithoutAppHostAndCompose_RunsPlainDotnetRun()
    {
        var (app, runner) = CreateApp();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        var result = await app.RunAsync(["run", "--project", _tempRoot]);

        Assert.Equal(0, result.ExitCode);
        await runner
            .Received()
            .RunAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "dotnet"
                    && s.Arguments.Contains("run")
                    && s.Arguments.Any(a => a.Contains("WebApi"))
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunCommand_PlainWithoutWebApi_ReturnsExitCodeOne()
    {
        var (app, _) = CreateApp();
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        var result = await app.RunAsync(["run", "--project", _tempRoot]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("WebApi project", result.Output);
    }

    // -------------------------------------------------------------------------
    // Compose teardown
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunCommand_OnComposePath_RegistersSIGINTandSIGTERM()
    {
        var (app, signalReg, _) = CreateAppWithSignalMock();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "version: '3.9'");
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        await app.RunAsync(["run", "--project", _tempRoot]);

        signalReg.Received().Register(PosixSignal.SIGINT, Arg.Any<Action<PosixSignalContext>>());
        signalReg.Received().Register(PosixSignal.SIGTERM, Arg.Any<Action<PosixSignalContext>>());
    }

    [Fact]
    public async Task RunCommand_OnAspirePath_DoesNotRegisterSignals()
    {
        var (app, signalReg, _) = CreateAppWithSignalMock();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.AppHost"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        await app.RunAsync(["run", "--project", _tempRoot]);

        signalReg
            .DidNotReceive()
            .Register(Arg.Any<PosixSignal>(), Arg.Any<Action<PosixSignalContext>>());
    }

    [Fact]
    public async Task RunCommand_OnPlainPath_DoesNotRegisterSignals()
    {
        var (app, signalReg, _) = CreateAppWithSignalMock();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        await app.RunAsync(["run", "--project", _tempRoot]);

        signalReg
            .DidNotReceive()
            .Register(Arg.Any<PosixSignal>(), Arg.Any<Action<PosixSignalContext>>());
    }

    [Fact]
    public async Task RunCommand_ComposeCleanupAfterCleanExit_RunsComposeDown()
    {
        var (app, runner) = CreateApp();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "version: '3.9'");
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");

        await app.RunAsync(["run", "--project", _tempRoot]);

        // 'docker compose up' then 'docker compose down'.
        await runner
            .Received()
            .RunAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "docker" && s.Arguments[0] == "compose" && s.Arguments[1] == "up"
                ),
                Arg.Any<CancellationToken>()
            );
        await runner
            .Received()
            .RunAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "docker"
                    && s.Arguments[0] == "compose"
                    && s.Arguments[1] == "down"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private (CommandAppTester App, IProcessRunner Runner) CreateApp()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);

        var services = new ServiceCollection();
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton(processRunner);
        services.AddSingleton<ISignalRegistration, SignalRegistration>();
        services.AddSingleton<IAnsiConsole>(new TestConsole());

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar);
        app.Configure(config => config.AddCommand<RunCommand>("run"));

        return (app, processRunner);
    }

    private (
        CommandAppTester App,
        ISignalRegistration SignalReg,
        IProcessRunner Runner
    ) CreateAppWithSignalMock()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);

        var signalReg = Substitute.For<ISignalRegistration>();
        signalReg
            .Register(Arg.Any<PosixSignal>(), Arg.Any<Action<PosixSignalContext>>())
            .Returns(Substitute.For<IDisposable>());

        var services = new ServiceCollection();
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton(processRunner);
        services.AddSingleton(signalReg);
        services.AddSingleton<IAnsiConsole>(new TestConsole());

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar);
        app.Configure(config => config.AddCommand<RunCommand>("run"));

        return (app, signalReg, processRunner);
    }
}
