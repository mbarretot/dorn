using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Dorn.Cli.Testing;

/// <summary>
/// Parsed <c>ResultSummary</c>/<c>Times</c> data from a VSTest <c>.trx</c> file.
/// </summary>
internal sealed record TrxSummary(
    int Total,
    int Passed,
    int Failed,
    int Skipped,
    double? DurationSeconds
);

/// <summary>
/// Reads MSTest/VSTest <c>.trx</c> result files produced by <c>dotnet test --logger trx</c>.
/// Element/attribute lookup is by local name only, so the standard VSTest 2010 XML namespace
/// never has to be hard-coded.
/// </summary>
internal static class TrxSummaryReader
{
    public static TrxSummary? TryRead(string path)
    {
        if (!File.Exists(path))
            return null;

        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }

        var counters = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Counters");
        if (counters is null)
            return null;

        if (
            !TryGetInt(counters, "total", out var total)
            || !TryGetInt(counters, "passed", out var passed)
            || !TryGetInt(counters, "failed", out var failed)
            || !TryGetInt(counters, "notExecuted", out var skipped)
        )
        {
            return null;
        }

        var times = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Times");
        return new TrxSummary(total, passed, failed, skipped, TryGetDurationSeconds(times));
    }

    private static double? TryGetDurationSeconds(XElement? times)
    {
        if (times is null)
            return null;

        if (
            !TryGetDateTime(times, "start", out var start)
            || !TryGetDateTime(times, "finish", out var finish)
        )
        {
            return null;
        }

        return (finish - start).TotalSeconds;
    }

    private static bool TryGetInt(XElement element, string attributeName, out int value)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute is null)
        {
            value = 0;
            return false;
        }

        return int.TryParse(attribute.Value, out value);
    }

    private static bool TryGetDateTime(XElement element, string attributeName, out DateTime value)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute is null)
        {
            value = default;
            return false;
        }

        return DateTime.TryParse(
            attribute.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value
        );
    }
}
