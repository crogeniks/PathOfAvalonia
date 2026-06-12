using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2BuildPlannerImporterTests
{
    [Fact]
    public void ImportsBuildPlannerJsonIntoImportedBuild()
    {
        var tree = CreateTree();
        var json = """
            {
              "name": "Planner Build",
              "ascendancy": "Warrior1",
              "passives": [
                { "id": "melee17" },
                { "id": "strength89", "weapon_set": 2 },
                { "id": "missing" }
              ],
              "skills": [
                {
                  "id": "Metadata/Items/Gems/SkillGemEarthquake",
                  "level_interval": [1, 100],
                  "support_skills": [
                    { "id": "Metadata/Items/Gems/SupportGemAftershock", "level_interval": [1, 100] }
                  ]
                }
              ],
              "inventory_slots": [
                {
                  "inventory_id": "BodyArmour1",
                  "additional_text": "Expert Hexer's Robe\n1. +50 to maximum Life",
                  "level_interval": [1, 100]
                },
                {
                  "inventory_id": "Ring1",
                  "unique_name": "Kalandra's Touch",
                  "level_interval": [1, 100]
                }
              ]
            }
            """;

        var result = Poe2BuildPlannerImporter.Import(json, tree);
        var build = result.Build;

        Assert.Equal("build-planner", build.Source);
        Assert.Equal("Warrior", build.ClassInternalId);
        Assert.Equal("Warrior1", build.AscendancyInternalId);
        Assert.Equal([10, 11], build.NodeHashes);
        Assert.Equal(PassiveAllocationSet.WeaponSet2, build.AllocationSets[11]);
        Assert.Equal(["missing"], result.SkippedPassiveIds);
        Assert.Equal("Planner Build", Assert.Single(build.PassiveTreeVariants).DisplayName);
        var group = Assert.Single(Assert.Single(build.Skills.SkillSets).Groups);
        Assert.Equal("Earthquake", group.Label);
        Assert.Equal("Metadata/Items/Gems/SkillGemEarthquake", group.Gems[0].GemId);
        Assert.Equal("Aftershock", group.Gems[1].NameSpec);
        Assert.Equal("Body Armour", build.Items[0].Slot);
        Assert.Contains("Expert Hexer's Robe", build.Items[0].RawText);
        Assert.Equal("Ring 1", build.Items[1].Slot);
        Assert.Equal("Unique", build.Items[1].Rarity);
        Assert.Equal("Kalandra's Touch", build.Items[1].Name);
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

    private static Node Node(int id, string buildPlannerId) => new()
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
}
