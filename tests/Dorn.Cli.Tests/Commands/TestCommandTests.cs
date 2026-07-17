using Dorn.Cli.Commands.Test;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

///<summary>Tests for <see cref="TestCommand"/>. Runs ExecuteAsync directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
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

    [Fact]
    public async Task TestCommand_WithTierFilter_OnlyRunsThatTier()
    {
        var (runner, _, command) = CreateCommand();
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new TestSettings { Tier = "integration", Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
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
        var (_, _, command) = CreateCommand();
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new TestSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task TestCommand_WithoutTestDirectories_PrintsClearMessageAndReturnsZero()
    {
        var (_, consoleMock, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new TestSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task TestCommand_WithoutProjectOption_UsesCurrentDirectory()
    {
        var (_, _, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempRoot);
            var settings = new TestSettings { Project = null };
            var exitCode = await command.RunAsync(settings, CancellationToken.None);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    private static CommandContext SyntheticContext(string name) =>
        new CommandContext(Array.Empty<string>(), new EmptyRemainingArgs(), name, null);

    private (IDotnetTestRunner Runner, IAnsiConsole Console, TestCommand Command) CreateCommand()
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

        var consoleMock = Substitute.For<IAnsiConsole>();
        var resolver = new ProjectContextResolver();
        var command = new TestCommand(resolver, testRunner, consoleMock);

        return (testRunner, consoleMock, command);
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

///<summary>Minimal <see cref="IRemainingArguments"/> stand-in for tests that build <see cref="CommandContext"/> directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
file sealed class EmptyRemainingArgs : IRemainingArguments
{
    public ILookup<string, string?> Parsed { get; } =
        Array.Empty<string>().ToLookup(x => x, x => (string?)null);

    public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
}
