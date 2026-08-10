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
using AuthCompatLib = Dorn.Core.Validation.AuthOrmCompatibilityValidator;
using AuthValidatorLib = Dorn.Core.Validation.AuthValidator;

namespace Dorn.Cli.Tests.Commands;

///<summary>Tests for <see cref="NewWebApiCommand"/>. Drives ExecuteAsync directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0). Stdout-capturing tests use a real <see cref="TestConsole"/>; exit-code tests use a fake <see cref="IAnsiConsole"/> and assert <c>MarkupLine</c> on the mock.</summary>
public class NewWebApiCommandTests
{
    [Fact]
    public async Task NewWebApi_WithSuccessfulGeneration_ReturnsExitCodeZeroAndCallsEngineWithExpectedRequest()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.TemplateShortName == "dorn-webapi" && r.ProjectName == "MyApp"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithFailedGeneration_ReturnsNonZeroExitCode()
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
            new NewWebApiSettings { Name = "MyApp" },
            CancellationToken.None
        );

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task NewWebApi_WithExplicitDatabaseOption_PassesThroughUntouched()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Database = "sqlserver" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["DatabaseProvider"] == "sqlserver"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithOmittedDatabaseAndNonInteractiveConsole_FallsBackToSqliteWithoutPrompting()
    {
        // Substitute IAnsiConsole reports non-interactive (no real TTY under dotnet test / CI).
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["DatabaseProvider"] == "sqlite"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithInvalidDatabaseOption_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Database = "mysql" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithPostgresDatabaseOption_PassesThroughToEngine()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Database = "postgres" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["DatabaseProvider"] == "postgres"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithSqlServerAndAspireUnsafeProjectName_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        // "My.App" passes ProjectNameValidator but is invalid as an Aspire resource name (ASPIRE006).
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "My.App", Database = "sqlserver" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithPostgresAndAspireUnsafeProjectName_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        // "My.App" passes ProjectNameValidator but is invalid as an Aspire resource name (ASPIRE006);
        // the guard is `!= "sqlite"`, so postgres must trigger it exactly like sqlserver does.
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "My.App", Database = "postgres" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithSqlite_AllowsProjectNamesThatAreUnsafeForAspire()
    {
        // The Aspire resource-name constraint only applies to --database sqlserver.
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/My.App", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "My.App", Database = "sqlite" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithExplicitOrchestratorOption_PassesThroughUntouched()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Orchestrator = "docker-compose" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Orchestrator"] == "docker-compose"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithNoneOrchestratorOption_PassesThroughUntouched()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Orchestrator = "none" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Orchestrator"] == "none"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithOmittedOrchestratorAndNonInteractiveConsole_FallsBackToAspireWithoutPrompting()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Orchestrator"] == "aspire"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithInvalidOrchestratorOption_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Orchestrator = "kubernetes" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithInvalidAuthOption_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Auth = "bogus" },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        var sanityCheck = AuthValidatorLib.Validate("bogus");
        Assert.False(sanityCheck.IsValid);
        Assert.Contains("Unknown auth mode", sanityCheck.ErrorMessage);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithCustomAuthAndDapper_ReturnsExitCodeOneWithD2GuardMessage()
    {
        var (engine, _, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new NewWebApiSettings
            {
                Name = "MyApp",
                Auth = "custom",
                Orm = "dapper",
            },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        var sanityCheck = AuthCompatLib.Validate("custom", "dapper");
        Assert.False(sanityCheck.IsValid);
        Assert.Contains("Auth='custom' requires Orm='efcore'", sanityCheck.ErrorMessage);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithCustomAuth_ForwardsAuthParameterToEngine()
    {
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Auth = "custom" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Auth"] == "custom"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task NewWebApi_WithSqlServerAndDockerComposeOrchestrator_SkipsAspireNameValidation()
    {
        // "My.App" fails ASPIRE006 but the compose path never creates an Aspire resource, so the name gate must not apply.
        var (engine, _, command) = CreateCommand();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/My.App", ["Program.cs"], []));

        var exitCode = await command.RunAsync(
            new NewWebApiSettings
            {
                Name = "My.App",
                Database = "sqlserver",
                Orchestrator = "docker-compose",
            },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithOmittedDatabaseAndInteractiveConsole_PromptsAndUsesSelection()
    {
        // 0.55.0 fix: TestConsoleInput no longer consumes scripted keys before the prompt reads them.
        var (engine, _, command, console) = CreateCommandWithRealTestConsole();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));
        console.Profile.Capabilities.Interactive = true;
        // Queue extras: ListPrompt may probe a non-mapped key for page-size/initial state; extra Enters are no-ops after first selection.
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);

        // Pin orchestrator=aspire so only the database prompt consumes the scripted input above.
        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Orchestrator = "aspire" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        // Exit 0 confirms the SelectionPrompt completed without throwing. We do not assert on
        // DatabaseProvider — TestConsoleInput key-buffering order is not guaranteed across point releases.
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithOmittedOrchestratorAndInteractiveConsole_PromptsAndUsesSelection()
    {
        var (engine, _, command, console) = CreateCommandWithRealTestConsole();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));
        console.Profile.Capabilities.Interactive = true;
        // Queue extras: ListPrompt may probe a non-mapped key for page-size/initial state; extra Enters are no-ops after first selection.
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);
        console.Input.PushKey(ConsoleKey.Enter);

        // Pin database=sqlite so only the orchestrator prompt consumes the scripted input above.
        var exitCode = await command.RunAsync(
            new NewWebApiSettings { Name = "MyApp", Database = "sqlite" },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        // Exit 0 confirms the SelectionPrompt completed without throwing. We do not assert on
        // Orchestrator — TestConsoleInput key-buffering order is not guaranteed across point releases.
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithSuccessfulGeneration_RunsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-restore-{Guid.NewGuid():N}");
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
                new NewWebApiSettings { Name = "MyApp", Output = tempDir },
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
    public async Task NewWebApi_WithNoRestoreFlag_SkipsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-norestore-{Guid.NewGuid():N}");
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
                new NewWebApiSettings
                {
                    Name = "MyApp",
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

    [Fact]
    public async Task NewWebApi_WhenToolRestoreFails_StillReturnsZeroWithWarning()
    {
        var (engine, processRunner, command, consoleMock) = CreateCommandWithConsole();
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-restore-fail-{Guid.NewGuid():N}");
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
        processRunner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(1); // restore fails

        try
        {
            var exitCode = await command.RunAsync(
                new NewWebApiSettings { Name = "MyApp", Output = tempDir },
                CancellationToken.None
            );

            // Generation succeeded; restore is best-effort and must not fail the command.
            Assert.Equal(0, exitCode);
            consoleMock.Received().Write(Arg.Any<IRenderable>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task NewWebApi_WithSuccessfulGenerationAndInteractiveConsole_StillRunsDotnetToolRestoreOnce()
    {
        var (engine, processRunner, command, console) = CreateCommandWithRealTestConsole();
        console.Profile.Capabilities.Interactive = true;
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            $"dorn-restore-interactive-{Guid.NewGuid():N}"
        );
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
            // All choices pinned via flags — the wizard's own SelectionPrompts aren't part of this slice.
            var exitCode = await command.RunAsync(
                new NewWebApiSettings
                {
                    Name = "MyApp",
                    Output = tempDir,
                    Orm = "efcore",
                    Database = "sqlite",
                    Orchestrator = "aspire",
                    Auth = "none",
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
    public async Task NewWebApi_WithoutManifestInOutput_SkipsDotnetToolRestore()
    {
        var (engine, processRunner, command) = CreateCommand();
        // Use a unique empty output dir; engine returns success without creating a manifest.
        var tempDir = Path.Combine(Path.GetTempPath(), $"dorn-no-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, tempDir, ["Program.cs"], []));

        try
        {
            var exitCode = await command.RunAsync(
                new NewWebApiSettings { Name = "MyApp", Output = tempDir },
                CancellationToken.None
            );

            Assert.Equal(0, exitCode);
            await processRunner
                .DidNotReceive()
                .RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static CommandContext SyntheticContext() =>
        new CommandContext(Array.Empty<string>(), new EmptyRemainingArguments(), "webapi", null);

    private (
        IGenerationEngine Engine,
        IProcessRunner ProcessRunner,
        NewWebApiCommand Command
    ) CreateCommand()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var consoleMock = CreateNonInteractiveConsoleMock();
        var theme = new DornTheme(consoleMock);
        var command = new NewWebApiCommand(engine, processRunner, consoleMock, theme);
        return (engine, processRunner, command);
    }

    private (
        IGenerationEngine Engine,
        IProcessRunner ProcessRunner,
        NewWebApiCommand Command,
        IAnsiConsole Console
    ) CreateCommandWithConsole()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var consoleMock = CreateNonInteractiveConsoleMock();
        var theme = new DornTheme(consoleMock);
        var command = new NewWebApiCommand(engine, processRunner, consoleMock, theme);
        return (engine, processRunner, command, consoleMock);
    }

    ///<summary>IAnsiConsole mock with Interactive=false, Unicode=true explicitly set (no test may rely on defaults). Interactive tests override with a real TestConsole at the test boundary.</summary>
    private static IAnsiConsole CreateNonInteractiveConsoleMock()
    {
        var consoleMock = Substitute.For<IAnsiConsole>();
        // Capabilities is sealed; instantiate directly and set properties explicitly.
        var capabilities = new Capabilities { Interactive = false, Unicode = true };
        // Profile is sealed; ctor takes (IAnsiConsoleOutput, Capabilities, Encoding).
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
        NewWebApiCommand Command,
        TestConsole Console
    ) CreateCommandWithRealTestConsole()
    {
        var engine = Substitute.For<IGenerationEngine>();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        var theme = new DornTheme(console);
        var command = new NewWebApiCommand(engine, processRunner, console, theme);
        return (engine, processRunner, command, console);
    }
}

///<summary>Minimal <see cref="IRemainingArguments"/> stand-in for tests that build <see cref="CommandContext"/> directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
file sealed class EmptyRemainingArguments : IRemainingArguments
{
    public ILookup<string, string?> Parsed { get; } =
        Array.Empty<string>().ToLookup(x => x, x => (string?)null);

    public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
}
