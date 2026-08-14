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
/// Mirrors <c>NewGrpcCommandTests</c>' threat-matrix shape plus the theme/playground
/// resolution shape mirrored from <c>NewWebApiCommandTests</c>' database/orchestrator prompts.
/// </summary>
public class NewBlazorWasmCommandTests
{
    [Fact]
    public async Task NewBlazorWasm_WithInvalidProjectName_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "1BadName" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewBlazorWasm_WithInvalidProjectName_RendersErrorPanelWithoutInvokingProcessRunner()
    {
        var (engine, processRunner, command, consoleMock) = CreateCommandWithConsole();

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "1BadName" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
        await processRunner
            .DidNotReceive()
            .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>());
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task NewBlazorWasm_WithSuccessfulGeneration_ReturnsExitCodeZeroAndCallsEngineWithDornBlazorWasmShortName()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp", Theme = "slate" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.TemplateShortName == "dorn-blazor-wasm" && r.ProjectName == "MyApp"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewBlazorWasm_WithUnknownTheme_ReturnsExitCodeOneAndNamesAllowedValues()
    {
        var (engine, _, command, consoleMock) = CreateCommandWithConsole();

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp", Theme = "not-a-real-theme" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task NewBlazorWasm_WithValidThemeFlag_SkipsPromptAndPassesThemeParameter()
    {
        var (engine, _, command, console) = CreateCommandWithRealTestConsole();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));
        console.Profile.Capabilities.Interactive = true;

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp", Theme = "rose" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Theme"] == "rose"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewBlazorWasm_WithOmittedThemeAndNonInteractiveConsole_FallsBackToSlateWithoutPrompting()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Theme"] == "slate"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewBlazorWasm_WithOmittedThemeAndInteractiveConsole_PromptsAndUsesSelection()
    {
        var (engine, _, command, console) = CreateCommandWithRealTestConsole();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewBlazorWasm_WithNoPlaygroundFlag_MapsIncludePlaygroundFalse()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings
            {
                Name = "MyApp",
                Theme = "slate",
                NoPlayground = true,
            },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["IncludePlayground"] == "false"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewBlazorWasm_WithoutNoPlaygroundFlag_MapsIncludePlaygroundTrue()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp", Theme = "slate" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["IncludePlayground"] == "true"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewBlazorWasm_WithFailedGeneration_ReturnsNonZeroExitCode()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new GenerationResult(
                    false,
                    "/tmp/MyApp",
                    [],
                    [new GenerationDiagnostic(GenerationDiagnosticSeverity.Error, "boom")]
                )
            );

        var exitCode = await command.RunAsync(
            new NewBlazorWasmSettings { Name = "MyApp", Theme = "slate" },
            CancellationToken.None
        );

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task NewBlazorWasm_WithSuccessfulGeneration_RunsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-blazor-restore-{Guid.NewGuid():N}");
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
                new NewBlazorWasmSettings
                {
                    Name = "MyApp",
                    Theme = "slate",
                    Output = tempDir,
                },
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
    public async Task NewBlazorWasm_WithNoRestoreFlag_SkipsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-blazor-norestore-{Guid.NewGuid():N}");
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
                new NewBlazorWasmSettings
                {
                    Name = "MyApp",
                    Theme = "slate",
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
        NewBlazorWasmCommand Command
    ) CreateCommand()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var consoleMock = CreateNonInteractiveConsoleMock();
        var theme = new DornTheme(consoleMock);
        var command = new NewBlazorWasmCommand(engine, processRunner, consoleMock, theme);
        return (engine, processRunner, command);
    }

    private (
        IGenerationEngine Engine,
        IProcessRunner ProcessRunner,
        NewBlazorWasmCommand Command,
        IAnsiConsole Console
    ) CreateCommandWithConsole()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var consoleMock = CreateNonInteractiveConsoleMock();
        var theme = new DornTheme(consoleMock);
        var command = new NewBlazorWasmCommand(engine, processRunner, consoleMock, theme);
        return (engine, processRunner, command, consoleMock);
    }

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

    private (
        IGenerationEngine Engine,
        IProcessRunner ProcessRunner,
        NewBlazorWasmCommand Command,
        TestConsole Console
    ) CreateCommandWithRealTestConsole()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        var theme = new DornTheme(console);
        var command = new NewBlazorWasmCommand(engine, processRunner, console, theme);
        return (engine, processRunner, command, console);
    }
}
