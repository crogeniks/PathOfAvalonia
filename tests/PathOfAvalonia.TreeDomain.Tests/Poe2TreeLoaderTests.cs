using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2TreeLoaderTests
{
    [Fact]
    public void LoadsClassesAndClassStartsFromTreeJson()
    {
        var tree = LoadTree();

        Assert.Equal(GameId.PathOfExile2, tree.GameId);
        Assert.Contains("Ranger", tree.Classes.ClassNames);
        Assert.Contains("Deadeye", tree.Classes.AscendancyNames(0));
        Assert.Contains(tree.Nodes.Values, n => n.Type == NodeType.ClassStart && n.ClassStartIndex == 0);
        Assert.Contains(tree.Nodes.Values, n =>
            n.Type == NodeType.ClassStart
            && n.ClassStartIndexes.Contains(5)
            && n.ClassStartIndexes.Contains(6));
    }

    [Fact]
    public void UsesOrbitAnglesAndWiresConnections()
    {
        var tree = LoadTree();

        Assert.True(tree.OrbitAngles.Count > 0);
        Assert.Contains(tree.Nodes.Values, n => n.LinkedNodes.Count > 0);
        Assert.All(tree.Connectors, c =>
        {
            Assert.True(tree.Nodes.ContainsKey(c.FromId));
            Assert.True(tree.Nodes.ContainsKey(c.ToId));
        });
    }

    [Fact]
    public void FiltersCrossAscendancyConnectors()
    {
        var tree = LoadTree();

        Assert.DoesNotContain(tree.Connectors, c =>
            tree.Nodes[c.FromId].AscendancyName != tree.Nodes[c.ToId].AscendancyName);
    }

    [Fact]
    public void RendersLocalClassStartConnectorsButNotExtremeChords()
    {
        var tree = LoadTree();

        Assert.Contains(tree.Connectors, c =>
            tree.Nodes[c.FromId].Type == NodeType.ClassStart
            || tree.Nodes[c.ToId].Type == NodeType.ClassStart);
        Assert.DoesNotContain(tree.Connectors, c =>
            (tree.Nodes[c.FromId].Type == NodeType.ClassStart && tree.Nodes[c.ToId].Type == NodeType.AscendancyStart)
            || (tree.Nodes[c.FromId].Type == NodeType.AscendancyStart && tree.Nodes[c.ToId].Type == NodeType.ClassStart));

        var maxReasonableDistance = tree.Bounds.Width * 0.25;
        Assert.DoesNotContain(tree.Connectors.OfType<LineConnector>(), c =>
        {
            var dx = c.X2 - c.X1;
            var dy = c.Y2 - c.Y1;
            return Math.Sqrt(dx * dx + dy * dy) > maxReasonableDistance;
        });
    }

    [Fact]
    public void DrawableLinkedNodesHaveConnectors()
    {
        var tree = LoadTree();
        var connectorPairs = tree.Connectors
            .Select(c => (FromId: Math.Min(c.FromId, c.ToId), ToId: Math.Max(c.FromId, c.ToId)))
            .ToHashSet();

        foreach (var node in tree.Nodes.Values)
        {
            foreach (var linked in node.LinkedNodes)
            {
                if (!NeedsDrawableConnector(node, linked))
                {
                    continue;
                }

                var pair = (FromId: Math.Min(node.Id, linked.Id), ToId: Math.Max(node.Id, linked.Id));
                Assert.Contains(pair, connectorPairs);
            }
        }
    }

    [Fact]
    public void WhispersOfDoomHasConnector()
    {
        var tree = LoadTree();
        var whispers = Assert.Single(tree.Nodes.Values.Where(n => n.Name == "Whispers of Doom"));

        var connector = Assert.Single(tree.Connectors.Where(c => c.FromId == whispers.Id || c.ToId == whispers.Id));

        Assert.Contains(whispers.LinkedNodes, n => n.Id == connector.FromId || n.Id == connector.ToId);
    }

    [Fact]
    public void PassiveSpecCanSelectSorceressDiscipleOfVarashta()
    {
        var tree = LoadTree();
        var spec = new PassiveSpec(tree, tree.Classes, GameFeatureFlags.Poe2Milestone1);

        spec.SetClass(6);
        spec.SetAscendancy(3);

        Assert.Equal(6, spec.SelectedClassIndex);
        Assert.Equal(3, spec.SelectedAscendancyIndex);
        var discipleStart = Assert.Single(tree.Nodes.Values.Where(n =>
            n.Type == NodeType.AscendancyStart
            && n.AscendancyName == "Disciple of Varashta"));
        Assert.Contains(discipleStart.Id, spec.AllocatedNodes);
    }

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE2", "tree_0_4.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadPoe2FromJson(stream, "0.4");
    }

    private static bool NeedsDrawableConnector(Node from, Node to)
    {
        return (from.GroupId == to.GroupId || from.AscendancyName is not null)
            && from.Type != NodeType.Mastery
            && to.Type != NodeType.Mastery
            && !((from.Type == NodeType.ClassStart && to.Type == NodeType.AscendancyStart)
                || (from.Type == NodeType.AscendancyStart && to.Type == NodeType.ClassStart));
    }
}
