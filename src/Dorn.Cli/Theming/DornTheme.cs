using Spectre.Console;

namespace Dorn.Cli.Theming;

// Capability-driven output is resolved per call, not cached in the ctor, so mutating
// TestConsole's capabilities after construction still takes effect.
public sealed class DornTheme : IDornTheme
{
    private readonly IAnsiConsole _console;

    public DornTheme(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public bool LiveRegionsEnabled => _console.Profile.Capabilities.Interactive;

    public void Message(Severity severity, string markup)
    {
        var icon = DornGlyphs.For(severity, _console.Profile.Capabilities.Unicode);
        var color = DornPalette.MarkupFor(severity);
        _console.MarkupLine($"[{color}]{icon} {markup}[/]");
    }

    public string Label(Severity severity, string text)
    {
        var icon = DornGlyphs.For(severity, _console.Profile.Capabilities.Unicode);
        var color = DornPalette.MarkupFor(severity);
        return $"[{color}]{icon} {text}[/]";
    }

    public void OutcomePanel(
        Severity severity,
        string header,
        string content,
        bool escapeContent = true
    )
    {
        var icon = DornGlyphs.For(severity, _console.Profile.Capabilities.Unicode);
        var color = DornPalette.ColorFor(severity);
        var body = escapeContent ? Markup.Escape(content) : content;

        _console.Write(
            new Panel(body).Header(Markup.Escape($"{icon} {header}")).BorderColor(color)
        );
    }

    public void Rule(string title)
    {
        _console.Write(new Rule(title).RuleStyle(new Style(foreground: DornPalette.Brand)));
    }

    public Table CreateTable(string title) => new Table().Border(TableBorder.Rounded).Title(title);

    public Progress CreateProgress() =>
        _console
            .Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            );

    public Status CreateStatus() => _console.Status().Spinner(Spinner.Known.Dots);

    public void Banner()
    {
        _console.Write(new FigletText("dorn").Color(DornPalette.Brand));
        _console.MarkupLine(
            $"[{DornPalette.MutedMarkup}]Clean Architecture project scaffolding for .NET[/]"
        );
        _console.WriteLine();

        var table = CreateTable("Available commands");
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]new webapi[/] <name>",
            "Generate a Clean Architecture Web API project."
        );
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]new grpc[/] <name>",
            "Generate a Clean Architecture gRPC service (sqlite + EF Core + Aspire)."
        );
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]new worker[/] <name>",
            "Generate a Clean Architecture worker service (sqlite + EF Core + Aspire)."
        );
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]test[/]",
            "Run the generated project's test tiers."
        );
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]run[/]",
            "Run the generated project (auto-detects AppHost/Compose/Plain)."
        );
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]coverage[/]",
            "Run tests with coverage; gate at 80%."
        );
        table.AddRow(
            $"[{DornPalette.SuccessMarkup}]doctor[/]",
            "Check the local environment (templates, .NET SDK, Docker)."
        );
        _console.Write(table);

        _console.WriteLine();
        _console.MarkupLine(
            $"Run [{DornPalette.WarningMarkup}]dorn <command> --help[/] for options on a specific command."
        );
        _console.MarkupLine(
            $"Run [{DornPalette.WarningMarkup}]dorn --help[/] for the full command reference."
        );
    }
}
