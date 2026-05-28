using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class TreeDiffTests
{
    [Fact]
    public void IgnoresLayoutAndIconOnlyChanges()
    {
        var baseline = TreeWith(Node(1, x: 10, y: 20, icon: "old.png"));
        var current = TreeWith(Node(1, x: 30, y: 40, icon: "new.png"));

        var diff = TreeDiff.Compare(current, baseline);

        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void DetectsStatChanges()
    {
        var baseline = TreeWith(Node(1, stats: ["10% increased Damage"]));
        var current = TreeWith(Node(1, stats: ["12% increased Damage"]));

        var diff = TreeDiff.Compare(current, baseline);

        var nodeDiff = Assert.Single(diff.CurrentNodeDiffs.Values);
        Assert.Equal(TreeNodeDiffKind.Changed, nodeDiff.Kind);
    }

    [Fact]
    public void DetectsAddedAndRemovedNodes()
    {
        var baseline = TreeWith("0.4.0", Node(1));
        var current = TreeWith("0.5.0", Node(2));

        var diff = TreeDiff.Compare(current, baseline);

        Assert.Equal("0.5.0", diff.CurrentVersion);
        Assert.Equal("0.4.0", diff.BaselineVersion);
        Assert.Same(baseline.Nodes, diff.BaselineNodes);
        var added = Assert.Single(diff.CurrentNodeDiffs.Values);
        Assert.Equal(TreeNodeDiffKind.Added, added.Kind);
        var removed = Assert.Single(diff.RemovedNodes);
        Assert.Equal(TreeNodeDiffKind.Removed, removed.Kind);
    }

    private static TreeModel TreeWith(params Node[] nodes) => TreeWith("test", nodes);

    private static TreeModel TreeWith(string version, params Node[] nodes) => new()
    {
        GameId = GameId.PathOfExile2,
        Version = version,
        Classes = new ClassCatalog
        {
            Classes =
            [
                new CharacterClassInfo(
                    0,
                    0,
                    "Test",
                    [new AscendancyInfo(0, "None", string.Empty, null)]),
            ],
        },
        Nodes = nodes.ToDictionary(node => node.Id),
        ClusterNodeTemplates = new Dictionary<string, Node>(),
        Connectors = Array.Empty<Connector>(),
        Bounds = new TreeBounds(0, 0, 100, 100),
        Groups = new Dictionary<int, GroupPosition>(),
        SkillsPerOrbit = Array.Empty<int>(),
        OrbitRadii = Array.Empty<double>(),
        OrbitAngles = Array.Empty<IReadOnlyList<double>>(),
    };

    private static Node Node(
        int id,
        string name = "Damage",
        NodeType type = NodeType.Normal,
        double x = 0,
        double y = 0,
        string? icon = null,
        IReadOnlyList<string>? stats = null) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        X = x,
        Y = y,
        Icon = icon,
        Stats = stats ?? ["10% increased Damage"],
        GroupId = 0,
        Orbit = 0,
        OrbitIndex = 0,
    };
}
