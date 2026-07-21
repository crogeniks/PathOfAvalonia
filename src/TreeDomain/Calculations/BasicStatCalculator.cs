using System.Globalization;
using System.Text.RegularExpressions;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Calculations;

public sealed record BasicResistance(int Uncapped, int Maximum)
{
    public int Capped => Math.Min(Uncapped, Maximum);
    public int OverCap => Math.Max(Uncapped - Maximum, 0);
}

public sealed record BasicStatCoverage(
    int AppliedLineCount,
    int UnsupportedRelevantLineCount,
    IReadOnlyList<string> UnsupportedExamples,
    bool HasIncompleteItemDefences,
    bool HasIncompleteShieldBlock)
{
    public bool IsPartial => UnsupportedRelevantLineCount > 0
        || HasIncompleteItemDefences
        || HasIncompleteShieldBlock;
}

/// <summary>
/// An intentionally small, deterministic subset of PoB's player output. It
/// covers unconditional basic-stat modifiers and does not claim to evaluate
/// buffs, conditions, keystones, reservations, flasks, or skill damage.
/// </summary>
public sealed record BasicCharacterStats(
    int Level,
    int Strength,
    int Dexterity,
    int Intelligence,
    int Life,
    int Mana,
    int EnergyShield,
    int Armour,
    int Evasion,
    int Ward,
    double LifeRegeneration,
    double ManaRegeneration,
    int BlockChance,
    int SpellBlockChance,
    int SpellSuppressionChance,
    int MovementSpeedModifier,
    BasicResistance FireResistance,
    BasicResistance ColdResistance,
    BasicResistance LightningResistance,
    BasicResistance ChaosResistance,
    BasicStatCoverage Coverage);

public static partial class BasicStatCalculator
{
    public const int WorstResistancePenalty = -60;
    private const int DefaultMaximumResistance = 75;
    private const int DefaultMaximumBlock = 75;

    public static BasicCharacterStats Calculate(
        PassiveSpec spec,
        IEnumerable<ImportedItem> items,
        int level,
        int activeWeaponSet = 1,
        PassiveAllocationPreview? passivePreview = null)
    {
        level = Math.Clamp(level, 1, 100);
        activeWeaponSet = activeWeaponSet == 2 ? 2 : 1;
        var totals = new ModifierTotals();
        var coverage = new CoverageBuilder();
        var allocationSet = activeWeaponSet == 2
            ? PassiveAllocationSet.WeaponSet2
            : PassiveAllocationSet.WeaponSet1;

        foreach (var line in spec.GetAllocatedStatLines(allocationSet, passivePreview))
        {
            ParseModifierLine(line, totals, coverage, localDefences: null);
        }

        foreach (var item in items.Where(item => AppliesToCurrentSetup(item, activeWeaponSet)))
        {
            ParseItem(item, totals, coverage);
        }

        var classInfo = spec.Classes.GetClass(spec.SelectedClassIndex);
        var strength = Round(totals.Value(BasicStat.Strength, classInfo.BaseStrength));
        var dexterity = Round(totals.Value(BasicStat.Dexterity, classInfo.BaseDexterity));
        var intelligence = Round(totals.Value(BasicStat.Intelligence, classInfo.BaseIntelligence));
        var isPoe2 = spec.Tree.GameId == GameId.PathOfExile2;

        var baseLife = isPoe2
            ? 16 + 12 * level + strength * 2
            : 38 + 12 * level + Math.Floor(strength / 2d);
        var baseMana = isPoe2
            ? 30 + 4 * level + intelligence * 2
            : 34 + 6 * level + Math.Floor(intelligence / 2d);
        var life = Math.Max(Round(totals.Value(BasicStat.Life, baseLife)), 1);
        var mana = Math.Max(Round(totals.Value(BasicStat.Mana, baseMana)), 0);

        var inherentEnergyShieldIncrease = isPoe2 ? 0 : Math.Floor(intelligence / 10d);
        var energyShield = Math.Max(Round(totals.Value(
            BasicStat.EnergyShield,
            0,
            additionalIncrease: inherentEnergyShieldIncrease)), 0);
        var armour = Math.Max(Round(totals.Value(BasicStat.Armour)), 0);
        var inherentEvasionIncrease = isPoe2 ? 0 : Math.Floor(dexterity / 5d);
        var evasion = Math.Max(Round(totals.Value(
            BasicStat.Evasion,
            isPoe2 ? 7 : 15,
            additionalIncrease: inherentEvasionIncrease)), 0);
        var ward = Math.Max(Round(totals.Value(BasicStat.Ward)), 0);

        var lifeRegeneration = Math.Max(
            totals.Flat(BasicStat.LifeRegeneration)
            + life * totals.Flat(BasicStat.LifeRegenerationPercent) / 100,
            0);
        lifeRegeneration *= 1 + totals.Increase(BasicStat.LifeRegeneration) / 100;
        var inherentManaRegeneration = mana * (isPoe2 ? 0.04 : 0.0175);
        var manaRegeneration = Math.Max(
            inherentManaRegeneration
            + totals.Flat(BasicStat.ManaRegeneration)
            + mana * totals.Flat(BasicStat.ManaRegenerationPercent) / 100,
            0);
        manaRegeneration *= 1 + totals.Increase(BasicStat.ManaRegeneration) / 100;

        var maximumBlock = Round(totals.Value(BasicStat.MaximumBlockChance, DefaultMaximumBlock));
        var maximumSpellBlock = Round(totals.Value(BasicStat.MaximumSpellBlockChance, DefaultMaximumBlock));
        var block = Math.Clamp(Round(totals.Value(BasicStat.BlockChance)), 0, maximumBlock);
        var spellBlock = Math.Clamp(Round(totals.Value(BasicStat.SpellBlockChance)), 0, maximumSpellBlock);
        var suppression = Math.Clamp(Round(totals.Value(BasicStat.SpellSuppressionChance)), 0, 100);

        var elementalPenalty = WorstResistancePenalty;
        var chaosPenalty = isPoe2 ? 0 : WorstResistancePenalty;
        var fire = Resistance(totals, BasicStat.FireResistance, BasicStat.MaximumFireResistance, elementalPenalty);
        var cold = Resistance(totals, BasicStat.ColdResistance, BasicStat.MaximumColdResistance, elementalPenalty);
        var lightning = Resistance(totals, BasicStat.LightningResistance, BasicStat.MaximumLightningResistance, elementalPenalty);
        var chaos = Resistance(totals, BasicStat.ChaosResistance, BasicStat.MaximumChaosResistance, chaosPenalty);

        return new BasicCharacterStats(
            level,
            strength,
            dexterity,
            intelligence,
            life,
            mana,
            energyShield,
            armour,
            evasion,
            ward,
            lifeRegeneration,
            manaRegeneration,
            block,
            spellBlock,
            suppression,
            Round(totals.Flat(BasicStat.MovementSpeed) + totals.Increase(BasicStat.MovementSpeed)),
            fire,
            cold,
            lightning,
            chaos,
            coverage.Build());
    }

    private static BasicResistance Resistance(
        ModifierTotals totals,
        BasicStat resistance,
        BasicStat maximumResistance,
        int penalty)
    {
        var uncapped = Round(totals.Value(resistance, penalty));
        var maximum = Math.Clamp(
            Round(totals.Value(maximumResistance, DefaultMaximumResistance)),
            -200,
            90);
        return new BasicResistance(uncapped, maximum);
    }

    private static bool AppliesToCurrentSetup(ImportedItem item, int activeWeaponSet)
    {
        if (item.Slot.Contains("Flask", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (item.Slot.EndsWith(" Swap", StringComparison.OrdinalIgnoreCase))
        {
            return activeWeaponSet == 2;
        }
        if (item.Slot is "Weapon 1" or "Weapon 2")
        {
            return activeWeaponSet == 1;
        }
        return true;
    }

    private static void ParseItem(ImportedItem item, ModifierTotals totals, CoverageBuilder coverage)
    {
        var localDefences = new HashSet<BasicStat>();
        var propertyDefences = new HashSet<BasicStat>();
        var isDefensiveEquipment = IsDefensiveEquipment(item);
        var isShield = IsShield(item);
        if (isDefensiveEquipment)
        {
            localDefences.UnionWith(
            [
                BasicStat.Armour,
                BasicStat.Evasion,
                BasicStat.EnergyShield,
                BasicStat.Ward,
            ]);
        }
        foreach (var line in item.Text.BodyLines.Where(line => AppliesToSelectedVariant(line, item.SelectedVariant)))
        {
            if (TryParseItemProperty(line.Text, totals, out var propertyStat))
            {
                if (IsDefence(propertyStat))
                {
                    propertyDefences.Add(propertyStat);
                }
                coverage.Applied(line.Text);
            }
        }
        if (isDefensiveEquipment && propertyDefences.Count == 0)
        {
            coverage.IncompleteItemDefences(item.BaseType);
        }
        if (isShield && !item.Text.BodyLines.Any(line =>
                line.Text.StartsWith("Chance to Block:", StringComparison.OrdinalIgnoreCase)
                || line.Text.StartsWith("Block Chance:", StringComparison.OrdinalIgnoreCase)))
        {
            coverage.IncompleteShieldBlock(item.BaseType);
        }
        foreach (var line in item.Text.BodyLines.Where(line => AppliesToSelectedVariant(line, item.SelectedVariant)))
        {
            ParseModifierLine(line.Text, totals, coverage, localDefences);
        }
    }

    private static bool IsDefensiveEquipment(ImportedItem item) =>
        item.Slot is "Helmet" or "Body Armour" or "Gloves" or "Boots"
        || IsShield(item);

    private static bool IsShield(ImportedItem item) =>
        item.BaseType.Contains("Shield", StringComparison.OrdinalIgnoreCase)
        || item.BaseType.Contains("Buckler", StringComparison.OrdinalIgnoreCase);

    private static bool AppliesToSelectedVariant(ItemTextLine line, int? selectedVariant)
    {
        var variantTags = line.Tags
            .Where(tag => tag.StartsWith("variant:", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (variantTags.Length == 0)
        {
            return true;
        }
        if (selectedVariant is null)
        {
            return false;
        }
        return variantTags.Any(tag => tag[8..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => int.TryParse(value, out var variant) && variant == selectedVariant));
    }

    private static bool TryParseItemProperty(
        string line,
        ModifierTotals totals,
        out BasicStat stat)
    {
        stat = BasicStat.None;
        var match = ItemPropertyRegex().Match(line);
        if (!match.Success || !TryNumber(match.Groups[2].Value, out var value))
        {
            return false;
        }

        stat = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "armour" => BasicStat.Armour,
            "evasion rating" => BasicStat.Evasion,
            "energy shield" => BasicStat.EnergyShield,
            "ward" => BasicStat.Ward,
            "chance to block" or "block chance" => BasicStat.BlockChance,
            _ => BasicStat.None,
        };
        if (stat == BasicStat.None)
        {
            return false;
        }
        totals.Add(stat, ModifierKind.Flat, value);
        return true;
    }

    private static void ParseModifierLine(
        string rawLine,
        ModifierTotals totals,
        CoverageBuilder coverage,
        IReadOnlySet<BasicStat>? localDefences)
    {
        var line = rawLine.Trim().TrimEnd('.');
        if (line.Length == 0 || line.StartsWith("---", StringComparison.Ordinal))
        {
            return;
        }
        if (ItemPropertyRegex().IsMatch(line))
        {
            return;
        }

        if (TryParseRegeneration(line, totals)
            || TryParseChance(line, totals)
            || TryParseScaledModifier(line, totals, localDefences)
            || TryParseFlatModifier(line, totals, localDefences))
        {
            coverage.Applied(rawLine);
        }
        else if (LooksRelevant(line))
        {
            coverage.Unsupported(rawLine);
        }
    }

    private static bool TryParseRegeneration(string line, ModifierTotals totals)
    {
        var match = PercentRegenerationRegex().Match(line);
        if (match.Success && TryNumber(match.Groups[1].Value, out var percent))
        {
            var stat = match.Groups[2].Value.Equals("Life", StringComparison.OrdinalIgnoreCase)
                ? BasicStat.LifeRegenerationPercent
                : BasicStat.ManaRegenerationPercent;
            totals.Add(stat, ModifierKind.Flat, percent);
            return true;
        }

        match = FlatRegenerationRegex().Match(line);
        if (match.Success && TryNumber(match.Groups[1].Value, out var flat))
        {
            var stat = match.Groups[2].Value.Equals("Life", StringComparison.OrdinalIgnoreCase)
                ? BasicStat.LifeRegeneration
                : BasicStat.ManaRegeneration;
            totals.Add(stat, ModifierKind.Flat, flat);
            return true;
        }
        return false;
    }

    private static bool TryParseChance(string line, ModifierTotals totals)
    {
        var match = ChanceRegex().Match(line);
        if (!match.Success || !TryNumber(match.Groups[1].Value, out var value))
        {
            return false;
        }
        var stat = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "suppress spell damage" => BasicStat.SpellSuppressionChance,
            "block attack damage" or "block attacks" => BasicStat.BlockChance,
            "block spell damage" or "block spells" => BasicStat.SpellBlockChance,
            _ => BasicStat.None,
        };
        if (stat == BasicStat.None)
        {
            return false;
        }
        totals.Add(stat, ModifierKind.Flat, value);
        return true;
    }

    private static bool TryParseScaledModifier(
        string line,
        ModifierTotals totals,
        IReadOnlySet<BasicStat>? localDefences)
    {
        var match = ScaledModifierRegex().Match(line);
        if (!match.Success || !TryNumber(match.Groups[1].Value, out var value))
        {
            return false;
        }
        var kindText = match.Groups[2].Value;
        var kind = kindText.Equals("more", StringComparison.OrdinalIgnoreCase)
            || kindText.Equals("less", StringComparison.OrdinalIgnoreCase)
                ? ModifierKind.More
                : ModifierKind.Increased;
        if (kindText.Equals("reduced", StringComparison.OrdinalIgnoreCase)
            || kindText.Equals("less", StringComparison.OrdinalIgnoreCase))
        {
            value = -value;
        }

        var target = match.Groups[3].Value;
        var stats = ResolveTarget(target, forMaximumResistance: false);
        if (stats.Count == 0)
        {
            return false;
        }
        var globalDefence = target.Contains("Global Defences", StringComparison.OrdinalIgnoreCase);
        foreach (var stat in stats)
        {
            if (!globalDefence && localDefences?.Contains(stat) == true && IsDefence(stat))
            {
                continue;
            }
            totals.Add(stat, kind, value);
        }
        return true;
    }

    private static bool TryParseFlatModifier(
        string line,
        ModifierTotals totals,
        IReadOnlySet<BasicStat>? localDefences)
    {
        var match = FlatModifierRegex().Match(line);
        if (!match.Success || !TryNumber(match.Groups[1].Value, out var value))
        {
            return false;
        }
        var target = match.Groups[3].Value;
        var stats = ResolveTarget(target, target.Contains("maximum", StringComparison.OrdinalIgnoreCase));
        if (stats.Count == 0)
        {
            return false;
        }
        foreach (var stat in stats)
        {
            if (localDefences?.Contains(stat) == true && IsDefence(stat))
            {
                continue;
            }
            totals.Add(stat, ModifierKind.Flat, value);
        }
        return true;
    }

    private static IReadOnlyList<BasicStat> ResolveTarget(string rawTarget, bool forMaximumResistance)
    {
        var target = rawTarget
            .Replace(" Rating", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("maximum ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        var normalized = target.ToLowerInvariant();
        if (normalized is "all attributes" or "attributes")
        {
            return [BasicStat.Strength, BasicStat.Dexterity, BasicStat.Intelligence];
        }
        if (normalized is "all elemental resistances" or "elemental resistances")
        {
            return forMaximumResistance
                ? [BasicStat.MaximumFireResistance, BasicStat.MaximumColdResistance, BasicStat.MaximumLightningResistance]
                : [BasicStat.FireResistance, BasicStat.ColdResistance, BasicStat.LightningResistance];
        }
        if (normalized == "all resistances")
        {
            return forMaximumResistance
                ? [BasicStat.MaximumFireResistance, BasicStat.MaximumColdResistance, BasicStat.MaximumLightningResistance, BasicStat.MaximumChaosResistance]
                : [BasicStat.FireResistance, BasicStat.ColdResistance, BasicStat.LightningResistance, BasicStat.ChaosResistance];
        }
        if (normalized == "global defences")
        {
            return [BasicStat.Armour, BasicStat.Evasion, BasicStat.EnergyShield, BasicStat.Ward];
        }

        if (normalized.EndsWith(" resistances", StringComparison.Ordinal)
            && !normalized.StartsWith("all ", StringComparison.Ordinal))
        {
            var resistanceParts = normalized[..^" resistances".Length]
                .Replace(", and ", ", ", StringComparison.Ordinal)
                .Replace(" and ", ", ", StringComparison.Ordinal)
                .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var resistances = resistanceParts
                .Select(part => ResolveSingleTarget(part + " resistance", forMaximumResistance))
                .Where(stat => stat != BasicStat.None)
                .Distinct()
                .ToArray();
            if (resistances.Length == resistanceParts.Length)
            {
                return resistances;
            }
        }

        var exact = ResolveSingleTarget(normalized, forMaximumResistance);
        if (exact != BasicStat.None)
        {
            return [exact];
        }

        var parts = normalized
            .Replace(", and ", ", ", StringComparison.Ordinal)
            .Replace(" and ", ", ", StringComparison.Ordinal)
            .Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            return [];
        }
        var result = parts
            .Select(part => ResolveSingleTarget(part, forMaximumResistance))
            .Where(stat => stat != BasicStat.None)
            .Distinct()
            .ToArray();
        return result.Length == parts.Length ? result : [];
    }

    private static BasicStat ResolveSingleTarget(string target, bool maximumResistance) => target switch
    {
        "strength" => BasicStat.Strength,
        "dexterity" => BasicStat.Dexterity,
        "intelligence" => BasicStat.Intelligence,
        "life" => BasicStat.Life,
        "mana" => BasicStat.Mana,
        "energy shield" => BasicStat.EnergyShield,
        "armour" => BasicStat.Armour,
        "evasion" => BasicStat.Evasion,
        "ward" => BasicStat.Ward,
        "fire resistance" => maximumResistance ? BasicStat.MaximumFireResistance : BasicStat.FireResistance,
        "cold resistance" => maximumResistance ? BasicStat.MaximumColdResistance : BasicStat.ColdResistance,
        "lightning resistance" => maximumResistance ? BasicStat.MaximumLightningResistance : BasicStat.LightningResistance,
        "chaos resistance" => maximumResistance ? BasicStat.MaximumChaosResistance : BasicStat.ChaosResistance,
        "block chance" or "chance to block attack damage" => BasicStat.BlockChance,
        "spell block chance" or "chance to block spell damage" => BasicStat.SpellBlockChance,
        "block chance cap" => BasicStat.MaximumBlockChance,
        "spell block chance cap" => BasicStat.MaximumSpellBlockChance,
        "spell suppression chance" or "chance to suppress spell damage" => BasicStat.SpellSuppressionChance,
        "movement speed" => BasicStat.MovementSpeed,
        "life regeneration rate" => BasicStat.LifeRegeneration,
        "mana regeneration rate" => BasicStat.ManaRegeneration,
        _ => BasicStat.None,
    };

    private static bool IsDefence(BasicStat stat) =>
        stat is BasicStat.Armour or BasicStat.Evasion or BasicStat.EnergyShield or BasicStat.Ward;

    private static bool LooksRelevant(string line) => RelevantKeywords.Any(keyword =>
        line.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(
            text.Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    private static int Round(double value) => (int)Math.Floor(value + 0.5);

    private static readonly string[] RelevantKeywords =
    [
        "Strength", "Dexterity", "Intelligence", "Attribute", "Life", "Mana",
        "Energy Shield", "Armour", "Evasion", "Ward", "Resistance", "Block",
        "Suppress Spell", "Movement Speed", "Defences",
    ];

    [GeneratedRegex(@"^(Armour|Evasion Rating|Energy Shield|Ward|Chance to Block|Block Chance):\s*([\d,.]+)%?", RegexOptions.IgnoreCase)]
    private static partial Regex ItemPropertyRegex();

    [GeneratedRegex(@"^(?:Regenerate\s+)?([+-]?[\d,.]+(?:\.\d+)?)% of (Life|Mana)(?: is Regenerated)? per second$", RegexOptions.IgnoreCase)]
    private static partial Regex PercentRegenerationRegex();

    [GeneratedRegex(@"^\+?([+-]?[\d,.]+(?:\.\d+)?) (Life|Mana) Regenerated per second$", RegexOptions.IgnoreCase)]
    private static partial Regex FlatRegenerationRegex();

    [GeneratedRegex(@"^\+?([+-]?[\d,.]+(?:\.\d+)?)% (?:Chance|chance) to (Suppress Spell Damage|Block Attack Damage|Block Attacks|Block Spell Damage|Block Spells)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChanceRegex();

    [GeneratedRegex(@"^([+-]?[\d,.]+(?:\.\d+)?)% (increased|reduced|more|less) (.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ScaledModifierRegex();

    [GeneratedRegex(@"^\+?([+-]?[\d,.]+(?:\.\d+)?)(%)? to (.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FlatModifierRegex();

    private enum BasicStat
    {
        None,
        Strength,
        Dexterity,
        Intelligence,
        Life,
        Mana,
        EnergyShield,
        Armour,
        Evasion,
        Ward,
        LifeRegeneration,
        LifeRegenerationPercent,
        ManaRegeneration,
        ManaRegenerationPercent,
        FireResistance,
        ColdResistance,
        LightningResistance,
        ChaosResistance,
        MaximumFireResistance,
        MaximumColdResistance,
        MaximumLightningResistance,
        MaximumChaosResistance,
        BlockChance,
        SpellBlockChance,
        MaximumBlockChance,
        MaximumSpellBlockChance,
        SpellSuppressionChance,
        MovementSpeed,
    }

    private enum ModifierKind
    {
        Flat,
        Increased,
        More,
    }

    private sealed class ModifierTotals
    {
        private readonly Dictionary<BasicStat, double> _flat = [];
        private readonly Dictionary<BasicStat, double> _increased = [];
        private readonly Dictionary<BasicStat, List<double>> _more = [];

        public void Add(BasicStat stat, ModifierKind kind, double value)
        {
            switch (kind)
            {
                case ModifierKind.Flat:
                    _flat[stat] = Flat(stat) + value;
                    break;
                case ModifierKind.Increased:
                    _increased[stat] = Increase(stat) + value;
                    break;
                case ModifierKind.More:
                    if (!_more.TryGetValue(stat, out var values))
                    {
                        values = [];
                        _more[stat] = values;
                    }
                    values.Add(value);
                    break;
            }
        }

        public double Flat(BasicStat stat) => _flat.GetValueOrDefault(stat);
        public double Increase(BasicStat stat) => _increased.GetValueOrDefault(stat);

        public double Value(BasicStat stat, double intrinsicBase = 0, double additionalIncrease = 0)
        {
            var more = _more.TryGetValue(stat, out var values)
                ? values.Aggregate(1d, (current, value) => current * (1 + value / 100))
                : 1d;
            return (intrinsicBase + Flat(stat)) * (1 + (Increase(stat) + additionalIncrease) / 100) * more;
        }
    }

    private sealed class CoverageBuilder
    {
        private readonly List<string> _unsupportedExamples = [];
        private int _applied;
        private int _unsupported;
        private bool _hasIncompleteItemDefences;
        private bool _hasIncompleteShieldBlock;

        public void Applied(string line) => _applied++;

        public void Unsupported(string line)
        {
            _unsupported++;
            AddExample(line);
        }

        public void IncompleteItemDefences(string baseType)
        {
            _hasIncompleteItemDefences = true;
            AddExample($"{baseType}: saved item text has no final defence properties");
        }

        public void IncompleteShieldBlock(string baseType)
        {
            _hasIncompleteShieldBlock = true;
            AddExample($"{baseType}: saved item text has no final block property");
        }

        private void AddExample(string line)
        {
            if (_unsupportedExamples.Count < 3 && !_unsupportedExamples.Contains(line, StringComparer.Ordinal))
            {
                _unsupportedExamples.Add(line);
            }
        }

        public BasicStatCoverage Build() => new(
            _applied,
            _unsupported,
            _unsupportedExamples.ToArray(),
            _hasIncompleteItemDefences,
            _hasIncompleteShieldBlock);
    }
}
