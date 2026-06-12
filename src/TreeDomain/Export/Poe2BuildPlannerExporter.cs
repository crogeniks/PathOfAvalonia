using System.Text.Json;
using System.Text.Json.Serialization;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Export;

public static class Poe2BuildPlannerExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Poe2BuildPlannerExportResult Export(ImportedBuild build, TreeModel tree, ClassCatalog classes)
    {
        return Export(build, tree, classes, BuildName(build), BuildSkills(build), BuildInventorySlots(build));
    }

    public static IReadOnlyList<Poe2BuildPlannerExportFile> ExportFiles(
        ImportedBuild build,
        TreeModel tree,
        ClassCatalog classes,
        string buildNamePrefix = "")
    {
        buildNamePrefix = buildNamePrefix.Trim();
        if (build.PassiveTreeVariants.Count == 0)
        {
            var name = PrefixedName(BuildName(build), buildNamePrefix);
            var export = Export(build, tree, classes, name, BuildSkills(build), BuildInventorySlots(build));
            return [new Poe2BuildPlannerExportFile(name, export)];
        }

        return build.PassiveTreeVariants
            .Select(variant =>
            {
                var name = PrefixedName(variant.DisplayName, buildNamePrefix);
                var variantBuild = build.WithPassiveTreeVariant(variant.Index);
                var skills = BuildSkills(MatchingSkillSet(build, variant.DisplayName));
                var inventorySlots = BuildInventorySlots(MatchingItems(build, variant.DisplayName));
                var export = Export(variantBuild, tree, classes, name, skills, inventorySlots);
                return new Poe2BuildPlannerExportFile(name, export);
            })
            .ToArray();
    }

    private static string PrefixedName(string name, string prefix) =>
        string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix} - {name}";

    private static Poe2BuildPlannerExportResult Export(
        ImportedBuild build,
        TreeModel tree,
        ClassCatalog classes,
        string name,
        IReadOnlyList<BuildSkill>? skills,
        IReadOnlyList<BuildInventorySlot>? inventorySlots)
    {
        if (tree.GameId != GameId.PathOfExile2)
        {
            throw new NotSupportedException("Build Planner export is only supported for Path of Exile 2.");
        }

        var skipped = new List<int>();
        var passives = new List<BuildPassive>();
        var seen = new HashSet<int>();
        foreach (var nodeId in build.NodeHashes.Concat(build.ClusterNodeHashes))
        {
            if (!seen.Add(nodeId))
            {
                continue;
            }
            if (!tree.Nodes.TryGetValue(nodeId, out var node)
                || string.IsNullOrWhiteSpace(node.BuildPlannerId))
            {
                skipped.Add(nodeId);
                continue;
            }

            var additionalText = AttributeOverrideText(build.AttributeOverrides.GetValueOrDefault(nodeId));
            var weaponSet = WeaponSetIndex(build.AllocationSets.GetValueOrDefault(nodeId));
            passives.Add(new BuildPassive(node.BuildPlannerId, weaponSet, additionalText));
        }

        var dto = new BuildFile(
            Name: name,
            Author: "PathOfAvalonia",
            Description: BuildDescription(build, skipped.Count),
            Ascendancy: ResolveAscendancy(build, classes),
            Passives: passives.Count == 0 ? null : passives,
            Skills: skills,
            InventorySlots: inventorySlots);

        return new Poe2BuildPlannerExportResult(JsonSerializer.Serialize(dto, JsonOptions), skipped);
    }

    private static string BuildName(ImportedBuild build)
    {
        var passiveVariant = build.PassiveTreeVariants.FirstOrDefault(v => v.Index == build.ActivePassiveTreeVariantIndex);
        if (!string.IsNullOrWhiteSpace(passiveVariant?.DisplayName))
        {
            return passiveVariant.DisplayName;
        }

        return "PathOfAvalonia Export";
    }

    private static string BuildDescription(ImportedBuild build, int skippedCount)
    {
        var parts = new List<string> { $"Exported from {build.Source}." };
        if (build.TreeVersion is not null)
        {
            parts.Add($"Tree version: {build.TreeVersion}.");
        }
        if (skippedCount > 0)
        {
            parts.Add($"Skipped {skippedCount} passive node(s) without Build Planner ids.");
        }
        return string.Join(" ", parts);
    }

    private static string? ResolveAscendancy(ImportedBuild build, ClassCatalog classes)
    {
        if (!string.IsNullOrWhiteSpace(build.AscendancyInternalId))
        {
            return build.AscendancyInternalId;
        }

        var classIndex = classes.ResolveClassIndex(build);
        var ascendancyIndex = classes.ResolveAscendancyIndex(classIndex, build);
        if (ascendancyIndex <= 0)
        {
            return null;
        }

        return classes.Classes
            .FirstOrDefault(c => c.ClassIndex == classIndex)?
            .Ascendancies
            .FirstOrDefault(a => a.AscendancyIndex == ascendancyIndex)?
            .InternalId;
    }

    private static int? WeaponSetIndex(PassiveAllocationSet allocationSet) =>
        allocationSet switch
        {
            PassiveAllocationSet.WeaponSet1 => 1,
            PassiveAllocationSet.WeaponSet2 => 2,
            _ => null,
        };

    private static string? AttributeOverrideText(AttributeNodeOverride overrideValue) =>
        overrideValue switch
        {
            AttributeNodeOverride.Strength => "<m>{<red>{Strength +5 is recommended}}",
            AttributeNodeOverride.Dexterity => "<m>{<green>{Dexterity +5 is recommended}}",
            AttributeNodeOverride.Intelligence => "<m>{<blue>{Intelligence +5 is recommended}}",
            _ => null,
        };

    private static ImportedSkillSet? MatchingSkillSet(ImportedBuild build, string passiveTreeName)
    {
        if (build.Skills.SkillSets.Count == 0)
        {
            return null;
        }

        if (build.Skills.SkillSets.Count == 1)
        {
            return build.Skills.SkillSets[0];
        }

        return build.Skills.SkillSets.FirstOrDefault(set => VariantNamesMatch(set.DisplayName, passiveTreeName));
    }

    private static IReadOnlyList<ImportedItem> MatchingItems(ImportedBuild build, string passiveTreeName)
    {
        if (build.ItemSetVariants.Count == 0)
        {
            return build.Items;
        }

        return build.ItemSetVariants.FirstOrDefault(set => VariantNamesMatch(set.DisplayName, passiveTreeName))?.Items
            ?? [];
    }

    private static bool VariantNamesMatch(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<BuildSkill>? BuildSkills(ImportedBuild build)
    {
        var activeSet = build.Skills.SkillSets.FirstOrDefault(set => set.Index == build.Skills.ActiveSkillSetIndex)
            ?? build.Skills.SkillSets.FirstOrDefault();
        return BuildSkills(activeSet);
    }

    private static IReadOnlyList<BuildSkill>? BuildSkills(ImportedSkillSet? activeSet)
    {
        if (activeSet is null)
        {
            return null;
        }

        var skills = new List<BuildSkill>();
        foreach (var group in activeSet.Groups.Where(group => group.Enabled))
        {
            var enabledGems = group.Gems
                .Select((gem, index) => (Gem: gem, Index: index))
                .Where(pair => pair.Gem.Enabled && !string.IsNullOrWhiteSpace(pair.Gem.GemId))
                .ToArray();
            var skillGems = enabledGems
                .Where(pair => IsSkillGem(pair.Gem.GemId))
                .ToArray();
            if (skillGems.Length == 0)
            {
                continue;
            }

            var mainSkill = skillGems.FirstOrDefault(pair => pair.Index == group.MainActiveSkillIndex);
            if (mainSkill.Gem is null)
            {
                mainSkill = skillGems[0];
            }

            var supportIds = new HashSet<string>(StringComparer.Ordinal);
            var supports = enabledGems
                .Where(pair => IsSupportGem(pair.Gem.GemId) && supportIds.Add(pair.Gem.GemId!))
                .Select(pair => new BuildSkillReference(pair.Gem.GemId!, DefaultLevelInterval()))
                .ToArray();

            skills.Add(new BuildSkill(
                mainSkill.Gem.GemId!,
                DefaultLevelInterval(),
                supports.Length == 0 ? null : supports));
        }

        return skills.Count == 0 ? null : skills;
    }

    private static IReadOnlyList<BuildInventorySlot>? BuildInventorySlots(ImportedBuild build) =>
        BuildInventorySlots(build.Items);

    private static IReadOnlyList<BuildInventorySlot>? BuildInventorySlots(IReadOnlyList<ImportedItem> items)
    {
        var slots = new List<BuildInventorySlot>();
        foreach (var item in items)
        {
            var inventoryId = InventoryId(item.Slot);
            if (inventoryId is null)
            {
                continue;
            }

            slots.Add(new BuildInventorySlot(
                inventoryId,
                IsUnique(item) && !string.IsNullOrWhiteSpace(item.Name) ? item.Name : null,
                IsUnique(item) ? null : BuildItemAdditionalText(item),
                DefaultLevelInterval()));
        }

        return slots.Count == 0 ? null : slots;
    }

    private static bool IsSkillGem(string? gemId) =>
        gemId is not null
        && gemId.Contains("/SkillGem", StringComparison.Ordinal)
        && !gemId.Contains("/MetaGem", StringComparison.Ordinal);

    private static bool IsSupportGem(string? gemId) =>
        gemId is not null && gemId.Contains("/SupportGem", StringComparison.Ordinal);

    private static int[] DefaultLevelInterval() => [1, 100];

    private static string? BuildItemAdditionalText(ImportedItem item)
    {
        var modIndex = 1;
        var textLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.BaseType))
        {
            textLines.Add(item.BaseType);
        }

        foreach (var line in item.RawText
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim()))
        {
            if (SkipItemLine(line, item))
            {
                continue;
            }

            textLines.Add($"{modIndex++}. {line}");
        }

        var text = string.Join('\n', textLines).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool SkipItemLine(string line, ImportedItem item)
    {
        return string.IsNullOrWhiteSpace(line)
            || line == "--------"
            || line.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Item Class:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Unique ID:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("LevelReq:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Requires Level", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Str:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Dex:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Int:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Requires Class ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Source:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Note:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Rune:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Implicits:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("BasePercentile: ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(line, item.Name, StringComparison.Ordinal)
            || string.Equals(line, item.BaseType, StringComparison.Ordinal);
    }


    private static bool IsUnique(ImportedItem item) =>
        string.Equals(item.Rarity, "Unique", StringComparison.OrdinalIgnoreCase);

    private static string? InventoryId(string slot) =>
        slot switch
        {
            "Weapon 1" => "Weapon1",
            "Weapon 2" => "Weapon2",
            "Weapon 1 Swap" => "Weapon1Swap",
            "Weapon 2 Swap" => "Weapon2Swap",
            "Helmet" => "Helm1",
            "Body Armour" => "BodyArmour1",
            "Gloves" => "Gloves1",
            "Boots" => "Boots1",
            "Amulet" => "Amulet1",
            "Ring 1" => "Ring1",
            "Ring 2" => "Ring2",
            "Belt" => "Belt1",
            "Flask 1" => "Flask1",
            "Flask 2" => "Flask2",
            "Flask 3" => "Flask3",
            "Flask 4" => "Flask4",
            "Flask 5" => "Flask5",
            "Charm 1" => "Charm1",
            "Charm 2" => "Charm2",
            _ => null,
        };

    private sealed record BuildFile(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("author")] string? Author,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("ascendancy")] string? Ascendancy,
        [property: JsonPropertyName("passives")] IReadOnlyList<BuildPassive>? Passives,
        [property: JsonPropertyName("skills")] IReadOnlyList<BuildSkill>? Skills,
        [property: JsonPropertyName("inventory_slots")] IReadOnlyList<BuildInventorySlot>? InventorySlots);

    private sealed record BuildPassive(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("weapon_set")] int? WeaponSet,
        [property: JsonPropertyName("additional_text")] string? AdditionalText);

    private sealed record BuildSkill(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("level_interval")] int[] LevelInterval,
        [property: JsonPropertyName("support_skills")] IReadOnlyList<BuildSkillReference>? SupportSkills);

    private sealed record BuildSkillReference(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("level_interval")] int[] LevelInterval);

    private sealed record BuildInventorySlot(
        [property: JsonPropertyName("inventory_id")] string InventoryId,
        [property: JsonPropertyName("unique_name")] string? UniqueName,
        [property: JsonPropertyName("additional_text")] string? AdditionalText,
        [property: JsonPropertyName("level_interval")] int[] LevelInterval);
}

public sealed record Poe2BuildPlannerExportResult(string Json, IReadOnlyList<int> SkippedNodeIds);

public sealed record Poe2BuildPlannerExportFile(string Name, Poe2BuildPlannerExportResult Export);
