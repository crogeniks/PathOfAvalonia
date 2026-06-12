using System.Text.Json;
using PathOfAvalonia.TreeDomain.Export;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2BuildPlannerExporterTests
{
    [Fact]
    public void ExportsBuildPlannerJsonWithOfficialPassiveIds()
    {
        var tree = CreateTree();
        var build = new ImportedBuild(
            ClassId: 2,
            AscendClassId: 1,
            SecondaryAscendClassId: 0,
            NodeHashes: [10, 11, 12, 99],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "0.5.0",
            Source: "test")
        {
            AscendancyInternalId = "Warrior1",
            AttributeOverrides = new Dictionary<int, AttributeNodeOverride>
            {
                [11] = AttributeNodeOverride.Strength,
            },
            AllocationSets = new Dictionary<int, PassiveAllocationSet>
            {
                [12] = PassiveAllocationSet.WeaponSet2,
            },
            Items =
            [
                new ImportedItem(
                    "Body Armour",
                    "Rare",
                    "Dire Shell",
                    "Expert Hexer's Robe",
                    "Rarity: Rare\nDire Shell\nExpert Hexer's Robe\n--------\nLevelReq: 70\n--------\n+50 to maximum Life\n+35% to Cold Resistance")
                {
                    Id = 1,
                },
                new ImportedItem(
                    "Ring 1",
                    "Unique",
                    "Kalandra's Touch",
                    "Ring",
                    "Rarity: Unique\nKalandra's Touch\nRing")
                {
                    Id = 2,
                },
            ],
            Skills = new ImportedSkills(
                [
                    new ImportedSkillSet(
                        0,
                        1,
                        "Skills",
                        [
                            new ImportedSkillGroup(
                                0,
                                "Earthquake",
                                "Body Armour",
                                null,
                                true,
                                false,
                                1,
                                0,
                                0,
                                [
                                    Gem("Earthquake", "Metadata/Items/Gems/SkillGemEarthquake", 20, 20),
                                    Gem("Fast Forward", "Metadata/Items/Gems/SupportGemFastForward", null, null),
                                    Gem("Aftershock", "Metadata/Items/Gems/SupportGemAftershock", null, null),
                                ])
                        ])
                ],
                0,
                0),
            PassiveTreeVariants =
            [
                new ImportedPassiveTreeVariant(
                    0,
                    "Titan Warrior",
                    2,
                    1,
                    0,
                    [10, 11, 12, 99],
                    [],
                    new Dictionary<int, int>(),
                    "0.5.0",
                    2,
                    "Warrior",
                    "Warrior1",
                    new Dictionary<int, AttributeNodeOverride>(),
                    [])
            ],
        };

        var result = Poe2BuildPlannerExporter.Export(build, tree, tree.Classes);
        using var document = JsonDocument.Parse(result.Json);
        var root = document.RootElement;
        var passives = root.GetProperty("passives");

        Assert.Equal("Titan Warrior", root.GetProperty("name").GetString());
        Assert.Equal("Warrior1", root.GetProperty("ascendancy").GetString());
        Assert.Equal("melee17", passives[0].GetProperty("id").GetString());
        Assert.Equal("strength89", passives[1].GetProperty("id").GetString());
        Assert.Contains("Strength +5", passives[1].GetProperty("additional_text").GetString());
        Assert.Equal("melee18", passives[2].GetProperty("id").GetString());
        Assert.Equal(2, passives[2].GetProperty("weapon_set").GetInt32());
        var skill = root.GetProperty("skills")[0];
        Assert.Equal("Metadata/Items/Gems/SkillGemEarthquake", skill.GetProperty("id").GetString());
        Assert.Equal(1, skill.GetProperty("level_interval")[0].GetInt32());
        Assert.Equal(100, skill.GetProperty("level_interval")[1].GetInt32());
        Assert.Equal("Metadata/Items/Gems/SupportGemFastForward", skill.GetProperty("support_skills")[0].GetProperty("id").GetString());
        Assert.Equal(1, skill.GetProperty("support_skills")[0].GetProperty("level_interval")[0].GetInt32());
        Assert.Equal("Metadata/Items/Gems/SupportGemAftershock", skill.GetProperty("support_skills")[1].GetProperty("id").GetString());
        var inventorySlots = root.GetProperty("inventory_slots");
        Assert.Equal("BodyArmour1", inventorySlots[0].GetProperty("inventory_id").GetString());
        Assert.Contains("Expert Hexer's Robe", inventorySlots[0].GetProperty("additional_text").GetString());
        Assert.Contains("1. +50 to maximum Life", inventorySlots[0].GetProperty("additional_text").GetString());
        Assert.Equal(1, inventorySlots[0].GetProperty("level_interval")[0].GetInt32());
        Assert.False(inventorySlots[0].TryGetProperty("slot_x", out _));
        Assert.False(inventorySlots[0].TryGetProperty("slot_y", out _));
        Assert.Equal("Ring1", inventorySlots[1].GetProperty("inventory_id").GetString());
        Assert.Equal("Kalandra's Touch", inventorySlots[1].GetProperty("unique_name").GetString());
        Assert.Equal([99], result.SkippedNodeIds);
    }

    [Fact]
    public void ExportFilesUsesPassiveTreeVariantsAsOutputSource()
    {
        var tree = CreateTree();
        var build = new ImportedBuild(
            ClassId: 2,
            AscendClassId: 1,
            SecondaryAscendClassId: 0,
            NodeHashes: [10],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "0.5.0",
            Source: "test")
        {
            PassiveTreeVariants =
            [
                PassiveVariant(0, "Bossing", [10]),
                PassiveVariant(1, "Mapping", [11]),
            ],
            ItemSetVariants =
            [
                new ImportedItemSetVariant(
                    0,
                    1,
                    "Bossing",
                    [new ImportedItem("Ring 1", "Unique", "Kalandra's Touch", "Ring", "Rarity: Unique\nKalandra's Touch\nRing")]),
                new ImportedItemSetVariant(
                    1,
                    2,
                    "Unused Gear",
                    [new ImportedItem("Ring 1", "Unique", "Ignored Ring", "Ring", "Rarity: Unique\nIgnored Ring\nRing")]),
            ],
            Skills = new ImportedSkills(
                [
                    SkillSet(0, 1, "Bossing", "Earthquake", "Metadata/Items/Gems/SkillGemEarthquake"),
                    SkillSet(1, 2, "Mapping", "Spark", "Metadata/Items/Gems/SkillGemSpark"),
                    SkillSet(2, 3, "Unused Gems", "Frostbolt", "Metadata/Items/Gems/SkillGemFrostbolt"),
                ],
                0,
                0),
        };

        var files = Poe2BuildPlannerExporter.ExportFiles(build, tree, tree.Classes, "League Starter");

        Assert.Equal(["League Starter - Bossing", "League Starter - Mapping"], files.Select(file => file.Name));

        using var bossingDocument = JsonDocument.Parse(files[0].Export.Json);
        var bossingRoot = bossingDocument.RootElement;
        Assert.Equal("League Starter - Bossing", bossingRoot.GetProperty("name").GetString());
        Assert.Equal("melee17", bossingRoot.GetProperty("passives")[0].GetProperty("id").GetString());
        Assert.Equal("Kalandra's Touch", bossingRoot.GetProperty("inventory_slots")[0].GetProperty("unique_name").GetString());
        Assert.Equal("Metadata/Items/Gems/SkillGemEarthquake", bossingRoot.GetProperty("skills")[0].GetProperty("id").GetString());

        using var mappingDocument = JsonDocument.Parse(files[1].Export.Json);
        var mappingRoot = mappingDocument.RootElement;
        Assert.Equal("League Starter - Mapping", mappingRoot.GetProperty("name").GetString());
        Assert.Equal("strength89", mappingRoot.GetProperty("passives")[0].GetProperty("id").GetString());
        Assert.False(mappingRoot.TryGetProperty("inventory_slots", out _));
        Assert.Equal("Metadata/Items/Gems/SkillGemSpark", mappingRoot.GetProperty("skills")[0].GetProperty("id").GetString());
    }

    private static TreeModel CreateTree()
    {
        var classes = new ClassCatalog
        {
            Classes =
            [
                new CharacterClassInfo(
                    2,
                    2,
                    "Warrior",
                    [
                        new AscendancyInfo(0, "None", string.Empty, null),
                        new AscendancyInfo(1, "Titan", "Titan", "Warrior1"),
                    ]),
            ],
        };

        return new TreeModel
        {
            GameId = GameId.PathOfExile2,
            Version = "0.5.0",
            Classes = classes,
            Nodes = new Dictionary<int, Node>
            {
                [10] = Node(10, "melee17"),
                [11] = Node(11, "strength89"),
                [12] = Node(12, "melee18"),
                [99] = Node(99, null),
            },
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = [],
            Bounds = new TreeBounds(0, 0, 1, 1),
            Groups = new Dictionary<int, GroupPosition>(),
            SkillsPerOrbit = [],
            OrbitRadii = [],
            OrbitAngles = [],
        };
    }

    private static Node Node(int id, string? buildPlannerId) => new()
    {
        Id = id,
        BuildPlannerId = buildPlannerId,
        Name = $"Node {id}",
        Type = NodeType.Normal,
        X = 0,
        Y = 0,
        GroupId = 0,
        Orbit = 0,
        OrbitIndex = 0,
    };

    private static ImportedPassiveTreeVariant PassiveVariant(int index, string name, IReadOnlyList<int> nodeIds) =>
        new(
            index,
            name,
            2,
            1,
            0,
            nodeIds,
            [],
            new Dictionary<int, int>(),
            "0.5.0",
            2,
            "Warrior",
            "Warrior1",
            new Dictionary<int, AttributeNodeOverride>(),
            []);

    private static ImportedSkillSet SkillSet(int index, int id, string name, string skillName, string gemId) =>
        new(
            index,
            id,
            name,
            [
                new ImportedSkillGroup(
                    0,
                    skillName,
                    null,
                    null,
                    true,
                    false,
                    1,
                    0,
                    0,
                    [Gem(skillName, gemId, 20, 20)])
            ]);

    private static ImportedGem Gem(string name, string gemId, int? level, int? quality) => new(
        name,
        gemId,
        null,
        null,
        level,
        quality,
        true,
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
}
