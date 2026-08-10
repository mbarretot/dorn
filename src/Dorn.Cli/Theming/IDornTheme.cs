using Spectre.Console;

namespace Dorn.Cli.Theming;

public interface IDornTheme
{
    // Mirrors IAnsiConsole.Profile.Capabilities.Interactive; call sites gate live regions on
    // this and fall back to plain output when false.
    bool LiveRegionsEnabled { get; }

    void Message(Severity severity, string markup);

    string Label(Severity severity, string text);

    void OutcomePanel(Severity severity, string header, string content, bool escapeContent = true);

    void Rule(string title);

    Table CreateTable(string title);

    Progress CreateProgress();

    Status CreateStatus();

    void Banner();
}
