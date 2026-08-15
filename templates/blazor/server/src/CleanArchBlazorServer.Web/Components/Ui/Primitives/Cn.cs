namespace CleanArchBlazorServer.Web.Components.Ui.Primitives;

// Curated tailwind-merge equivalent (design C2); consumer's Class param is passed last, so it wins.
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

        // Right-to-left: first occurrence found here is the LAST original token, so later inputs win.
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

    // Splits at the last top-level colon, ignoring colons inside an arbitrary-value [...] bracket.
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
