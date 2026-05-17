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
