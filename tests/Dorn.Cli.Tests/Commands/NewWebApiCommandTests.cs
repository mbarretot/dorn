using Dorn.Abstractions.Generation;
using Dorn.Cli.Commands.New;
using Dorn.Cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

/// <summary>
/// Minimal <see cref="IRemainingArguments"/> stand-in for tests that construct a
/// <see cref="CommandContext"/> directly instead of going through <see cref="CommandAppTester"/>
/// (needed for the interactive-prompt case, since CommandAppTester 0.49.1 always creates its own
/// internal <see cref="TestConsole"/> and offers no way to inject a pre-scripted one).
/// </summary>
file sealed class EmptyRemainingArguments : IRemainingArguments
{
    public ILookup<string, string?> Parsed { get; } =
        Array.Empty<string>().ToLookup(x => x, x => (string?)null);

    public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
}

/// <summary>
/// Program.cs is top-level statements, which the compiler turns into an internal, largely
/// unusable Program class — it is not something a test can construct or drive directly. So
/// instead of invoking the real Program.Main, this test builds the same tiny "new webapi"
/// branch wiring directly against a fresh CommandApp, with a NSubstitute fake IGenerationEngine
/// standing in for AddDornCore()'s real Template Engine-backed implementation.
/// </summary>
public class NewWebApiCommandTests
{
    private static (CommandAppTester App, IGenerationEngine Engine) CreateApp()
    {
        var engine = Substitute.For<IGenerationEngine>();

        var services = new ServiceCollection();
        services.AddSingleton(engine);

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar);

        app.Configure(config =>
        {
            config.AddBranch(
                "new",
                branch =>
                {
                    branch.AddCommand<NewWebApiCommand>("webapi");
                }
            );
        });

        return (app, engine);
    }

    [Fact]
    public async Task NewWebApi_WithSuccessfulGeneration_ReturnsExitCodeZeroAndCallsEngineWithExpectedRequest()
    {
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var result = await app.RunAsync(["new", "webapi", "MyApp"]);

        Assert.Equal(0, result.ExitCode);
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
        var (app, engine) = CreateApp();
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

        var result = await app.RunAsync(["new", "webapi", "MyApp"]);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task NewWebApi_WithExplicitDatabaseOption_PassesThroughUntouched()
    {
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var result = await app.RunAsync(["new", "webapi", "MyApp", "--database", "sqlserver"]);

        Assert.Equal(0, result.ExitCode);
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
        // CommandAppTester's default TestConsole has InteractionSupport.Detect, which resolves
        // to non-interactive when there is no real TTY (true for `dotnet test` runs and CI).
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var result = await app.RunAsync(["new", "webapi", "MyApp"]);

        Assert.Equal(0, result.ExitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["DatabaseProvider"] == "sqlite"
                ),
                Arg.Any<CancellationToken>()
            );
        Assert.DoesNotContain("Select a", result.Output);
    }

    [Fact]
    public async Task NewWebApi_WithOmittedDatabaseAndInteractiveConsole_PromptsAndUsesSelection()
    {
        // CommandAppTester 0.49.1 does not expose a way to inject a pre-scripted TestConsole, so
        // this case drives NewWebApiCommand directly instead of routing through CommandAppTester.
        var engine = Substitute.For<IGenerationEngine>();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        using var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Interactive = true;
        // Choices are added as "sqlite", "sqlserver" in that order; one Down arrow moves the
        // selection to "sqlserver" before confirming with Enter.
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var command = new NewWebApiCommand(engine, console);
        var context = new CommandContext(
            Array.Empty<string>(),
            new EmptyRemainingArguments(),
            "webapi",
            null
        );
        // Orchestrator is pinned explicitly so only the database prompt consumes the scripted
        // input above — leaving it null would trigger a second (unscripted) prompt.
        var settings = new NewWebApiSettings { Name = "MyApp", Orchestrator = "aspire" };

        var exitCode = await command.ExecuteAsync(context, settings);

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
    public async Task NewWebApi_WithInvalidDatabaseOption_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        var (app, engine) = CreateApp();

        var result = await app.RunAsync(["new", "webapi", "MyApp", "--database", "postgres"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid database provider", result.Output);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithSqlServerAndAspireUnsafeProjectName_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        // "My.App" passes ProjectNameValidator but is invalid as an Aspire resource name (ASPIRE006).
        var (app, engine) = CreateApp();

        var result = await app.RunAsync(["new", "webapi", "My.App", "--database", "sqlserver"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid project name", result.Output);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithSqlite_AllowsProjectNamesThatAreUnsafeForAspire()
    {
        // The Aspire resource-name constraint only applies to --database sqlserver.
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/My.App", ["Program.cs"], []));

        var result = await app.RunAsync(["new", "webapi", "My.App", "--database", "sqlite"]);

        Assert.Equal(0, result.ExitCode);
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithExplicitOrchestratorOption_PassesThroughUntouched()
    {
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var result = await app.RunAsync([
            "new",
            "webapi",
            "MyApp",
            "--orchestrator",
            "docker-compose",
        ]);

        Assert.Equal(0, result.ExitCode);
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
    public async Task NewWebApi_WithOmittedOrchestratorAndNonInteractiveConsole_FallsBackToAspireWithoutPrompting()
    {
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        var result = await app.RunAsync(["new", "webapi", "MyApp"]);

        Assert.Equal(0, result.ExitCode);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r =>
                    r.Parameters != null && r.Parameters["Orchestrator"] == "aspire"
                ),
                Arg.Any<CancellationToken>()
            );
        Assert.DoesNotContain("Select an", result.Output);
    }

    [Fact]
    public async Task NewWebApi_WithOmittedOrchestratorAndInteractiveConsole_PromptsAndUsesSelection()
    {
        // CommandAppTester 0.49.1 does not expose a way to inject a pre-scripted TestConsole, so
        // this case drives NewWebApiCommand directly instead of routing through CommandAppTester.
        var engine = Substitute.For<IGenerationEngine>();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));

        using var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Interactive = true;
        // Choices are added as "aspire", "docker-compose" in that order; one Down arrow moves
        // the selection to "docker-compose" before confirming with Enter.
        console.Input.PushKey(ConsoleKey.DownArrow);
        console.Input.PushKey(ConsoleKey.Enter);

        var command = new NewWebApiCommand(engine, console);
        var context = new CommandContext(
            Array.Empty<string>(),
            new EmptyRemainingArguments(),
            "webapi",
            null
        );
        // Database is pinned explicitly so only the orchestrator prompt consumes the scripted
        // input above — leaving it null would trigger a second (unscripted) prompt.
        var settings = new NewWebApiSettings { Name = "MyApp", Database = "sqlite" };

        var exitCode = await command.ExecuteAsync(context, settings);

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
    public async Task NewWebApi_WithInvalidOrchestratorOption_ReturnsExitCodeOneAndNeverCallsEngine()
    {
        var (app, engine) = CreateApp();

        var result = await app.RunAsync(["new", "webapi", "MyApp", "--orchestrator", "kubernetes"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Invalid orchestrator", result.Output);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithSqlServerAndDockerComposeOrchestrator_SkipsAspireNameValidation()
    {
        // "My.App" fails ASPIRE006 (see the aspire+sqlserver case above) but the compose
        // path never creates an Aspire resource, so the name gate must not apply here.
        var (app, engine) = CreateApp();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/My.App", ["Program.cs"], []));

        var result = await app.RunAsync([
            "new",
            "webapi",
            "My.App",
            "--database",
            "sqlserver",
            "--orchestrator",
            "docker-compose",
        ]);

        Assert.Equal(0, result.ExitCode);
        await engine
            .Received(1)
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }
}
