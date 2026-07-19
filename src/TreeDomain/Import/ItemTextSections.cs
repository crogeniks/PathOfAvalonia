namespace PathOfAvalonia.TreeDomain.Import;

/// <summary>Normalized, tag-aware representation of an item's copied text.</summary>
public sealed class ItemTextSections
{
    private ItemTextSections(string rawText, IReadOnlyList<ItemTextLine> lines, int headerLineCount, string rarity)
    {
        RawText = rawText;
        Lines = lines;
        HeaderLineCount = headerLineCount;
        Rarity = rarity;
        BodyLines = lines.Skip(headerLineCount).ToArray();
    }

    public string RawText { get; }
    public IReadOnlyList<ItemTextLine> Lines { get; }
    public int HeaderLineCount { get; }
    public IReadOnlyList<ItemTextLine> BodyLines { get; }

    public static ItemTextSections Parse(string rawText)
    {
        var normalized = PobText.StripColorCodes(rawText).Replace("\r\n", "\n").Trim();
        var lines = normalized.Split('\n').Select(ItemTextLine.Parse).ToArray();
        var rarity = ReadRarity(lines);
        var headerLineCount = FindHeaderLineCount(lines, rarity);
        return new ItemTextSections(normalized, lines, headerLineCount, rarity);
    }

    public string Rarity { get; }

    private static string ReadRarity(IReadOnlyList<ItemTextLine> lines)
    {
        var first = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line.Raw));
        return first is not null && first.Text.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase)
            ? first.Text[7..].Trim()
            : "Normal";
    }

    private static int FindHeaderLineCount(IReadOnlyList<ItemTextLine> lines, string rarity)
    {
        var index = 0;
        while (index < lines.Count && string.IsNullOrWhiteSpace(lines[index].Raw)) index++;
        if (index < lines.Count && lines[index].Text.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase)) index++;
        while (index < lines.Count && string.IsNullOrWhiteSpace(lines[index].Raw)) index++;
        if (index < lines.Count && !lines[index].Raw.StartsWith("---", StringComparison.Ordinal)) index++;
        while (index < lines.Count && string.IsNullOrWhiteSpace(lines[index].Raw)) index++;
        if ((string.Equals(rarity, "Rare", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rarity, "Unique", StringComparison.OrdinalIgnoreCase))
            && index < lines.Count && !lines[index].Raw.StartsWith("---", StringComparison.Ordinal)) index++;
        return index;
    }
}

public sealed record ItemTextLine(string Raw, string Text, IReadOnlyList<string> Tags)
{
    public static ItemTextLine Parse(string raw)
    {
        raw = raw.Trim();
        var remaining = raw.AsSpan();
        var tags = new List<string>();
        while (remaining.StartsWith("{", StringComparison.Ordinal))
        {
            var close = remaining.IndexOf('}');
            if (close < 0) break;
            tags.Add(remaining[1..close].ToString());
            remaining = remaining[(close + 1)..];
            while (!remaining.IsEmpty && char.IsWhiteSpace(remaining[0])) remaining = remaining[1..];
        }
        return new ItemTextLine(raw, remaining.ToString(), tags);
    }
}
