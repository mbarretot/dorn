using System.Xml.Linq;
using Dorn.Cli.Commands.Coverage;
using Dorn.Cli.Coverage;
using Dorn.Cli.Projects;
using Dorn.Cli.Testing;
using Dorn.Cli.Theming;
using NSubstitute;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

///<summary>Tests for <see cref="CoverageCommand"/>: tier dispatch → freshest-per-tier discovery → merge → threshold gate → table. Drives RunAsync directly (CommandAppTester removed in Spectre.Console.Cli 0.55.0).</summary>
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
        var (_, console, command) = CreateCommand();
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("IncludeTests=false", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenAllTiersPassAndAboveThreshold_ReturnsZero()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, command) = CreateCommand(
            specs: [TierSpec("MyProject.Application.Tests")],
            writeReports: () =>
                CreateTierReport(
                    "MyProject.Application.Tests",
                    SimpleClass("MyApp", "Widget", "Widget.cs", totalLines: 100, coveredLines: 85)
                )
        );
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("85.00%", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenBelowThreshold_ReturnsExitOne()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, command) = CreateCommand(
            specs: [TierSpec("MyProject.Application.Tests")],
            writeReports: () =>
                CreateTierReport(
                    "MyProject.Application.Tests",
                    SimpleClass("MyApp", "Widget", "Widget.cs", totalLines: 100, coveredLines: 50)
                )
        );
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("50.00%", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenAtThreshold_ReturnsZero()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, command) = CreateCommand(
            specs: [TierSpec("MyProject.Application.Tests")],
            writeReports: () =>
                CreateTierReport(
                    "MyProject.Application.Tests",
                    SimpleClass("MyApp", "Widget", "Widget.cs", totalLines: 100, coveredLines: 80)
                )
        );
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("80.00%", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenTestsFail_ReturnsExitOneWithoutThreshold()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, command) = CreateCommand(allSucceeded: false);
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("coverage report not generated", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_WhenNoCoverageReport_ReturnsExitOne()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, command) = CreateCommand();
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("No coverage report found", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_ArchitectureTierZeroButApplicationNinety_MergesAndPassesThreshold()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Architecture.Tests");
        var (_, console, command) = CreateCommand(
            specs:
            [
                TierSpec("MyProject.Application.Tests"),
                TierSpec("MyProject.Architecture.Tests"),
            ],
            writeReports: () =>
            {
                CreateTierReport(
                    "MyProject.Application.Tests",
                    SimpleClass(
                        "MyApp",
                        "MyApp.Services.Widget",
                        "Services/Widget.cs",
                        totalLines: 10,
                        coveredLines: 9
                    )
                );
                CreateTierReport(
                    "MyProject.Architecture.Tests",
                    SimpleClass(
                        "MyApp",
                        "MyApp.Services.Widget",
                        "Services/Widget.cs",
                        totalLines: 10,
                        coveredLines: 0
                    )
                );
            }
        );
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("90.00%", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_OnlyStaleReportsFromPreviousRun_ReturnsExitOneAndIgnoresThem()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTierReport(
            "MyProject.Application.Tests",
            SimpleClass("MyApp", "Widget", "Widget.cs", totalLines: 10, coveredLines: 9)
        );
        BackdateAllReports(TimeSpan.FromMinutes(10));
        var (_, console, command) = CreateCommand(specs: [TierSpec("MyProject.Application.Tests")]);
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("No coverage report found", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_PartialTierReports_MergesAvailableAndWarnsAboutMissingTier()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        CreateTestsDir("MyProject.Architecture.Tests");
        var (_, console, command) = CreateCommand(
            specs:
            [
                TierSpec("MyProject.Application.Tests"),
                TierSpec("MyProject.Architecture.Tests"),
            ],
            writeReports: () =>
                CreateTierReport(
                    "MyProject.Application.Tests",
                    SimpleClass("MyApp", "Widget", "Widget.cs", totalLines: 10, coveredLines: 9)
                )
        );
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("MyProject.Architecture.Tests", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_DefaultTable_ShowsOnlyBelowThresholdAscendingCappedAtFifteenRows()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var belowThreshold = Enumerable
            .Range(1, 18)
            .Select(i =>
                SimpleClass(
                    "MyApp",
                    $"BelowClass{i:D2}",
                    $"BelowClass{i:D2}.cs",
                    totalLines: 10,
                    coveredLines: 0
                )
            )
            .ToArray();
        var aboveThreshold = SimpleClass(
            "MyApp",
            "AboveClass",
            "AboveClass.cs",
            totalLines: 10,
            coveredLines: 10
        );
        var (_, console, command) = CreateCommand(
            specs: [TierSpec("MyProject.Application.Tests")],
            writeReports: () =>
                CreateTierReport("MyProject.Application.Tests", [.. belowThreshold, aboveThreshold])
        );
        var settings = new CoverageSettings { Project = _tempRoot };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain("AboveClass", console.Output);
        Assert.Contains("+3 more below threshold", console.Output);
    }

    [Fact]
    public async Task CoverageCommand_AllFlag_ShowsEveryClassRegardlessOfThreshold()
    {
        CreateSolution("MyProject.slnx");
        CreateWebApi("MyProject.WebApi");
        CreateTestsDir("MyProject.Application.Tests");
        var (_, console, command) = CreateCommand(
            specs: [TierSpec("MyProject.Application.Tests")],
            writeReports: () =>
                CreateTierReport(
                    "MyProject.Application.Tests",
                    SimpleClass(
                        "MyApp",
                        "LowClass",
                        "LowClass.cs",
                        totalLines: 10,
                        coveredLines: 0
                    ),
                    SimpleClass(
                        "MyApp",
                        "HighClass",
                        "HighClass.cs",
                        totalLines: 10,
                        coveredLines: 10
                    )
                )
        );
        var settings = new CoverageSettings { Project = _tempRoot, All = true };

        var exitCode = await command.RunAsync(settings, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("LowClass", console.Output);
        Assert.Contains("HighClass", console.Output);
    }

    private (IDotnetTestRunner Runner, TestConsole Console, CoverageCommand Command) CreateCommand(
        IReadOnlyList<CapturedProcessSpec>? specs = null,
        bool allSucceeded = true,
        Action? writeReports = null
    )
    {
        var testRunner = Substitute.For<IDotnetTestRunner>();
        testRunner
            .RunAsync(
                Arg.Any<ProjectContext>(),
                Arg.Any<DatabaseProvider>(),
                Arg.Any<IReadOnlyList<TestTier>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ci =>
            {
                writeReports?.Invoke();
                return new TestRunResult(specs ?? [], allSucceeded);
            });

        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.Interactive = false;
        var theme = new DornTheme(console);
        var resolver = new ProjectContextResolver();
        var reporter = new CoverageReporter();
        var command = new CoverageCommand(resolver, testRunner, reporter, console, theme);

        return (testRunner, console, command);
    }

    private CapturedProcessSpec TierSpec(string tierDirName) =>
        new(
            "dotnet",
            [
                "test",
                "path",
                "--collect:XPlat Code Coverage",
                "--results-directory",
                Path.Combine(_tempRoot, "TestResults", tierDirName),
                "--no-build",
            ],
            _tempRoot
        );

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

    private void CreateTierReport(string tierDirName, params ClassSpec[] classes)
    {
        var dir = Path.Combine(_tempRoot, "TestResults", tierDirName, Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "coverage.cobertura.xml"), BuildCobertura(classes));
    }

    private void BackdateAllReports(TimeSpan age)
    {
        var testResultsDir = Path.Combine(_tempRoot, "TestResults");
        foreach (
            var file in Directory.EnumerateFiles(
                testResultsDir,
                "coverage.cobertura.xml",
                new EnumerationOptions { RecurseSubdirectories = true }
            )
        )
        {
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow - age);
        }
    }

    private static ClassSpec SimpleClass(
        string assembly,
        string className,
        string fileName,
        int totalLines,
        int coveredLines
    ) =>
        new(
            assembly,
            className,
            fileName,
            [.. Enumerable.Range(1, totalLines).Select(n => (n, n <= coveredLines ? 1 : 0))]
        );

    private static string BuildCobertura(ClassSpec[] classes)
    {
        var packages = classes
            .GroupBy(c => c.Assembly)
            .Select(group => new XElement(
                "package",
                new XAttribute("name", group.Key),
                new XElement(
                    "classes",
                    group.Select(c => new XElement(
                        "class",
                        new XAttribute("name", c.ClassName),
                        new XAttribute("filename", c.FileName),
                        new XElement(
                            "lines",
                            c.Lines.Select(l => new XElement(
                                "line",
                                new XAttribute("number", l.Number),
                                new XAttribute("hits", l.Hits)
                            ))
                        )
                    ))
                )
            ));

        var doc = new XDocument(
            new XElement(
                "coverage",
                new XAttribute("line-rate", "0"),
                new XElement("packages", packages)
            )
        );

        return doc.ToString();
    }

    private sealed record ClassSpec(
        string Assembly,
        string ClassName,
        string FileName,
        (int Number, int Hits)[] Lines
    );
}
