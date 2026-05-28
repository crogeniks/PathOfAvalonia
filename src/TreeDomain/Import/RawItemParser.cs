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
            name = ItemText.StripTags(lines[i++].Trim());
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        var ru = rarity.ToUpperInvariant();
        if ((ru == "RARE" || ru == "UNIQUE") && i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            baseType = ItemText.StripTags(lines[i].Trim());
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
                runes.Add(ItemText.StripTags(line[5..].Trim()));
            }
        }

        return new ImportedItem(slot, rarity, name, baseType, rawText.Trim())
        {
            Sockets = sockets,
            Runes = runes,
        };
    }

}
