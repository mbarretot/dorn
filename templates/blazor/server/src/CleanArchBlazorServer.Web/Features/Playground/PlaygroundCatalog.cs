namespace CleanArchBlazorServer.Web.Features.Playground;

public sealed record PlaygroundEntry(string Href, string Label, string[] Keywords);

public sealed record PlaygroundCategory(string Name, IReadOnlyList<PlaygroundEntry> Entries);

public static class PlaygroundCatalog
{
    public static readonly IReadOnlyList<PlaygroundCategory> Categories =
    [
        new(
            "Forms",
            [
                new("/playground/button", "Button", ["action", "click", "cta"]),
                new("/playground/form", "Form", ["input", "label", "text field"]),
                new("/playground/select", "Select", ["dropdown", "combobox", "options"]),
            ]
        ),
        new(
            "Overlays",
            [
                new("/playground/dialog", "Dialog", ["modal", "overlay"]),
                new("/playground/dropdown-menu", "DropdownMenu", ["dropdown", "menu", "overlay"]),
            ]
        ),
        new("Display", [new("/playground/card", "Card", ["container", "panel"])]),
        new("Layout", [new("/playground/tabs", "Tabs", ["navigation", "panel"])]),
    ];
}
