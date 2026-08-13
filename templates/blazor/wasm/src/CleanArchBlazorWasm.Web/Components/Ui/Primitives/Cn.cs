namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

/// <summary>
/// The one shared variant/class-merge utility (design C2) — a curated
/// <c>tailwind-merge</c> equivalent. Consumers pass base classes, variant classes, and finally
/// the consumer-supplied <c>Class</c> parameter last so it wins on conflict.
/// </summary>
public static class Cn
{
    public static string Merge(params string?[] inputs)
    {
        var tokens = inputs
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .SelectMany(input => input!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        var kept = new bool[tokens.Count];
        var seenGroupKeys = new HashSet<string>();

        // Right-to-left: the first occurrence encountered here is the LAST original token, so
        // keeping it and dropping earlier duplicates makes later inputs take precedence.
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            if (seenGroupKeys.Add(ResolveGroupKey(tokens[i])))
            {
                kept[i] = true;
            }
        }

        return string.Join(' ', tokens.Where((_, i) => kept[i]));
    }

    private static string ResolveGroupKey(string token)
    {
        var (variant, baseToken) = SplitVariant(token);
        var normalized = baseToken.StartsWith('!') ? baseToken[1..] : baseToken;
        return variant + ClassGroups.Resolve(normalized);
    }

    /// <summary>
    /// Splits off the variant prefix (<c>hover:</c>, <c>md:</c>, <c>dark:</c>,
    /// <c>data-[state=open]:</c>) at the last top-level colon — one that is not inside an
    /// arbitrary-value <c>[...]</c> bracket, since those can contain their own colons.
    /// </summary>
    private static (string Variant, string BaseToken) SplitVariant(string token)
    {
        var depth = 0;
        var lastColon = -1;

        for (var i = 0; i < token.Length; i++)
        {
            switch (token[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ':' when depth == 0:
                    lastColon = i;
                    break;
            }
        }

        return lastColon < 0
            ? (string.Empty, token)
            : (token[..(lastColon + 1)], token[(lastColon + 1)..]);
    }
}
