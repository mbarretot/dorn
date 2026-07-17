using Dorn.Cli.Commands.Coverage;
using Dorn.Cli.Coverage;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

///<summary>Tests for <see cref="CoverageCommand"/>: tier dispatch → test run → Cobertura parsing → threshold gate. Drives ExecuteAsync directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
public class CoverageCommandTests : IDisposable
{
    private readonly string _tempRoot;

    public CoverageCommandTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-covcmd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task CoverageCommand_WithoutTiers_ReturnsExitOneWithClearMessage()
    {
        var (_, consoleMock, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task CoverageCommand_WhenAllTiersPassAndAboveThreshold_ReturnsZero()
    {
        var (_, consoleMock, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateCoberturaReport(lineRate: 0.85);
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task CoverageCommand_WhenBelowThreshold_ReturnsExitOne()
    {
        var (_, consoleMock, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateCoberturaReport(lineRate: 0.50);
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task CoverageCommand_WhenAtThreshold_ReturnsZero()
    {
        var (_, consoleMock, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateCoberturaReport(lineRate: 0.80);
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task CoverageCommand_WhenTestsFail_ReturnsExitOneWithoutThreshold()
    {
        var (_, consoleMock, command) = CreateCommandWithFailingRunner();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public async Task CoverageCommand_WhenNoCoverageReport_ReturnsExitOne()
    {
        var (_, consoleMock, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        // No Cobertura report created.
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        consoleMock.Received().Write(Arg.Any<IRenderable>());
    }

    private static CommandContext SyntheticContext(string name) =>
        new CommandContext(Array.Empty<string>(), new EmptyRemainingArgs(), name, null);

    private (
        IDotnetTestRunner Runner,
        IAnsiConsole Console,
        CoverageCommand Command
    ) CreateCommand()
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
        var reporter = new CoverageReporter();
        var command = new CoverageCommand(resolver, testRunner, reporter, consoleMock);

        return (testRunner, consoleMock, command);
    }

    private (
        IDotnetTestRunner Runner,
        IAnsiConsole Console,
        CoverageCommand Command
    ) CreateCommandWithFailingRunner()
    {
        var testRunner = Substitute.For<IDotnetTestRunner>();
        testRunner
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Any<IReadOnlyList<TestTier>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new TestRunResult([], AllSucceeded: false));

        var consoleMock = Substitute.For<IAnsiConsole>();
        var resolver = new ProjectContextResolver();
        var reporter = new CoverageReporter();
        var command = new CoverageCommand(resolver, testRunner, reporter, consoleMock);

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

    private void CreateCoberturaReport(double lineRate)
    {
        var guid = Guid.NewGuid().ToString();
        var dir = Path.Combine(_tempRoot, "TestResults", guid);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "coverage.cobertura.xml"),
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + $"<coverage line-rate=\"{lineRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" "
                + $"branch-rate=\"0.5\" version=\"1.9\" timestamp=\"0\" lines-covered=\"0\" lines-valid=\"0\" "
                + $"branches-covered=\"0\" branches-valid=\"0\"></coverage>"
        );
    }
}

///<summary>Minimal <see cref="IRemainingArguments"/> stand-in for tests that build <see cref="CommandContext"/> directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
file sealed class EmptyRemainingArgs : IRemainingArguments
{
    public ILookup<string, string?> Parsed { get; } =
        Array.Empty<string>().ToLookup(x => x, x => (string?)null);

    public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
}
