namespace PathOfAvalonia.TreeDomain.Import;

public static class ItemVariant
{
    public static bool IsActive(string line, int? selectedVariant)
    {
        var variantIds = VariantIds(line);
        return variantIds.Count == 0 || selectedVariant is null || variantIds.Contains(selectedVariant.Value);
    }

    private static IReadOnlySet<int> VariantIds(string line)
    {
        var ids = new HashSet<int>();
        var remaining = line.AsSpan();
        while (true)
        {
            var start = remaining.IndexOf("{variant:", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return ids;
            }
            remaining = remaining[(start + "{variant:".Length)..];
            var end = remaining.IndexOf('}');
            if (end < 0)
            {
                return ids;
            }
            foreach (var value in remaining[..end].ToString().Split(',', StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(value, out var id))
                {
                    ids.Add(id);
                }
            }
            remaining = remaining[(end + 1)..];
        }
    }
}
