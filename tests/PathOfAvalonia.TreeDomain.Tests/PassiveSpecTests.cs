using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class PassiveSpecTests
{
    [Fact]
    public void SelectedAscendancyStartIsAnAllocationRoot()
    {
        var spec = LoadSpec();
        spec.SetClass(1);
        spec.SetAscendancy(1);

        var ascendancyStart = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.Type == NodeType.AscendancyStart &&
            node.AscendancyName == "Juggernaut"));
        var passives = ascendancyStart.LinkedNodes
            .Where(node => node.Type is NodeType.Normal or NodeType.Notable)
            .Take(2)
            .ToArray();

        Assert.Equal(2, passives.Length);
        Assert.Contains(ascendancyStart.Id, spec.AllocatedNodes);

        spec.AllocateMany(passives.Select(node => node.Id));
        spec.Toggle(passives[0].Id);

        Assert.DoesNotContain(passives[0].Id, spec.AllocatedNodes);
        Assert.Contains(passives[1].Id, spec.AllocatedNodes);
    }

    [Fact]
    public void ScionCanSelectScavengerAscendancy()
    {
        var spec = LoadSpec();
        spec.SetClass(0);
        spec.SetAscendancy(2);

        var scavengerStart = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.Type == NodeType.AscendancyStart &&
            node.Name == "Scavenger" &&
            node.AscendancyName == "Reliquarian"));

        Assert.Equal(new[] { "None", "Ascendant", "Scavenger" }, spec.Classes.AscendancyNames(0));
        Assert.Contains(scavengerStart.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void CannotAllocateUnselectedAscendancyNodes()
    {
        var spec = LoadSpec();
        spec.SetClass(1);
        spec.SetAscendancy(1);

        var berserkerNode = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.AscendancyName == "Berserker" &&
            node.Type == NodeType.Notable &&
            node.Name == "Aspect of Carnage"));

        spec.Toggle(berserkerNode.Id);
        spec.AllocateMany(new[] { berserkerNode.Id });

        Assert.DoesNotContain(berserkerNode.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void HoverPathRejectsUnselectedAscendancyNodes()
    {
        var spec = LoadSpec();
        spec.SetClass(1);
        spec.SetAscendancy(1);

        var berserkerNode = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.AscendancyName == "Berserker" &&
            node.Type == NodeType.Notable &&
            node.Name == "Aspect of Carnage"));

        var path = spec.HoverPathTo(berserkerNode.Id);

        Assert.True(path.IsEmpty);
    }

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", "tree_3_28.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadFromJson(stream, "3.28");
    }
}
