namespace PathOfAvalonia.TreeDomain.Import;

public static class RawItemParser
{
    public static ImportedItem Parse(string slot, string rawText)
    {
        var lines = rawText.Replace("\r\n", "\n").Split('\n');
        var rarity = "Normal";
        var name = string.Empty;
        var baseType = string.Empty;
        var sockets = new List<ImportedItemSocket>();
        var runes = new List<string>();
        var i = 0;

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        if (i < lines.Length)
        {
            var first = lines[i].Trim();
            if (first.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase))
            {
                rarity = first[7..].Trim();
                i++;
            }
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        if (i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            name = StripTags(lines[i++].Trim());
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        var ru = rarity.ToUpperInvariant();
        if ((ru == "RARE" || ru == "UNIQUE") && i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            baseType = StripTags(lines[i].Trim());
        }
        else
        {
            baseType = name;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var token in line[8..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    sockets.Add(new ImportedItemSocket(token, null));
                }
            }
            else if (line.StartsWith("Rune:", StringComparison.OrdinalIgnoreCase))
            {
                runes.Add(StripTags(line[5..].Trim()));
            }
        }

        return new ImportedItem(slot, rarity, name, baseType, rawText.Trim())
        {
            Sockets = sockets,
            Runes = runes,
        };
    }

    private static string StripTags(string text)
    {
        var span = text.AsSpan();
        while (span.StartsWith("{", StringComparison.Ordinal))
        {
            var close = span.IndexOf('}');
            if (close < 0)
            {
                break;
            }
            span = span[(close + 1)..];
        }
        return span.ToString();
    }
}
