namespace Dorn.Cli.Theming;

/// <summary>
/// The four outcome severities dorn renders through <see cref="IDornTheme"/>. Every
/// success/error/warning/info message in the CLI maps to exactly one of these — there is no
/// fifth "neutral" bucket, so purely informational status lines use <see cref="Info"/>.
/// </summary>
public enum Severity
{
    Success,
    Error,
    Warning,
    Info,
}
