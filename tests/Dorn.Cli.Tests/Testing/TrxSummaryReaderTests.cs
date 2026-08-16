using System.Xml.Linq;
using Dorn.Cli.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Testing;

///<summary>Tests for <see cref="TrxSummaryReader"/>: fixture-driven parsing of VSTest <c>.trx</c> result summaries.</summary>
public class TrxSummaryReaderTests : IDisposable
{
    private readonly string _tempRoot;

    public TrxSummaryReaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-trxreader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void TryRead_HappyPath_ReturnsPopulatedSummary()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var finish = start.AddSeconds(12.5);
        var path = WriteTrx(
            BuildTrx(total: 10, passed: 8, failed: 2, notExecuted: 0, start: start, finish: finish)
        );

        var summary = TrxSummaryReader.TryRead(path);

        Assert.NotNull(summary);
        Assert.Equal(10, summary!.Total);
        Assert.Equal(8, summary.Passed);
        Assert.Equal(2, summary.Failed);
        Assert.Equal(0, summary.Skipped);
        Assert.NotNull(summary.DurationSeconds);
        Assert.Equal(12.5, summary.DurationSeconds!.Value, precision: 3);
    }

    [Fact]
    public void TryRead_AllZeroCounters_ReturnsPopulatedSummaryNotNull()
    {
        var path = WriteTrx(BuildTrx(total: 0, passed: 0, failed: 0, notExecuted: 0));

        var summary = TrxSummaryReader.TryRead(path);

        Assert.NotNull(summary);
        Assert.Equal(0, summary!.Total);
        Assert.Equal(0, summary.Passed);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Skipped);
    }

    [Fact]
    public void TryRead_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(_tempRoot, "does-not-exist.trx");

        var summary = TrxSummaryReader.TryRead(path);

        Assert.Null(summary);
    }

    [Fact]
    public void TryRead_MalformedXml_ReturnsNullAndDoesNotThrow()
    {
        var path = WriteTrx("<TestRun><ResultSummary><Counters total=\"1\"");

        var summary = TrxSummaryReader.TryRead(path);

        Assert.Null(summary);
    }

    [Fact]
    public void TryRead_CountersAbsent_ReturnsNull()
    {
        var path = WriteTrx(
            BuildTrx(total: 1, passed: 1, failed: 0, notExecuted: 0, includeCounters: false)
        );

        var summary = TrxSummaryReader.TryRead(path);

        Assert.Null(summary);
    }

    [Fact]
    public void TryRead_TimesAbsent_CountsSurviveWithNullDuration()
    {
        var path = WriteTrx(
            BuildTrx(total: 5, passed: 5, failed: 0, notExecuted: 0, includeTimes: false)
        );

        var summary = TrxSummaryReader.TryRead(path);

        Assert.NotNull(summary);
        Assert.Equal(5, summary!.Total);
        Assert.Equal(5, summary.Passed);
        Assert.Null(summary.DurationSeconds);
    }

    private string WriteTrx(string content)
    {
        var path = Path.Combine(_tempRoot, $"{Guid.NewGuid():N}.trx");
        File.WriteAllText(path, content);
        return path;
    }

    private static string BuildTrx(
        int total,
        int passed,
        int failed,
        int notExecuted,
        DateTime? start = null,
        DateTime? finish = null,
        bool includeTimes = true,
        bool includeCounters = true
    )
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var root = new XElement(ns + "TestRun");

        root.Add(
            includeCounters
                ? new XElement(
                    ns + "ResultSummary",
                    new XAttribute("outcome", "Completed"),
                    new XElement(
                        ns + "Counters",
                        new XAttribute("total", total),
                        new XAttribute("passed", passed),
                        new XAttribute("failed", failed),
                        new XAttribute("notExecuted", notExecuted)
                    )
                )
                : new XElement(ns + "ResultSummary", new XAttribute("outcome", "Completed"))
        );

        if (includeTimes)
        {
            root.Add(
                new XElement(
                    ns + "Times",
                    new XAttribute("start", (start ?? DateTime.UtcNow).ToString("O")),
                    new XAttribute("finish", (finish ?? DateTime.UtcNow).ToString("O"))
                )
            );
        }

        return new XDocument(root).ToString();
    }
}
