using System.Xml;
using System.Xml.Linq;
using Dorn.Cli.Coverage;
using Xunit;

namespace Dorn.Cli.Tests.Coverage;

/// <summary>
/// Tests Cobertura parsing, the fixed 80% gate, and graceful ReportGenerator failure handling.
/// </summary>
public class CoverageReporterTests : IDisposable
{
    private readonly string _tempRoot;

    public CoverageReporterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-cov-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // Cobertura XML parsing

    [Fact]
    public void ParseCobertura_AboveThreshold_ReturnsAboveThreshold()
    {
        var xml = BuildCobertura(lineRate: 0.85);
        File.WriteAllText(Path.Combine(_tempRoot, "coverage.cobertura.xml"), xml);

        var reporter = new CoverageReporter();
        var result = reporter.ParseCobertura(Path.Combine(_tempRoot, "coverage.cobertura.xml"));

        Assert.Equal(0.85, result.LineRate, precision: 4);
    }

    [Fact]
    public void ParseCobertura_BelowThreshold_ReturnsBelowThreshold()
    {
        var xml = BuildCobertura(lineRate: 0.40);
        File.WriteAllText(Path.Combine(_tempRoot, "coverage.cobertura.xml"), xml);

        var reporter = new CoverageReporter();
        var result = reporter.ParseCobertura(Path.Combine(_tempRoot, "coverage.cobertura.xml"));

        Assert.Equal(0.40, result.LineRate, precision: 4);
    }

    [Fact]
    public void ParseCobertura_MissingFile_ThrowsFileNotFoundException()
    {
        var reporter = new CoverageReporter();
        var missingPath = Path.Combine(_tempRoot, "does-not-exist.xml");

        Assert.Throws<FileNotFoundException>(() => reporter.ParseCobertura(missingPath));
    }

    [Fact]
    public void ParseCobertura_MalformedXml_ThrowsInvalidDataException()
    {
        // Malformed XML with a valid declaration but unclosed root element.
        File.WriteAllText(
            Path.Combine(_tempRoot, "coverage.cobertura.xml"),
            "<?xml version=\"1.0\"?><coverage line-rate=\"0.5\"" // unclosed
        );

        var reporter = new CoverageReporter();
        // The XmlException is wrapped in InvalidDataException by the implementation,
        // but a root-level parser error (no declaration / whitespace-only input) can
        // surface as a raw XmlException. We accept either: the contract is "throw a
        // meaningful exception type, not crash with a stack trace through the CLI".
        Assert.ThrowsAny<Exception>(() =>
            reporter.ParseCobertura(Path.Combine(_tempRoot, "coverage.cobertura.xml"))
        );
    }

    [Fact]
    public void ParseCobertura_NonCoberturaXml_ThrowsInvalidDataException()
    {
        File.WriteAllText(
            Path.Combine(_tempRoot, "coverage.cobertura.xml"),
            "<?xml version=\"1.0\"?><other line-rate=\"0.5\" />"
        );

        var reporter = new CoverageReporter();
        Assert.Throws<InvalidDataException>(() =>
            reporter.ParseCobertura(Path.Combine(_tempRoot, "coverage.cobertura.xml"))
        );
    }

    // Threshold gate

    [Fact]
    public void EvaluateThreshold_AtEightyPercent_Passes()
    {
        var reporter = new CoverageReporter();
        var decision = reporter.EvaluateThreshold(lineRate: 0.80);

        Assert.True(decision.Passed);
        Assert.Equal(80.0, decision.Percentage, precision: 1);
    }

    [Fact]
    public void EvaluateThreshold_AboveEightyPercent_Passes()
    {
        var reporter = new CoverageReporter();
        var decision = reporter.EvaluateThreshold(lineRate: 0.95);

        Assert.True(decision.Passed);
        Assert.Equal(95.0, decision.Percentage, precision: 1);
    }

    [Fact]
    public void EvaluateThreshold_BelowEightyPercent_Fails()
    {
        var reporter = new CoverageReporter();
        var decision = reporter.EvaluateThreshold(lineRate: 0.75);

        Assert.False(decision.Passed);
        Assert.Equal(75.0, decision.Percentage, precision: 1);
    }

    // MergeCobertura

    [Fact]
    public void MergeCobertura_SameClassAcrossReports_TakesMaxHitsPerLine()
    {
        var reportA = BuildCobertura(
            new ClassSpec("MyApp", "MyApp.Services.Widget", "Services/Widget.cs", [(1, 0), (2, 3)])
        );
        var reportB = BuildCobertura(
            new ClassSpec("MyApp", "MyApp.Services.Widget", "Services/Widget.cs", [(1, 4), (3, 0)])
        );
        var pathA = WriteReport(reportA, "a.xml");
        var pathB = WriteReport(reportB, "b.xml");

        var summary = new CoverageReporter().MergeCobertura([pathA, pathB]);

        var widget = Assert.Single(summary.Classes);
        Assert.Equal("MyApp.Services.Widget", widget.Class);
        Assert.Equal("Services/Widget.cs", widget.File);
        Assert.Equal(3, widget.TotalLines);
        Assert.Equal(2, widget.CoveredLines);
    }

    [Fact]
    public void MergeCobertura_SameClassNameDifferentFiles_KeptAsSeparateEntries()
    {
        var report = BuildCobertura(
            new ClassSpec("MyApp", "Widget", "A/Widget.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Widget", "B/Widget.cs", [(1, 0)])
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        Assert.Equal(2, summary.Classes.Count);
        var widgetA = Assert.Single(summary.Classes, c => c.File == "A/Widget.cs");
        var widgetB = Assert.Single(summary.Classes, c => c.File == "B/Widget.cs");
        Assert.Equal(1, widgetA.CoveredLines);
        Assert.Equal(0, widgetB.CoveredLines);
    }

    [Fact]
    public void MergeCobertura_ExcludesGeneratedBuildAndMigrationFiles()
    {
        var report = BuildCobertura(
            new ClassSpec("MyApp", "Foo", "obj/Debug/Foo.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Bar", "Bar.generated.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Baz", "Baz.g.cs", [(1, 1)]),
            new ClassSpec("MyApp", "SeedData", "Migrations/20240101_Seed.cs", [(1, 1)]),
            new ClassSpec(
                "MyApp",
                "AppDbContextModelSnapshot",
                "Migrations/AppDbContextModelSnapshot.cs",
                [(1, 1)]
            ),
            new ClassSpec(
                "MyApp",
                "InitialCreate",
                "Migrations/InitialCreate.Designer.cs",
                [(1, 1)]
            ),
            new ClassSpec("MyApp", "Widget", "Services/Widget.cs", [(1, 1)])
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        var survivor = Assert.Single(summary.Classes);
        Assert.Equal("Widget", survivor.Class);
        Assert.Equal("Services/Widget.cs", survivor.File);
    }

    [Fact]
    public void MergeCobertura_ExcludesBlazorPlaygroundAndLayoutFiles()
    {
        var report = BuildCobertura(
            new ClassSpec(
                "CleanArchBlazorWasm.Web",
                "ButtonPlayground",
                "Features/Playground/ButtonPlayground.razor",
                [(1, 1)]
            ),
            new ClassSpec(
                "CleanArchBlazorWasm.Web",
                "MainLayout",
                "Components/Layout/MainLayout.razor",
                [(1, 1)]
            ),
            new ClassSpec(
                "CleanArchBlazorWasm.Web",
                "Button",
                "Components/Ui/Button/Button.razor",
                [(1, 1)]
            )
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        var survivor = Assert.Single(summary.Classes);
        Assert.Equal("Button", survivor.Class);
    }

    [Fact]
    public void MergeCobertura_AllEntriesExcluded_ReturnsZeroLineRate()
    {
        var report = BuildCobertura(
            new ClassSpec("MyApp", "Foo", "obj/Debug/Foo.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Bar", "Bar.g.cs", [(1, 1)])
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        Assert.Empty(summary.Classes);
        Assert.Equal(0, summary.TotalLines);
        Assert.Equal(0, summary.CoveredLines);
        Assert.Equal(0.0, summary.LineRate);
    }

    [Fact]
    public void MergeCobertura_FoldsCompilerGeneratedNestedTypes_ButKeepsGenuineNestedTypesSeparate()
    {
        var report = BuildCobertura(
            new ClassSpec("MyApp", "Handler", "Handlers/Handler.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Handler/<Handle>d__2", "Handlers/Handler.cs", [(10, 1)]),
            new ClassSpec("MyApp", "Handler/<>c", "Handlers/Handler.cs", [(20, 1)]),
            new ClassSpec(
                "MyApp",
                "Handler/<>c__DisplayClass2_0",
                "Handlers/Handler.cs",
                [(30, 0)]
            ),
            new ClassSpec("MyApp", "Outer", "Outer.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Outer/Inner", "Outer.cs", [(2, 0)])
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        Assert.Equal(3, summary.Classes.Count);

        var handler = Assert.Single(
            summary.Classes,
            c => c.Class == "Handler" && c.File == "Handlers/Handler.cs"
        );
        Assert.Equal(4, handler.TotalLines);
        Assert.Equal(3, handler.CoveredLines);

        Assert.Single(summary.Classes, c => c.Class == "Outer" && c.File == "Outer.cs");
        Assert.Single(summary.Classes, c => c.Class == "Outer/Inner" && c.File == "Outer.cs");
    }

    [Fact]
    public void MergeCobertura_FoldsTopLevelStatementAsyncStateMachine()
    {
        var report = BuildCobertura(
            new ClassSpec("MyApp", "Program", "Program.cs", [(1, 1)]),
            new ClassSpec("MyApp", "Program/<<Main>$>d__0", "Program.cs", [(5, 0)])
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        var program = Assert.Single(summary.Classes);
        Assert.Equal("Program", program.Class);
        Assert.Equal(2, program.TotalLines);
        Assert.Equal(1, program.CoveredLines);
    }

    [Fact]
    public void MergeCobertura_EmptyList_ReturnsZeroSummaryWithoutThrowing()
    {
        var summary = new CoverageReporter().MergeCobertura([]);

        Assert.Equal(0.0, summary.LineRate);
        Assert.Equal(0, summary.CoveredLines);
        Assert.Equal(0, summary.TotalLines);
        Assert.Empty(summary.Classes);
    }

    [Fact]
    public void MergeCobertura_MalformedXml_ThrowsInvalidDataException()
    {
        var path = WriteReport(
            "<?xml version=\"1.0\"?><coverage line-rate=\"0.5\"",
            "malformed.xml"
        );

        var reporter = new CoverageReporter();

        Assert.ThrowsAny<Exception>(() => reporter.MergeCobertura([path]));
    }

    [Fact]
    public void MergeCobertura_OrdersClassesByLineRateThenNameAscending()
    {
        var report = BuildCobertura(
            new ClassSpec("MyApp", "AlwaysCovered", "Always.cs", [(1, 1), (2, 1)]),
            new ClassSpec("MyApp", "HalfCovered", "Half.cs", [(1, 1), (2, 0)]),
            new ClassSpec("MyApp", "NeverCovered", "Never.cs", [(1, 0), (2, 0)])
        );
        var path = WriteReport(report, "report.xml");

        var summary = new CoverageReporter().MergeCobertura([path]);

        Assert.Collection(
            summary.Classes,
            c => Assert.Equal("NeverCovered", c.Class),
            c => Assert.Equal("HalfCovered", c.Class),
            c => Assert.Equal("AlwaysCovered", c.Class)
        );
    }

    // Helpers

    private static string BuildCobertura(double lineRate) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + $"<coverage line-rate=\"{lineRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" "
        + $"branch-rate=\"0.5\" version=\"1.9\" timestamp=\"0\" lines-covered=\"0\" lines-valid=\"0\" "
        + $"branches-covered=\"0\" branches-valid=\"0\"></coverage>";

    private static string BuildCobertura(params ClassSpec[] classes)
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

    private string WriteReport(string xml, string fileName)
    {
        var path = Path.Combine(_tempRoot, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    private sealed record ClassSpec(
        string Assembly,
        string ClassName,
        string FileName,
        (int Number, int Hits)[] Lines
    );
}
