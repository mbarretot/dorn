namespace CleanArchBlazorServer.Web.Features.Playground;

public sealed record PlaygroundEntry(string Href, string Label, string[] Keywords);

public sealed record PlaygroundCategory(string Name, IReadOnlyList<PlaygroundEntry> Entries);

public static class PlaygroundCatalog
{
    public static readonly IReadOnlyList<PlaygroundCategory> Categories =
    [
        new("Forms", [new("/playground/button", "Button", ["action", "click", "cta"])]),
    ];
}
