namespace CleanArchBlazorWasm.Web.Features.Playground;

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
                new("/playground/toggle", "Toggle", ["press", "button", "on", "off"]),
                new(
                    "/playground/toggle-group",
                    "ToggleGroup",
                    ["press", "multiple", "single", "options"]
                ),
                new(
                    "/playground/radio-group",
                    "RadioGroup",
                    ["options", "select one", "radio button"]
                ),
            ]
        ),
        new(
            "Overlays",
            [
                new("/playground/dialog", "Dialog", ["modal", "overlay"]),
                new("/playground/dropdown-menu", "DropdownMenu", ["dropdown", "menu", "overlay"]),
            ]
        ),
        new(
            "Display",
            [
                new("/playground/card", "Card", ["container", "panel"]),
                new("/playground/avatar", "Avatar", ["profile", "image", "fallback", "initials"]),
                new("/playground/badge", "Badge", ["tag", "label", "chip", "status"]),
                new("/playground/skeleton", "Skeleton", ["loading", "placeholder", "shimmer"]),
            ]
        ),
        new(
            "Layout",
            [
                new("/playground/tabs", "Tabs", ["navigation", "panel"]),
                new("/playground/separator", "Separator", ["divider", "line", "hr"]),
                new("/playground/breadcrumb", "Breadcrumb", ["navigation", "trail", "path"]),
            ]
        ),
        new(
            "Feedback",
            [
                new("/playground/alert", "Alert", ["banner", "notice", "status", "warning"]),
                new("/playground/progress", "Progress", ["loading", "bar", "meter", "percentage"]),
            ]
        ),
    ];
}
