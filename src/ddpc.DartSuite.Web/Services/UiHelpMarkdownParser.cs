namespace ddpc.DartSuite.Web.Services;

public static class UiHelpMarkdownParser
{
    public static IReadOnlyDictionary<string, string> Parse(string markdown)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(markdown))
            return result;

        string? currentKey = null;
        var buffer = new List<string>();

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (TryParseKey(line, out var parsedKey))
            {
                FlushCurrent(result, currentKey, buffer);
                currentKey = parsedKey;
                buffer.Clear();
                continue;
            }

            if (currentKey is not null)
                buffer.Add(line);
        }

        FlushCurrent(result, currentKey, buffer);
        return result;
    }

    private static void FlushCurrent(IDictionary<string, string> target, string? key, List<string> buffer)
    {
        if (string.IsNullOrWhiteSpace(key) || target.ContainsKey(key))
            return;

        var text = string.Join(Environment.NewLine, buffer).Trim();
        if (!string.IsNullOrWhiteSpace(text))
            target[key] = text;
    }

    private static bool TryParseKey(string line, out string? key)
    {
        key = null;
        const string headingPrefix = "### ";

        if (!line.StartsWith(headingPrefix, StringComparison.Ordinal))
            return false;

        var heading = line[headingPrefix.Length..].Trim();

        if (heading.StartsWith("[help:", StringComparison.OrdinalIgnoreCase)
            && heading.EndsWith(']'))
        {
            var value = heading[6..^1].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                key = value;
                return true;
            }
        }

        if (heading.StartsWith("help:", StringComparison.OrdinalIgnoreCase))
        {
            var value = heading[5..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                key = value;
                return true;
            }
        }

        return false;
    }
}
