using Dorn.Abstractions.Generation;
using Dorn.Cli.Commands.New;
using Dorn.Cli.Execution;
using Dorn.Cli.Theming;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

///<summary>Covers the optional <see cref="NewWebApiSettings.Name"/> wizard-validation behavior: live re-prompt when interactive, dorn-owned error when not.</summary>
public class NewWebApiSettingsTests
{
    [Fact]
    public async Task NewWebApi_WithOmittedNameAndNonInteractiveConsole_ReturnsExitCodeOneWithDornOwnedMessage()
    {
        var (engine, _, command, console) = CreateCommandWithRealTestConsole();

        var exitCode = await command.RunAsync(new NewWebApiSettings(), CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("Project name is required", console.Output);
        await engine
            .DidNotReceive()
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewWebApi_WithOmittedNameAndInteractiveConsole_RePromptsUntilValidThenGenerates()
    {
        var (engine, _, command, console) = CreateCommandWithRealTestConsole();
        engine
            .GenerateAsync(Arg.Any<GenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GenerationResult(true, "/tmp/MyApp", ["Program.cs"], []));
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("1bad");
        console.Input.PushTextWithEnter("MyApp");

        var exitCode = await command.RunAsync(
            new NewWebApiSettings
            {
                Orm = "efcore",
                Database = "sqlite",
                Orchestrator = "aspire",
                Auth = "none",
            },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("cannot start with a digit", console.Output);
        await engine
            .Received(1)
            .GenerateAsync(
                Arg.Is<GenerationRequest>(r => r.ProjectName == "MyApp"),
                Arg.Any<CancellationToken>()
            );
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
