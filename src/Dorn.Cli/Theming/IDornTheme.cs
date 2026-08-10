using Spectre.Console;

namespace Dorn.Cli.Theming;

/// <summary>
/// Owns every color, glyph, panel, rule, and live-region decision for the dorn CLI so command
/// classes hold zero markup literals. Registered as a singleton alongside the shared
/// <see cref="IAnsiConsole"/> instance.
/// </summary>
public interface IDornTheme
{
    /// <summary>
    /// True when <c>Progress()</c>/<c>Status()</c> live regions may be opened (mirrors
    /// <c>IAnsiConsole.Profile.Capabilities.Interactive</c>). Call sites gate on this before
    /// opening a live region; the <c>false</c> branch performs the same work with plain output.
    /// </summary>
    bool LiveRegionsEnabled { get; }

    /// <summary>Writes a single severity-styled line: <c>{icon} {markup}</c> in the severity's color.</summary>
    void Message(Severity severity, string markup);

    /// <summary>Returns severity-styled markup (<c>{icon} {text}</c>) for embedding in table cells.</summary>
    string Label(Severity severity, string text);

    /// <summary>Renders a themed outcome <see cref="Panel"/> for a command's final result.</summary>
    void OutcomePanel(Severity severity, string header, string content, bool escapeContent = true);

    /// <summary>Writes a themed <see cref="Rule"/> section separator.</summary>
    void Rule(string title);

    /// <summary>Creates a themed, rounded-border <see cref="Table"/> with the given title.</summary>
    Table CreateTable(string title);

    /// <summary>Creates a themed <see cref="Progress"/> region (columns styled consistently).</summary>
    Progress CreateProgress();

    /// <summary>Creates a themed indeterminate <see cref="Status"/> spinner.</summary>
    Status CreateStatus();

    /// <summary>Renders the dorn startup banner (Figlet + command reference table).</summary>
    void Banner();
}
