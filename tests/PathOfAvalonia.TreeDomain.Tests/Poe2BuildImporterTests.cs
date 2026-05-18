using System.IO.Compression;
using System.Text;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2BuildImporterTests
{
    [Fact]
    public void BuildCodeDecodesPathOfBuilding2Root()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree>
                <Spec treeVersion="0_4" classId="0" ascendClassId="0" classInternalId="Witch" ascendancyInternalId="Witch1" nodes="2,999999" masteryEffects="{2,42}">
                  <Sockets>
                    <Socket nodeId="3" itemId="7" />
                  </Sockets>
                  <Overrides>
                    <AttributeOverride nodeId="4" attribute="Strength" />
                  </Overrides>
                </Spec>
              </Tree>
              <Items activeItemSet="1">
                <Item id="7">Rarity: Rare
            Eagle Spark
            Ruby
            --------
            Sockets: S S
            Rune: Soul Core of Cholotl</Item>
                <ItemSet id="1">
                  <Slot name="Ring 1" itemId="7" />
                </ItemSet>
              </Items>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildCodeDecoder.Decode(EncodeBuildXml(xml));

        Assert.Equal("pob2-code", build.Source);
        Assert.Equal("0_4", build.TreeVersion);
        Assert.Equal("Witch", build.ClassInternalId);
        Assert.Equal("Witch1", build.AscendancyInternalId);
        Assert.Equal(new[] { 2 }, build.NodeHashes);
        Assert.Equal(new[] { 999999 }, build.ClusterNodeHashes);
        Assert.Equal(42, build.MasterySelections[2]);
        Assert.Equal(AttributeNodeOverride.Strength, build.AttributeOverrides[4]);
        Assert.Equal(7, Assert.Single(build.SocketedJewels).ItemId);
        Assert.Equal("Ruby", build.ItemsById[7].BaseType);
        Assert.Equal(2, build.ItemsById[7].Sockets.Count);
        Assert.Equal("Soul Core of Cholotl", Assert.Single(build.ItemsById[7].Runes));
    }

    [Fact]
    public void InternalIdsResolveClassAndAscendancyOnImport()
    {
        var tree = LoadTree();
        var node = tree.Nodes.Values.First(n => n.Type == NodeType.Normal);
        var build = new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: [node.Id],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "0_4",
            Source: "test")
        {
            ClassInternalId = "Witch",
            AscendancyInternalId = "Witch1",
        };
        var spec = new PassiveSpec(tree, tree.Classes, GameFeatureFlags.Poe2Milestone2);

        var result = spec.ApplyImport(build);

        Assert.Equal(5, spec.SelectedClassIndex);
        Assert.Equal(1, spec.SelectedAscendancyIndex);
        Assert.Equal(1, result.Applied);
        Assert.Contains(node.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void AttributeOverridesArePreservedWhenFeatureIsEnabled()
    {
        var tree = LoadTree();
        var attrNode = tree.Nodes.Values.First(n => n.Name == "Attribute");
        var build = new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: [attrNode.Id],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "0_4",
            Source: "test")
        {
            AttributeOverrides = new Dictionary<int, AttributeNodeOverride>
            {
                [attrNode.Id] = AttributeNodeOverride.Dexterity,
            },
        };
        var spec = new PassiveSpec(tree, tree.Classes, GameFeatureFlags.Poe2Milestone2);

        spec.ApplyImport(build);

        Assert.Equal(AttributeNodeOverride.Dexterity, spec.AttributeOverrides[attrNode.Id]);
    }

    [Fact]
    public void ParseXmlPreservesMultiplePassiveTreeVariants()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree activeSpec="2">
                <Spec title="Bossing" classId="1" ascendClassId="2" nodes="10,70000" masteryEffects="{10,42}" />
                <Spec classId="3" ascendClassId="1" nodes="20,80000" masteryEffects="{20,84}" />
              </Tree>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildXmlParser.Parse(xml);

        Assert.Equal(new[] { 20 }, build.NodeHashes);
        Assert.Equal(new[] { 80000 }, build.ClusterNodeHashes);
        Assert.Equal(2, build.PassiveTreeVariants.Count);
        Assert.Equal("Bossing", build.PassiveTreeVariants[0].DisplayName);
        Assert.Equal("Tree 2", build.PassiveTreeVariants[1].DisplayName);
        Assert.Equal(1, build.ActivePassiveTreeVariantIndex);
    }

    [Fact]
    public void ParseXmlPreservesWeaponSetAllocations()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree>
                <Spec nodes="10,11,12">
                  <WeaponSet1 nodes="11" />
                  <WeaponSet2 nodes="12" />
                </Spec>
              </Tree>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildXmlParser.Parse(xml);

        Assert.Equal(PassiveAllocationSet.WeaponSet1, build.AllocationSets[11]);
        Assert.Equal(PassiveAllocationSet.WeaponSet2, build.AllocationSets[12]);
    }

    [Fact]
    public void ParseXmlPreservesWeaponSetsPerPassiveTreeVariant()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree activeSpec="2">
                <Spec nodes="10,11">
                  <WeaponSet1 nodes="11" />
                </Spec>
                <Spec nodes="20,21">
                  <WeaponSet2 nodes="21" />
                </Spec>
              </Tree>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildXmlParser.Parse(xml);

        Assert.Equal(PassiveAllocationSet.WeaponSet2, build.AllocationSets[21]);
        Assert.DoesNotContain(11, build.AllocationSets.Keys);

        var switched = build.WithPassiveTreeVariant(0);

        Assert.Equal(PassiveAllocationSet.WeaponSet1, switched.AllocationSets[11]);
        Assert.DoesNotContain(21, switched.AllocationSets.Keys);
    }

    [Fact]
    public void ParseXmlIgnoresMalformedWeaponSetNodes()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree>
                <Spec nodes="10,11">
                  <WeaponSet1 nodes="10,abc,11" />
                </Spec>
              </Tree>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildXmlParser.Parse(xml);

        Assert.Equal(new[] { 10, 11 }, build.AllocationSets.Keys.OrderBy(id => id));
    }

    [Fact]
    public void WithPassiveTreeVariantRebuildsActiveBuild()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree activeSpec="2">
                <Spec title="Leveling" classId="1" ascendClassId="2" secondaryAscendClassId="3" treeVersion="0_4" classInternalId="Witch" ascendancyInternalId="Witch1" clusterHashFormatVersion="2" nodes="10,70000" masteryEffects="{10,42}">
                  <Sockets>
                    <Socket nodeId="100" itemId="7" />
                  </Sockets>
                  <Overrides>
                    <AttributeOverride nodeId="10" attribute="Dexterity" />
                  </Overrides>
                </Spec>
                <Spec title="Mapping" classId="3" ascendClassId="1" nodes="20" />
              </Tree>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildXmlParser.Parse(xml).WithPassiveTreeVariant(0);

        Assert.Equal(1, build.ClassId);
        Assert.Equal(2, build.AscendClassId);
        Assert.Equal(3, build.SecondaryAscendClassId);
        Assert.Equal(new[] { 10 }, build.NodeHashes);
        Assert.Equal(new[] { 70000 }, build.ClusterNodeHashes);
        Assert.Equal(42, build.MasterySelections[10]);
        Assert.Equal(AttributeNodeOverride.Dexterity, build.AttributeOverrides[10]);
        Assert.Equal(7, Assert.Single(build.SocketedJewels).ItemId);
        Assert.Equal("Witch", build.ClassInternalId);
        Assert.Equal("Witch1", build.AscendancyInternalId);
    }

    [Fact]
    public void ParseXmlPreservesMultipleItemSetVariants()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree>
                <Spec nodes="10" />
              </Tree>
              <Items activeItemSet="2">
                <Item id="1">Rarity: Rare
            First Ring
            Ruby Ring</Item>
                <Item id="2">Rarity: Rare
            Second Ring
            Sapphire Ring</Item>
                <ItemSet id="1" title="Boss Gear">
                  <Slot name="Ring 1" itemId="1" />
                </ItemSet>
                <ItemSet id="2" name="Mapping Gear">
                  <Slot name="Ring 1" itemId="2" />
                </ItemSet>
                <ItemSet>
                  <Slot name="Ring 1" itemId="1" />
                </ItemSet>
              </Items>
            </PathOfBuilding2>
            """;

        var build = Poe2BuildXmlParser.Parse(xml);

        Assert.Equal("Second Ring", Assert.Single(build.Items).Name);
        Assert.Equal(3, build.ItemSetVariants.Count);
        Assert.Equal("Boss Gear", build.ItemSetVariants[0].DisplayName);
        Assert.Equal("Mapping Gear", build.ItemSetVariants[1].DisplayName);
        Assert.Equal("Item Set 3", build.ItemSetVariants[2].DisplayName);
        Assert.Equal(1, build.ActiveItemSetVariantIndex);
    }

    [Fact]
    public void WithItemSetVariantRebuildsActiveItemsOnly()
    {
        var xml = """
            <PathOfBuilding2>
              <Tree>
                <Spec nodes="10">
                  <Sockets>
                    <Socket nodeId="100" itemId="1" />
                  </Sockets>
                </Spec>
              </Tree>
              <Items activeItemSet="2">
                <Item id="1">Rarity: Rare
            First Ring
            Ruby Ring</Item>
                <Item id="2">Rarity: Rare
            Second Ring
            Sapphire Ring</Item>
                <ItemSet id="1">
                  <Slot name="Ring 1" itemId="1" />
                </ItemSet>
                <ItemSet id="2">
                  <Slot name="Ring 1" itemId="2" />
                </ItemSet>
              </Items>
            </PathOfBuilding2>
            """;
        var imported = Poe2BuildXmlParser.Parse(xml);

        var switched = imported.WithItemSetVariant(0);

        Assert.Equal("First Ring", Assert.Single(switched.Items).Name);
        Assert.Equal(imported.NodeHashes, switched.NodeHashes);
        Assert.Equal(imported.SocketedJewels, switched.SocketedJewels);
    }

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE2", "tree_0_4.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadPoe2FromJson(stream, "0.4");
    }

    private static string EncodeBuildXml(string xml)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            zlib.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
