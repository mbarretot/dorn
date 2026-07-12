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
}
