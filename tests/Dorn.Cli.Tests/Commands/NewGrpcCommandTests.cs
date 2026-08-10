using System.Text;
using Dorn.Abstractions.Generation;
using Dorn.Cli.Commands.New;
using Dorn.Cli.Execution;
using Dorn.Cli.Theming;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

/// <summary>
/// Mirrors the threat-matrix shape of <c>NewWebApiCommandTests</c>: project-name validation
/// and the dorn-grpc short name are the only required behaviors, since the gRPC MVP is
/// fixed at sqlite + EF Core + Aspire (no provider/orm/orchestrator options).
/// </summary>
public class NewGrpcCommandTests
{
    [Fact]
    public async Task NewGrpc_WithInvalidProjectName_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        // Threat-matrix row: project-name validation must short-circuit before any
        // subprocess argv is built or any engine call is attempted.
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewGrpcSettings { Name = "1BadName" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewGrpc_WithInvalidProjectName_RendersErrorPanelWithoutInvokingProcessRunner()
    {
        // Same threat-matrix row: invalid path must not run a subprocess.
        var (engine, processRunner, command, consoleMock) = CreateCommandWithConsole();

        var exitCode = await command.RunAsync(
            new NewGrpcSettings { Name = "1BadName" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
        await processRunner
            .DidNotReceive()
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>());
        // Error panel is rendered so the user sees the reason.
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task NewGrpc_WithSuccessfulGeneration_ReturnsExitCodeZeroAndCallsEngineWithDornGrpcShortName()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyService", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewGrpcSettings { Name = "MyService" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.TemplateShortName == "dorn-grpc" && r.ProjectName == "MyService"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewGrpc_WithSuccessfulGeneration_PassesNoParametersDictionary()
    {
        // The gRPC MVP is fixed (sqlite + EF Core + Aspire); the engine must receive
        // a request with no Parameters so the template's default symbol values flow.
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyService", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewGrpcSettings { Name = "MyService" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r => r.Parameters == null),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewGrpc_WithFailedGeneration_ReturnsNonZeroExitCode()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new GenerationResult(
                    false,
                    "/tmp/MyService",
                    [],
                    [new GenerationDiagnostic(GenerationDiagnosticSeverity.Error, "boom")]
                )
            );

        var exitCode = await command.RunAsync(
            new NewGrpcSettings { Name = "MyService" },
            CancellationToken.None
        );

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task NewGrpc_WithSuccessfulGeneration_RunsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-grpc-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.ArgAt<GenerationRequest>(0);
                var manifestDir = Path.Combine(request.OutputDirectory, ".config");
                Directory.CreateDirectory(manifestDir);
                File.WriteAllText(Path.Combine(manifestDir, "dotnet-tools.json"), "{}");
                return new GenerationResult(true, request.OutputDirectory, ["Program.cs"], []);
            });

        try
        {
            var exitCode = await command.RunAsync(
                new NewGrpcSettings { Name = "MyService", Output = tempDir },
                CancellationToken.None
            );

            Assert.Equal(0, exitCode);
            await processRunner
                .Received(1)
                .RunAsync(
                    Arg.Is<ProcessSpec>(s =>
                        s.FileName == "dotnet"
                        && s.Arguments.Contains("tool")
                        && s.Arguments.Contains("restore")
                    ),
                    Arg.Any<CancellationToken>()
                );
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NewGrpc_WithNoRestoreFlag_SkipsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-grpc-norestore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.ArgAt<GenerationRequest>(0);
                var manifestDir = Path.Combine(request.OutputDirectory, ".config");
                Directory.CreateDirectory(manifestDir);
                File.WriteAllText(Path.Combine(manifestDir, "dotnet-tools.json"), "{}");
                return new GenerationResult(true, request.OutputDirectory, ["Program.cs"], []);
            });

        try
        {
            var exitCode = await command.RunAsync(
                new NewGrpcSettings
                {
                    Name = "MyService",
                    Output = tempDir,
                    NoRestore = true,
                },
                CancellationToken.None
            );

            Assert.Equal(0, exitCode);
            await processRunner
                .DidNotReceive()
                .RunAsync(
                    Arg.Is<ProcessSpec>(s =>
                        s.FileName == "dotnet"
                        && s.Arguments.Contains("tool")
                        && s.Arguments.Contains("restore")
                    ),
                    Arg.Any<CancellationToken>()
                );
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private (
        IGenerationEngine Engine,
        IProcessRunner ProcessRunner,
        NewGrpcCommand Command
    ) CreateCommand()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var consoleMock = CreateNonInteractiveConsoleMock();
        var theme = new DornTheme(consoleMock);
        var command = new NewGrpcCommand(engine, processRunner, consoleMock, theme);
        return (engine, processRunner, command);
    }

    private (
        IGenerationEngine Engine,
        IProcessRunner ProcessRunner,
        NewGrpcCommand Command,
        IAnsiConsole Console
    ) CreateCommandWithConsole()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var consoleMock = CreateNonInteractiveConsoleMock();
        var theme = new DornTheme(consoleMock);
        var command = new NewGrpcCommand(engine, processRunner, consoleMock, theme);
        return (engine, processRunner, command, consoleMock);
    }

    /// <summary>TestConsole-style interactive flows aren't exercised — the gRPC MVP has no SelectionPrompt paths. Interactive=false, Unicode=true set explicitly (no test may rely on defaults).</summary>
    private static IAnsiConsole CreateNonInteractiveConsoleMock()
    {
        var consoleMock = Substitute.For<IAnsiConsole>();
        var capabilities = new Capabilities { Interactive = false, Unicode = true };
        var profile = new Profile(
            Substitute.For<IAnsiConsoleOutput>(),
            capabilities,
            Encoding.UTF8
        );
        consoleMock.Profile.Returns(profile);
        return consoleMock;
    }
}
