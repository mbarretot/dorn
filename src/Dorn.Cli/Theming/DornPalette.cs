using Spectre.Console;

namespace Dorn.Cli.Theming;

// Markup name constants are the lowercase form of the matching Color member — Spectre
// markup names are case-insensitive, so the two representations always agree.
public static class DornPalette
{
    public const string BrandMarkup = "steelblue1";
    public const string SuccessMarkup = "green";
    public const string ErrorMarkup = "red";
    public const string WarningMarkup = "yellow";
    public const string InfoMarkup = "cyan1";
    public const string MutedMarkup = "grey";

    public static Color Brand => Color.SteelBlue1;
    public static Color Success => Color.Green;
    public static Color Error => Color.Red;
    public static Color Warning => Color.Yellow;
    public static Color Info => Color.Cyan1;
    public static Color Muted => Color.Grey;

    public static Color ColorFor(Severity severity) =>
        severity switch
        {
            Severity.Success => Success,
            Severity.Error => Error,
            Severity.Warning => Warning,
            Severity.Info => Info,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
        };

    public static string MarkupFor(Severity severity) =>
        severity switch
        {
            Severity.Success => SuccessMarkup,
            Severity.Error => ErrorMarkup,
            Severity.Warning => WarningMarkup,
            Severity.Info => InfoMarkup,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
        };
}
