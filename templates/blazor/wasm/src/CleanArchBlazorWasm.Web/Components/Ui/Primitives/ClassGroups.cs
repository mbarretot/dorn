namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

/// <summary>
/// Curated `tailwind-merge` conflict-group table (design C2) — enough coverage for the seven
/// v1 components, not a full port. An utility that matches no rule below resolves to its own
/// literal text as the group name, which guarantees it is never merged away (only identical
/// unknown tokens collapse, which is correct de-duplication, not loss).
/// </summary>
internal static class ClassGroups
{
    private static readonly string[] DisplayValues =
    [
        "block",
        "inline-block",
        "inline",
        "flex",
        "inline-flex",
        "grid",
        "inline-grid",
        "hidden",
        "contents",
        "table",
    ];

    private static readonly string[] PositionValues =
    [
        "static",
        "fixed",
        "absolute",
        "relative",
        "sticky",
    ];

    private static readonly string[] TextSizeValues =
    [
        "xs",
        "sm",
        "base",
        "lg",
        "xl",
        "2xl",
        "3xl",
        "4xl",
        "5xl",
        "6xl",
        "7xl",
        "8xl",
        "9xl",
    ];

    private static readonly string[] TextAlignValues =
    [
        "left",
        "center",
        "right",
        "justify",
        "start",
        "end",
    ];

    private static readonly string[] FontWeightValues =
    [
        "thin",
        "extralight",
        "light",
        "normal",
        "medium",
        "semibold",
        "bold",
        "extrabold",
        "black",
    ];

    private static readonly string[] FontFamilyValues = ["sans", "serif", "mono"];

    private static readonly (string GroupName, Func<string, bool> Matches)[] Rules =
    [
        ("display", t => DisplayValues.Contains(t)),
        ("position", t => PositionValues.Contains(t)),
        ("z-index", t => HasPrefix(t, "z-")),
        (
            "flex-direction",
            t => t is "flex-row" or "flex-row-reverse" or "flex-col" or "flex-col-reverse"
        ),
        ("items", t => HasPrefix(t, "items-")),
        ("justify", t => HasPrefix(t, "justify-")),
        ("gap-x", t => HasPrefix(t, "gap-x-")),
        ("gap-y", t => HasPrefix(t, "gap-y-")),
        ("gap", t => HasPrefix(t, "gap-")),
        ("padding-x", t => HasPrefix(t, "px-")),
        ("padding-y", t => HasPrefix(t, "py-")),
        ("padding-top", t => HasPrefix(t, "pt-")),
        ("padding-right", t => HasPrefix(t, "pr-")),
        ("padding-bottom", t => HasPrefix(t, "pb-")),
        ("padding-left", t => HasPrefix(t, "pl-")),
        ("padding-all", t => HasPrefix(t, "p-")),
        ("margin-x", t => HasPrefix(t, "mx-")),
        ("margin-y", t => HasPrefix(t, "my-")),
        ("margin-top", t => HasPrefix(t, "mt-")),
        ("margin-right", t => HasPrefix(t, "mr-")),
        ("margin-bottom", t => HasPrefix(t, "mb-")),
        ("margin-left", t => HasPrefix(t, "ml-")),
        ("margin-all", t => HasPrefix(t, "m-")),
        ("width", t => HasPrefix(t, "w-")),
        ("height", t => HasPrefix(t, "h-")),
        (
            "text-size",
            t => HasPrefix(t, "text-") && TextSizeValues.Contains(StripPrefix(t, "text-"))
        ),
        (
            "text-align",
            t => HasPrefix(t, "text-") && TextAlignValues.Contains(StripPrefix(t, "text-"))
        ),
        (
            "font-weight",
            t => HasPrefix(t, "font-") && FontWeightValues.Contains(StripPrefix(t, "font-"))
        ),
        (
            "font-family",
            t => HasPrefix(t, "font-") && FontFamilyValues.Contains(StripPrefix(t, "font-"))
        ),
        ("leading", t => HasPrefix(t, "leading-")),
        ("tracking", t => HasPrefix(t, "tracking-")),
        ("bg-color", t => HasPrefix(t, "bg-")),
        ("border-radius", t => HasPrefix(t, "rounded")),
        (
            "border-width",
            t =>
                t == "border"
                || HasPrefix(t, "border-") && IsNumericSuffix(StripPrefix(t, "border-"))
        ),
        ("border-color", t => HasPrefix(t, "border-")),
        ("ring", t => HasPrefix(t, "ring")),
        ("shadow", t => HasPrefix(t, "shadow")),
        ("opacity", t => HasPrefix(t, "opacity-")),
        ("transition", t => HasPrefix(t, "transition")),
        ("cursor", t => HasPrefix(t, "cursor-")),
        ("overflow-x", t => HasPrefix(t, "overflow-x-")),
        ("overflow-y", t => HasPrefix(t, "overflow-y-")),
        ("overflow", t => HasPrefix(t, "overflow-")),
        ("whitespace", t => HasPrefix(t, "whitespace-")),
        // Text color is the widest "text-*" catch-all and must stay last among text- rules.
        ("text-color", t => HasPrefix(t, "text-")),
    ];

    public static string Resolve(string baseToken)
    {
        foreach (var (groupName, matches) in Rules)
        {
            if (matches(baseToken))
            {
                return groupName;
            }
        }

        return baseToken;
    }

    private static bool HasPrefix(string token, string prefix) =>
        token.StartsWith(prefix, StringComparison.Ordinal);

    private static string StripPrefix(string token, string prefix) => token[prefix.Length..];

    private static bool IsNumericSuffix(string value) =>
        value.Length > 0 && value.All(char.IsAsciiDigit);
}
