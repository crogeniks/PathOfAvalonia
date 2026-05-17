namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed class JewelRadiusTable
{
    private readonly Dictionary<int, JewelRadiusBand> _byIndex;

    private JewelRadiusTable(IReadOnlyList<JewelRadiusBand> bands)
    {
        Bands = bands;
        _byIndex = bands.ToDictionary(b => b.Index);
        MaxOuterRadius = bands.Count == 0 ? 0 : bands.Max(b => b.Outer);
    }

    public IReadOnlyList<JewelRadiusBand> Bands { get; }
    public double MaxOuterRadius { get; }

    public JewelRadiusBand this[int index] => _byIndex[index];

    public bool TryGet(int index, out JewelRadiusBand band) => _byIndex.TryGetValue(index, out band!);

    public int? NormalRadiusIndex(string label)
    {
        foreach (var band in Bands)
        {
            if (!band.IsAnnulus && band.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return band.Index;
            }
        }
        return null;
    }

    public int? AnnulusRadiusIndex(string label)
    {
        var normalized = NormalizeRingLabel(label);
        return Bands.FirstOrDefault(b => b.IsAnnulus && NormalizeRingLabel(b.Label) == normalized)?.Index;
    }

    public static JewelRadiusTable For(GameId gameId, string treeVersion) =>
        gameId == GameId.PathOfExile2 ? Poe2_0_1() : Poe1(treeVersion);

    private static JewelRadiusTable Poe1(string treeVersion)
    {
        if (UsePoe1_3_15(treeVersion))
        {
            return new JewelRadiusTable(
            [
                new(1, "Small", 0, 800, "#BB6600"),
                new(2, "Medium", 0, 1200, "#66FFCC"),
                new(3, "Large", 0, 1500, "#2222CC"),
                new(4, "Small Ring", 850, 1100, "#D35400"),
                new(5, "Medium Ring", 1150, 1400, "#66FFCC"),
                new(6, "Large Ring", 1450, 1700, "#2222CC"),
                new(7, "Very Large Ring", 1750, 2000, "#C100FF"),
                new(8, "Massive Ring", 1750, 2000, "#C100FF"),
            ]);
        }

        return new JewelRadiusTable(
        [
            new(1, "Small", 0, 960, "#BB6600"),
            new(2, "Medium", 0, 1440, "#66FFCC"),
            new(3, "Large", 0, 1800, "#2222CC"),
            new(4, "Very Large", 0, 2400, "#C100FF"),
            new(5, "Massive", 0, 2880, "#0B9300"),
            new(6, "Small Ring", 960, 1320, "#D35400"),
            new(7, "Medium Ring", 1320, 1680, "#66FFCC"),
            new(8, "Large Ring", 1680, 2040, "#2222CC"),
            new(9, "Very Large Ring", 2040, 2400, "#C100FF"),
            new(10, "Massive Ring", 2400, 2880, "#0B9300"),
        ]);
    }

    private static JewelRadiusTable Poe2_0_1()
    {
        const double multiplier = 1.2;
        return new JewelRadiusTable(
        [
            Band(1, "Small", 0, 1000, "#BB6600"),
            Band(2, "Medium", 0, 1150, "#66FFCC"),
            Band(3, "Large", 0, 1300, "#2222CC"),
            Band(4, "Very Large", 0, 1500, "#C100FF"),
            Band(5, "Very Small Ring", 650, 950, "#D35400"),
            Band(6, "Small Ring", 800, 1100, "#66FFCC"),
            Band(7, "Medium-Small Ring", 950, 1250, "#2222CC"),
            Band(8, "Medium Ring", 1100, 1400, "#C100FF"),
            Band(9, "Medium-Large Ring", 1250, 1550, "#0B9300"),
            Band(10, "Large Ring", 1400, 1700, "#FFCC00"),
            Band(11, "Very Large Ring", 1650, 1950, "#FF6600"),
            Band(12, "Massive Ring", 1800, 2100, "#0099FF"),
        ]);

        static JewelRadiusBand Band(int index, string label, double inner, double outer, string color) =>
            new(index, label, inner * multiplier, outer * multiplier, color);
    }

    private static bool UsePoe1_3_15(string treeVersion)
    {
        var parts = treeVersion.Replace('_', '.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && int.TryParse(parts[0], out var major)
            && int.TryParse(parts[1], out var minor)
            && major <= 3
            && minor <= 15;
    }

    private static string NormalizeRingLabel(string label) =>
        label.Replace("Only affects Passives in ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Affects Passives in ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" Ring", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToUpperInvariant();
}
