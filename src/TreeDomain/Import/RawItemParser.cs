namespace PathOfAvalonia.TreeDomain.Import;

public static class RawItemParser
{
    public static ImportedItem Parse(string slot, string rawText)
    {
        var text = ItemTextSections.Parse(rawText);
        var lines = text.Lines;
        var rarity = text.Rarity;
        var name = string.Empty;
        var baseType = string.Empty;
        var sockets = new List<ImportedItemSocket>();
        var runes = new List<string>();
        var variants = new List<string>();
        int? selectedVariant = null;
        var i = 0;

        while (i < lines.Count && string.IsNullOrWhiteSpace(lines[i].Raw))
        {
            i++;
        }

        if (i < lines.Count)
        {
            var first = lines[i].Text;
            if (first.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase))
            {
                i++;
            }
        }

        while (i < lines.Count && string.IsNullOrWhiteSpace(lines[i].Raw))
        {
            i++;
        }

        if (i < lines.Count && !lines[i].Raw.StartsWith("---", StringComparison.Ordinal))
        {
            name = lines[i++].Text;
        }

        while (i < lines.Count && string.IsNullOrWhiteSpace(lines[i].Raw))
        {
            i++;
        }

        var ru = rarity.ToUpperInvariant();
        if ((ru == "RARE" || ru == "UNIQUE") && i < lines.Count && !lines[i].Raw.StartsWith("---", StringComparison.Ordinal))
        {
            baseType = lines[i].Text;
        }
        else
        {
            baseType = name;
        }

        foreach (var itemLine in lines)
        {
            var line = itemLine.Text;
            if (line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var token in line[8..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    sockets.Add(new ImportedItemSocket(token, null));
                }
            }
            else if (line.StartsWith("Rune:", StringComparison.OrdinalIgnoreCase))
            {
                runes.Add(line[5..].Trim());
            }
            else if (line.StartsWith("Variant:", StringComparison.OrdinalIgnoreCase))
            {
                variants.Add(line[8..].Trim());
            }
            else if (line.StartsWith("Selected Variant:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line[17..].Trim(), out var selected))
            {
                selectedVariant = selected;
            }
        }

        selectedVariant ??= variants.Count > 0 ? variants.Count : null;

        return new ImportedItem(slot, rarity, name, baseType, text.RawText)
        {
            Sockets = sockets,
            Runes = runes,
            Variants = variants,
            SelectedVariant = selectedVariant,
            Text = text,
        };
    }

}
