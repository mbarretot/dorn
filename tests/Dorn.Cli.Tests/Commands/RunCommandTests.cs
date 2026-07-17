using System.Runtime.InteropServices;
using Dorn.Cli.Commands.Run;
using Dorn.Cli.Execution;
using Dorn.Cli.Infrastructure;
using Dorn.Cli.Projects;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using Xunit;
using PosixSignalContext = Dorn.Cli.Execution.PosixSignalContext;

namespace Dorn.Cli.Tests.Commands;

///<summary>Tests for <see cref="RunCommand"/>: orchestrator dispatch (Aspire / Compose / Plain) and Compose Ctrl+C teardown. Drives ExecuteAsync with a synthetic CommandContext (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
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

    [Fact]
    public async Task RunCommand_WithAppHost_RunsViaAspire()
    {
        var (runner, _, command) = CreateCommand();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.AppHost"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
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
        var (runner, _, command) = CreateCommand();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "version: '3.9'");
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
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
        var (runner, _, command) = CreateCommand();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
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
        var (_, consoleMock, command) = CreateCommand();
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task RunCommand_OnComposePath_RegistersSIGINTandSIGTERM()
    {
        var (_, _, command, signalReg) = CreateCommandWithSignalMock();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "version: '3.9'");
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        await command.RunAsync(settings, CancellationToken.None);

        signalReg.Received().Register(PosixSignal.SIGINT, Arg.Any<Action<PosixSignalContext>>());
        signalReg.Received().Register(PosixSignal.SIGTERM, Arg.Any<Action<PosixSignalContext>>());
    }

    [Fact]
    public async Task RunCommand_OnAspirePath_DoesNotRegisterSignals()
    {
        var (_, _, command, signalReg) = CreateCommandWithSignalMock();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.AppHost"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        await command.RunAsync(settings, CancellationToken.None);

        signalReg
            .DidNotReceive()
            .Register(Arg.Any<PosixSignal>(), Arg.Any<Action<PosixSignalContext>>());
    }

    [Fact]
    public async Task RunCommand_OnPlainPath_DoesNotRegisterSignals()
    {
        var (_, _, command, signalReg) = CreateCommandWithSignalMock();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        await command.RunAsync(settings, CancellationToken.None);

        signalReg
            .DidNotReceive()
            .Register(Arg.Any<PosixSignal>(), Arg.Any<Action<PosixSignalContext>>());
    }

    [Fact]
    public async Task RunCommand_ComposeCleanupAfterCleanExit_RunsComposeDown()
    {
        var (runner, _, command) = CreateCommand();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src", "MyProject.WebApi"));
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "version: '3.9'");
        File.WriteAllText(Path.Combine(_tempRoot, "MyProject.slnx"), "<Solution />");
        var settings = new RunSettings { Project = _tempRoot };

        await command.RunAsync(settings, CancellationToken.None);

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

    private static CommandContext SyntheticContext() =>
        new CommandContext(Array.Empty<string>(), new EmptyRemainingArgs(), "run", null);

    private (IProcessRunner Runner, IAnsiConsole Console, RunCommand Command) CreateCommand()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);

        var consoleMock = Substitute.For<IAnsiConsole>();
        var resolver = new ProjectContextResolver();
        var signalReg = new SignalRegistration();
        var command = new RunCommand(resolver, processRunner, signalReg, consoleMock);

        return (processRunner, consoleMock, command);
    }

    private (
        IProcessRunner Runner,
        IAnsiConsole Console,
        RunCommand Command,
        ISignalRegistration SignalReg
    ) CreateCommandWithSignalMock()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(0);

        var consoleMock = Substitute.For<IAnsiConsole>();
        var resolver = new ProjectContextResolver();
        var signalReg = Substitute.For<ISignalRegistration>();
        signalReg
            .Register(Arg.Any<PosixSignal>(), Arg.Any<Action<PosixSignalContext>>())
            .Returns(Substitute.For<IDisposable>());
        var command = new RunCommand(resolver, processRunner, signalReg, consoleMock);

        return (processRunner, consoleMock, command, signalReg);
    }
}

///<summary>Minimal <see cref="IRemainingArguments"/> stand-in for tests that build <see cref="CommandContext"/> directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
file sealed class EmptyRemainingArgs : IRemainingArguments
{
    public ILookup<string, string?> Parsed { get; } =
        Array.Empty<string>().ToLookup(x => x, x => (string?)null);

    public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
}
