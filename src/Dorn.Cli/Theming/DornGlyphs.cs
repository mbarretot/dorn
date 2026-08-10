namespace Dorn.Cli.Theming;

/// <summary>
/// Severity icon vocabulary, unicode + ASCII fallback pairs. Resolution always takes the
/// unicode flag as an explicit parameter (never reads console state itself) so callers decide
/// per-call whether to degrade, per the design's "resolved per call, not cached" rule.
/// </summary>
public static class DornGlyphs
{
    public const string SuccessUnicode = "✔";
    public const string SuccessAscii = "+";
    public const string ErrorUnicode = "✖";
    public const string ErrorAscii = "x";
    public const string WarningUnicode = "▲";
    public const string WarningAscii = "!";
    public const string InfoUnicode = "•";
    public const string InfoAscii = "-";

    public static string For(Severity severity, bool unicode) =>
        severity switch
        {
            Severity.Success => unicode ? SuccessUnicode : SuccessAscii,
            Severity.Error => unicode ? ErrorUnicode : ErrorAscii,
            Severity.Warning => unicode ? WarningUnicode : WarningAscii,
            Severity.Info => unicode ? InfoUnicode : InfoAscii,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
        };
}
