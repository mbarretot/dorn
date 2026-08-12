using System.Text.Json;
using Dorn.Cli.Commands.Test;
using Dorn.Cli.Output;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Tests.Output;
using Dorn.Cli.Theming;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

///<summary>Tests for <see cref="TestCommand"/>. Runs RunAsync directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
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
        var (runner, _, _, command) = CreateCommand();
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
                Arg.Any<CancellationToken>(),
                Arg.Any<bool>()
            );
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("application")]
    public async Task TestCommand_WithUnitOrApplicationTierFilter_ResolvesOnlyApplicationTier(
        string tierFilter
    )
    {
        var (runner, _, _, command) = CreateCommand();
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new TestSettings { Tier = tierFilter, Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        await runner
            .Received(1)
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Is<IReadOnlyList<TestTier>>(t =>
                    t.SequenceEqual(new[] { TestTier.Application })
                ),
                Arg.Any<CancellationToken>(),
                Arg.Any<bool>()
            );
    }

    [Fact]
    public async Task TestCommand_WithUnknownTierFilter_FallsBackToAllTiers()
    {
        var (runner, _, _, command) = CreateCommand();
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new TestSettings { Tier = "bogus", Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        await runner
            .Received(1)
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Is<IReadOnlyList<TestTier>>(t =>
                    t.Count == 2
                    && t.Contains(TestTier.Application)
                    && t.Contains(TestTier.Integration)
                ),
                Arg.Any<CancellationToken>(),
                Arg.Any<bool>()
            );
    }

    [Fact]
    public async Task TestCommand_WithoutTierFilter_RunsAllTiers()
    {
        var (_, _, _, command) = CreateCommand();
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
        var (_, console, _, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new TestSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("No test tiers found", console.Output);
    }

    [Fact]
    public async Task TestCommand_WithoutProjectOption_UsesCurrentDirectory()
    {
        var (_, _, _, command) = CreateCommand();
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

    [Fact]
    public async Task TestCommand_TableModeSuccess_PrintsNothing()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, _, command) = CreateCommand(
            tierResults: [new TierRunResult(TestTier.Application, true, 1, 1, 0, 0, 0.1)]
        );
        var settings = new TestSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, console.Output);
    }

    [Fact]
    public async Task TestCommand_TableModeTestsFailed_PrintsExistingErrorMessage()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, _, command) = CreateCommand(
            allSucceeded: false,
            tierResults: [new TierRunResult(TestTier.Application, false, 1, 0, 1, 0, 0.1)]
        );
        var settings = new TestSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("One or more tier runs failed.", console.Output);
    }

    [Fact]
    public async Task TestCommand_InvalidFormat_ReturnsExitOneWithErrorAndNoJson()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, writer, command) = CreateCommand();
        var settings = new TestSettings { Project = _tempRoot, Format = "xml" };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Empty(writer.Lines);
        Assert.Contains("Unknown format", console.Output);
    }

    [Fact]
    public async Task TestCommand_FormatJsonAllTiersClean_OutcomeOkCountsAvailableTrueNonNullCounts()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        var (_, console, writer, command) = CreateCommand(
            tierResults:
            [
                new TierRunResult(TestTier.Application, true, 10, 10, 0, 0, 1.5),
                new TierRunResult(TestTier.Integration, true, 5, 5, 0, 0, 2.0),
            ]
        );
        var settings = new TestSettings { Project = _tempRoot, Format = "json" };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, console.Output);
        var report = DeserializeSingle(writer);
        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal("test", report.Command);
        Assert.True(report.Success);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal("ok", report.Data.Outcome);
        Assert.Equal(2, report.Data.Tiers.Count);
        Assert.All(report.Data.Tiers, t => Assert.True(t.CountsAvailable));
        Assert.Equal(15, report.Data.TotalTests);
        Assert.Equal(15, report.Data.PassedTests);
        Assert.Equal(0, report.Data.FailedTests);
        Assert.Equal(0, report.Data.SkippedTests);
        Assert.Empty(report.Data.ReportUnavailableTiers);
    }

    [Fact]
    public async Task TestCommand_FormatJsonOneTierFails_ThatTierFailedOthersPassedTopLevelTestsFailed()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        var (_, console, writer, command) = CreateCommand(
            allSucceeded: false,
            tierResults:
            [
                new TierRunResult(TestTier.Application, true, 10, 10, 0, 0, 1.0),
                new TierRunResult(TestTier.Integration, false, 5, 3, 2, 0, 1.0),
            ]
        );
        var settings = new TestSettings { Project = _tempRoot, Format = "json" };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, console.Output);
        var report = DeserializeSingle(writer);
        Assert.Equal("tests-failed", report.Data.Outcome);
        Assert.False(report.Success);
        var appTier = report.Data.Tiers.Single(t => t.Tier == "Application");
        var intTier = report.Data.Tiers.Single(t => t.Tier == "Integration");
        Assert.Equal("passed", appTier.Outcome);
        Assert.Equal("failed", intTier.Outcome);
    }

    [Fact]
    public async Task TestCommand_FormatJsonTierPassesButCountsUnavailable_PassedCountsAvailableFalseInReportUnavailableTiers()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, writer, command) = CreateCommand(
            tierResults:
            [
                new TierRunResult(TestTier.Application, true, null, null, null, null, null),
            ]
        );
        var settings = new TestSettings { Project = _tempRoot, Format = "json" };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, console.Output);
        var report = DeserializeSingle(writer);
        Assert.Equal("ok", report.Data.Outcome);
        Assert.True(report.Success);
        var tier = Assert.Single(report.Data.Tiers);
        Assert.Equal("passed", tier.Outcome);
        Assert.False(tier.CountsAvailable);
        Assert.Null(tier.Total);
        Assert.Contains("Application", report.Data.ReportUnavailableTiers);
        Assert.Null(report.Data.TotalTests);
    }

    [Fact]
    public async Task TestCommand_FormatJsonMixedAvailability_TotalsSumOnlyAvailableTiers()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        var (_, _, writer, command) = CreateCommand(
            tierResults:
            [
                new TierRunResult(TestTier.Application, true, 10, 10, 0, 0, 1.0),
                new TierRunResult(TestTier.Integration, true, null, null, null, null, null),
            ]
        );
        var settings = new TestSettings { Project = _tempRoot, Format = "json" };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var report = DeserializeSingle(writer);
        Assert.Equal(10, report.Data.TotalTests);
        Assert.Equal(10, report.Data.PassedTests);
        Assert.Equal(["Integration"], report.Data.ReportUnavailableTiers);
    }

    [Fact]
    public async Task TestCommand_FormatJsonNoTiers_ExitZeroOutcomeNoTestTiersEmptyArray()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var (_, console, writer, command) = CreateCommand();
        var settings = new TestSettings { Project = _tempRoot, Format = "json" };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, console.Output);
        var report = DeserializeSingle(writer);
        Assert.Equal("no-test-tiers", report.Data.Outcome);
        Assert.Empty(report.Data.Tiers);
        Assert.True(report.Success);
    }

    [Fact]
    public async Task TestCommand_FormatJsonUnrecognizedTierFilter_EchoesRawValueRecognizedFalseAllTiersRun()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        var (runner, _, writer, command) = CreateCommand(
            tierResults:
            [
                new TierRunResult(TestTier.Application, true, 1, 1, 0, 0, 0.1),
                new TierRunResult(TestTier.Integration, true, 1, 1, 0, 0, 0.1),
            ]
        );
        var settings = new TestSettings
        {
            Project = _tempRoot,
            Format = "json",
            Tier = "integraton",
        };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var report = DeserializeSingle(writer);
        Assert.Equal("integraton", report.Data.TierFilter);
        Assert.False(report.Data.TierFilterRecognized);
        await runner
            .Received(1)
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Is<IReadOnlyList<TestTier>>(t => t.Count == 2),
                Arg.Any<CancellationToken>(),
                Arg.Any<bool>()
            );
    }

    [Fact]
    public async Task TestCommand_FormatJsonTierOmitted_TierFilterAndRecognizedAreNull()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, _, writer, command) = CreateCommand(
            tierResults: [new TierRunResult(TestTier.Application, true, 1, 1, 0, 0, 0.1)]
        );
        var settings = new TestSettings { Project = _tempRoot, Format = "json" };

        await command.RunAsync(settings, CancellationToken.None);

        var report = DeserializeSingle(writer);
        Assert.Null(report.Data.TierFilter);
        Assert.Null(report.Data.TierFilterRecognized);
    }

    [Fact]
    public async Task TestCommand_FormatJsonRecognizedTierAlias_TierFilterRecognizedTrue()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Integration.Tests");
        var (_, _, writer, command) = CreateCommand(
            tierResults: [new TierRunResult(TestTier.Application, true, 1, 1, 0, 0, 0.1)]
        );
        var settings = new TestSettings
        {
            Project = _tempRoot,
            Format = "json",
            Tier = "unit",
        };

        await command.RunAsync(settings, CancellationToken.None);

        var report = DeserializeSingle(writer);
        Assert.Equal("unit", report.Data.TierFilter);
        Assert.True(report.Data.TierFilterRecognized);
    }

    private static CliEnvelope<TestReport> DeserializeSingle(RecordingCliOutputWriter writer)
    {
        var line = Assert.Single(writer.Lines);
        var envelope = JsonSerializer.Deserialize<CliEnvelope<TestReport>>(line, CliJson.Options);
        Assert.NotNull(envelope);
        return envelope!;
    }

    private (
        IDotnetTestRunner Runner,
        TestConsole Console,
        RecordingCliOutputWriter Writer,
        TestCommand Command
    ) CreateCommand(IReadOnlyList<TierRunResult>? tierResults = null, bool allSucceeded = true)
    {
        var testRunner = Substitute.For<IDotnetTestRunner>();
        testRunner
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Any<IReadOnlyList<TestTier>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<bool>()
            )
            .Returns(new TestRunResult([], allSucceeded, tierResults ?? []));

        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.Interactive = false;
        var theme = new DornTheme(console);
        var resolver = new ProjectContextResolver();
        var writer = new RecordingCliOutputWriter();
        var command = new TestCommand(resolver, testRunner, theme, writer);

        return (testRunner, console, writer, command);
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
