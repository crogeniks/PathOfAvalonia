using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathOfAvalonia.TreeDomain.Import;

public static class Poe2BuildPlannerImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static Poe2BuildPlannerImportResult Import(string json, TreeModel tree)
    {
        if (tree.GameId != GameId.PathOfExile2)
        {
            throw new NotSupportedException("Build Planner import is only supported for Path of Exile 2.");
        }
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Build Planner file is empty.");
        }

        var file = JsonSerializer.Deserialize<BuildFile>(json, JsonOptions)
                   ?? throw new InvalidDataException("Build Planner JSON was empty.");

        var nodeIdByBuildPlannerId = tree.Nodes.Values
            .Where(node => !string.IsNullOrWhiteSpace(node.BuildPlannerId))
            .GroupBy(node => node.BuildPlannerId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

        var nodeIds = new List<int>();
        var allocationSets = new Dictionary<int, PassiveAllocationSet>();
        var skippedPassiveIds = new List<string>();
        var seen = new HashSet<int>();
        foreach (var passive in file.Passives ?? [])
        {
            if (string.IsNullOrWhiteSpace(passive.Id))
            {
                continue;
            }
            if (!nodeIdByBuildPlannerId.TryGetValue(passive.Id, out var nodeId))
            {
                skippedPassiveIds.Add(passive.Id);
                continue;
            }
            if (seen.Add(nodeId))
            {
                nodeIds.Add(nodeId);
            }
            if (WeaponSet(passive.WeaponSet) is { } allocationSet)
            {
                allocationSets[nodeId] = allocationSet;
            }
        }

        var (classInternalId, ascendancyInternalId) = ResolveAscendancy(file.Ascendancy, tree.Classes);
        var items = BuildItems(file.InventorySlots).ToArray();
        var skills = BuildSkills(file.Skills);
        var buildName = string.IsNullOrWhiteSpace(file.Name) ? "Build Planner" : file.Name;
        var build = new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: nodeIds,
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: tree.Version,
            Source: "build-planner")
        {
            ClassInternalId = classInternalId,
            AscendancyInternalId = ascendancyInternalId,
            AllocationSets = allocationSets,
            Items = items,
            ItemSetVariants = items.Length == 0
                ? []
                : [new ImportedItemSetVariant(0, 1, buildName, items)],
            Skills = skills,
            PassiveTreeVariants =
            [
                new ImportedPassiveTreeVariant(
                    0,
                    buildName,
                    0,
                    0,
                    0,
                    nodeIds,
                    [],
                    new Dictionary<int, int>(),
                    tree.Version,
                    2,
                    classInternalId,
                    ascendancyInternalId,
                    new Dictionary<int, AttributeNodeOverride>(),
                    [])
                {
                    AllocationSets = allocationSets,
                },
            ],
        };

        return new Poe2BuildPlannerImportResult(build, skippedPassiveIds);
    }

    private static (string? ClassInternalId, string? AscendancyInternalId) ResolveAscendancy(string? ascendancy, ClassCatalog classes)
    {
        if (string.IsNullOrWhiteSpace(ascendancy))
        {
            return (null, null);
        }

        foreach (var cls in classes.Classes)
        {
            foreach (var candidate in cls.Ascendancies)
            {
                if (string.Equals(candidate.InternalId, ascendancy, StringComparison.Ordinal)
                    || string.Equals(candidate.TreeName, ascendancy, StringComparison.Ordinal)
                    || string.Equals(candidate.DisplayName, ascendancy, StringComparison.OrdinalIgnoreCase))
                {
                    return (cls.Name, candidate.InternalId ?? ascendancy);
                }
            }
        }

        return (null, ascendancy);
    }

    private static PassiveAllocationSet? WeaponSet(int? weaponSet) =>
        weaponSet switch
        {
            1 => PassiveAllocationSet.WeaponSet1,
            2 => PassiveAllocationSet.WeaponSet2,
            _ => null,
        };

    private static ImportedSkills BuildSkills(IReadOnlyList<BuildSkill>? skills)
    {
        var groups = new List<ImportedSkillGroup>();
        foreach (var skill in skills ?? [])
        {
            if (string.IsNullOrWhiteSpace(skill.Id))
            {
                continue;
            }

            var gems = new List<ImportedGem>
            {
                BuildGem(skill.Id, enabled: true),
            };
            foreach (var support in skill.SupportSkills ?? [])
            {
                if (!string.IsNullOrWhiteSpace(support.Id))
                {
                    gems.Add(BuildGem(support.Id, enabled: true));
                }
            }

            groups.Add(new ImportedSkillGroup(
                groups.Count,
                GemDisplayName(skill.Id),
                null,
                null,
                true,
                false,
                1,
                0,
                0,
                gems));
        }

        return groups.Count == 0
            ? ImportedSkills.Empty
            : new ImportedSkills([new ImportedSkillSet(0, 1, "Build Planner", groups)], 0, 0);
    }

    private static ImportedGem BuildGem(string id, bool enabled) => new(
        GemDisplayName(id),
        id,
        null,
        null,
        null,
        null,
        enabled,
        false,
        false,
        1,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private static IEnumerable<ImportedItem> BuildItems(IReadOnlyList<BuildInventorySlot>? slots)
    {
        var id = 1;
        foreach (var slot in slots ?? [])
        {
            if (!BuildPlannerItemSlots.TryGetByInventoryId(slot.InventoryId, out var plannerSlot))
            {
                continue;
            }

            var isUnique = !string.IsNullOrWhiteSpace(slot.UniqueName);
            var name = isUnique ? slot.UniqueName! : ItemName(slot.AdditionalText) ?? "Build Planner Item";
            var baseType = isUnique ? slot.UniqueName! : ItemName(slot.AdditionalText) ?? "Build Planner Item";
            yield return new ImportedItem(
                plannerSlot.DisplayName,
                isUnique ? "Unique" : "Normal",
                name,
                baseType,
                BuildRawItemText(isUnique, name, baseType, slot.AdditionalText))
            {
                Id = id++,
            };
        }
    }

    private static string BuildRawItemText(bool isUnique, string name, string baseType, string? additionalText)
    {
        var lines = new List<string>
        {
            "Rarity: " + (isUnique ? "Unique" : "Normal"),
            name,
        };
        if (!isUnique && !string.Equals(name, baseType, StringComparison.Ordinal))
        {
            lines.Add(baseType);
        }
        if (!string.IsNullOrWhiteSpace(additionalText))
        {
            lines.Add("--------");
            lines.AddRange(additionalText.Replace("\r\n", "\n").Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        return string.Join('\n', lines);
    }

    private static string? ItemName(string? additionalText) =>
        additionalText?
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

    private static string GemDisplayName(string id)
    {
        var name = id.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? id;
        foreach (var prefix in new[] { "SkillGem", "SupportGem", "MetaGem" })
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        return SplitCamelCase(name);
    }

    private static string SplitCamelCase(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var result = new List<char> { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsUpper(ch) && !char.IsWhiteSpace(value[i - 1]) && !char.IsUpper(value[i - 1]))
            {
                result.Add(' ');
            }
            result.Add(ch);
        }

        return new string(result.ToArray());
    }

    private sealed record BuildFile(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("ascendancy")] string? Ascendancy,
        [property: JsonPropertyName("passives")] IReadOnlyList<BuildPassive>? Passives,
        [property: JsonPropertyName("skills")] IReadOnlyList<BuildSkill>? Skills,
        [property: JsonPropertyName("inventory_slots")] IReadOnlyList<BuildInventorySlot>? InventorySlots);

    private sealed record BuildPassive(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("weapon_set")] int? WeaponSet);

    private sealed record BuildSkill(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("support_skills")] IReadOnlyList<BuildSkillReference>? SupportSkills);

    private sealed record BuildSkillReference(
        [property: JsonPropertyName("id")] string? Id);

    private sealed record BuildInventorySlot(
        [property: JsonPropertyName("inventory_id")] string? InventoryId,
        [property: JsonPropertyName("unique_name")] string? UniqueName,
        [property: JsonPropertyName("additional_text")] string? AdditionalText);
}

public sealed record Poe2BuildPlannerImportResult(ImportedBuild Build, IReadOnlyList<string> SkippedPassiveIds);
