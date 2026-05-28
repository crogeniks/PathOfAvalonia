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
                    "Rarity: Rare\nDire Shell\nExpert Hexer's Robe")
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
        Assert.Equal("melee17", passives[0].GetString());
        Assert.Equal("strength89", passives[1].GetProperty("id").GetString());
        Assert.Contains("Strength +5", passives[1].GetProperty("additional_text").GetString());
        Assert.Equal("melee18", passives[2].GetProperty("id").GetString());
        Assert.Equal(2, passives[2].GetProperty("weapon_set").GetInt32());
        var skill = root.GetProperty("skills")[0];
        Assert.Equal("Metadata/Items/Gems/SkillGemEarthquake", skill.GetProperty("id").GetString());
        Assert.Equal("Level 20, Quality 20", skill.GetProperty("additional_text").GetString());
        Assert.Equal("Metadata/Items/Gems/SupportGemFastForward", skill.GetProperty("support_skills")[0].GetString());
        Assert.Equal("Metadata/Items/Gems/SupportGemAftershock", skill.GetProperty("support_skills")[1].GetString());
        var inventorySlots = root.GetProperty("inventory_slots");
        Assert.Equal("BodyArmour1", inventorySlots[0].GetProperty("inventory_id").GetString());
        Assert.Contains("Expert Hexer's Robe", inventorySlots[0].GetProperty("additional_text").GetString());
        Assert.Equal("Ring1", inventorySlots[1].GetProperty("inventory_id").GetString());
        Assert.Equal("Kalandra's Touch", inventorySlots[1].GetProperty("unique_name").GetString());
        Assert.Equal([99], result.SkippedNodeIds);
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
