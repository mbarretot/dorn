using System.Xml;
using System.Xml.Linq;

namespace Dorn.Cli.Coverage;

/// <summary>
/// Parses Cobertura XML coverage reports and decides whether the fixed 80% line-rate
/// threshold has been met. HTML rendering via ReportGenerator is orchestrated here
/// (installed ephemerally via <c>--tool-path</c>) but the gate logic does not depend on it.
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
}

/// <summary>Parsed Cobertura line rate (fraction 0.0–1.0).</summary>
public sealed record CoberturaResult(double LineRate);

/// <summary>Threshold gate outcome with the percentage (0–100) and pass/fail.</summary>
public sealed record ThresholdDecision(bool Passed, double Percentage);
