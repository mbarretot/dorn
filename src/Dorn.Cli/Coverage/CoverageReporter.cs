using System.Xml;
using System.Xml.Linq;

namespace Dorn.Cli.Coverage;

/// <summary>
/// Parses Cobertura coverage reports and applies the fixed 80% line-rate gate; HTML rendering is delegated to ReportGenerator.
/// </summary>
public sealed class CoverageReporter
{
    /// <summary>The fixed threshold required for a passing run.</summary>
    public const double Threshold = 0.80;

    /// <summary>
    /// Parses a Cobertura XML report and returns its line rate as a fraction (0.0–1.0).
    /// </summary>
    public CoberturaResult ParseCobertura(string xmlPath)
    {
        var doc = LoadCoberturaDocument(xmlPath);
        var root = doc.Root!;

        var lineRateAttr = root.Attribute("line-rate");
        if (
            lineRateAttr is null
            || !double.TryParse(
                lineRateAttr.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var rate
            )
        )
        {
            throw new InvalidDataException(
                $"Coverage report at '{xmlPath}' has missing or invalid 'line-rate' attribute."
            );
        }

        return new CoberturaResult(rate);
    }

    /// <summary>
    /// Evaluates the fixed threshold gate against a line rate.
    /// </summary>
    public ThresholdDecision EvaluateThreshold(double lineRate)
    {
        var passed = lineRate >= Threshold;
        return new ThresholdDecision(passed, lineRate * 100.0);
    }

    /// <summary>
    /// Merges line coverage across multiple Cobertura reports, keyed by (filename, folded declaring type), taking max hits per line.
    /// </summary>
    public CoverageSummary MergeCobertura(IReadOnlyList<string> xmlPaths)
    {
        var lineHitsByKey = new Dictionary<(string File, string Class), Dictionary<int, int>>();
        var assemblyByKey = new Dictionary<(string File, string Class), string>();

        foreach (var xmlPath in xmlPaths)
        {
            var doc = LoadCoberturaDocument(xmlPath);
            foreach (var package in doc.Root!.Descendants("package"))
            {
                var assembly = package.Attribute("name")?.Value ?? string.Empty;
                foreach (var classElement in package.Descendants("class"))
                {
                    var filename = (
                        classElement.Attribute("filename")?.Value ?? string.Empty
                    ).Replace('\\', '/');
                    if (IsExcluded(filename))
                        continue;

                    var className = FoldGeneratedType(
                        classElement.Attribute("name")?.Value ?? string.Empty
                    );
                    var key = (filename, className);

                    if (!lineHitsByKey.TryGetValue(key, out var lineHits))
                    {
                        lineHits = new Dictionary<int, int>();
                        lineHitsByKey[key] = lineHits;
                        assemblyByKey[key] = assembly;
                    }

                    foreach (var line in classElement.Descendants("line"))
                    {
                        if (!int.TryParse(line.Attribute("number")?.Value, out var number))
                            continue;
                        if (!int.TryParse(line.Attribute("hits")?.Value, out var hits))
                            continue;

                        lineHits[number] = lineHits.TryGetValue(number, out var existing)
                            ? Math.Max(existing, hits)
                            : hits;
                    }
                }
            }
        }

        var classes = lineHitsByKey
            .Select(entry => new ClassCoverage(
                assemblyByKey[entry.Key],
                entry.Key.Class,
                entry.Key.File,
                entry.Value.Count(l => l.Value > 0),
                entry.Value.Count
            ))
            .OrderBy(c => c.LineRate)
            .ThenBy(c => c.Class, StringComparer.Ordinal)
            .ToList();

        var totalLines = classes.Sum(c => c.TotalLines);
        var coveredLines = classes.Sum(c => c.CoveredLines);
        var lineRate = totalLines == 0 ? 0.0 : (double)coveredLines / totalLines;

        return new CoverageSummary(lineRate, coveredLines, totalLines, classes);
    }

    private static XDocument LoadCoberturaDocument(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"Coverage report not found at '{xmlPath}'.", xmlPath);

        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlPath);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException($"Coverage report at '{xmlPath}' is not valid XML.", ex);
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "coverage")
            throw new InvalidDataException(
                $"Coverage report at '{xmlPath}' is not a Cobertura document (root is not <coverage>)."
            );

        return doc;
    }

    /// <summary>Filename-based exclusion for build output, migrations, generated demo/layout markup, and compiler/tool-generated files.</summary>
    private static bool IsExcluded(string filename)
    {
        var segments = filename.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (segments.Any(s => s.Equals("Migrations", StringComparison.OrdinalIgnoreCase)))
            return true;
        // Demo markup and layout shells have no behavior of their own to assert on.
        if (segments.Any(s => s.Equals("Playground", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (segments.Any(s => s.Equals("Layout", StringComparison.OrdinalIgnoreCase)))
            return true;

        return filename.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Folds compiler-generated nested types (async state machines, closures) into their declaring type.</summary>
    private static string FoldGeneratedType(string className)
    {
        var segments = className.Split('/');
        var declaring = segments.TakeWhile(s => !s.StartsWith('<')).ToArray();
        return declaring.Length == 0 ? className : string.Join('/', declaring);
    }
}

/// <summary>Parsed Cobertura line rate (fraction 0.0–1.0).</summary>
public sealed record CoberturaResult(double LineRate);

/// <summary>Threshold gate outcome with the percentage (0–100) and pass/fail.</summary>
public sealed record ThresholdDecision(bool Passed, double Percentage);

/// <summary>Merged per-class line coverage keyed by file and folded declaring type.</summary>
public sealed record ClassCoverage(
    string Assembly,
    string Class,
    string File,
    int CoveredLines,
    int TotalLines
)
{
    public double LineRate => TotalLines == 0 ? 0.0 : (double)CoveredLines / TotalLines;
}

/// <summary>Aggregate line coverage across all merged, non-excluded classes.</summary>
public sealed record CoverageSummary(
    double LineRate,
    int CoveredLines,
    int TotalLines,
    IReadOnlyList<ClassCoverage> Classes
);
