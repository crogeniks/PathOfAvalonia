namespace PathOfAvalonia.TreeDomain.Jewels;

using System.Runtime.CompilerServices;

public sealed record RadiusMembership(
    int SourceNodeId,
    IReadOnlyDictionary<int, IReadOnlySet<int>> NodesByRadiusIndex)
{
    private static readonly ConditionalWeakTable<TreeModel, Lazy<RadiusMembershipCache>> TreeCaches = new();

    public bool Contains(int radiusIndex, int nodeId) =>
        NodesByRadiusIndex.TryGetValue(radiusIndex, out var nodes) && nodes.Contains(nodeId);

    public static IReadOnlyDictionary<int, RadiusMembership> BuildForSockets(TreeModel tree, JewelRadiusTable table)
    {
        var sources = tree.Nodes.Values
            .Where(n => n.Type == NodeType.JewelSocket && n.Name != "Charm Socket" && n.ExpansionSocket?.ParentSocketId is null);
        return Build(tree, table, sources, excludeSockets: false);
    }

    public static IReadOnlyDictionary<int, RadiusMembership> BuildForKeystones(TreeModel tree, JewelRadiusTable table)
    {
        var sources = tree.Nodes.Values.Where(n => n.Type == NodeType.Keystone);
        return Build(tree, table, sources, excludeSockets: true);
    }

    /// <summary>
    /// Returns the immutable radius memberships shared by every spec over the
    /// same loaded tree. Computing these tables is proportional to the product
    /// of radius sources and tree nodes, so rebuilding them per open build is
    /// unnecessarily expensive.
    /// </summary>
    public static RadiusMembershipCache ForTree(TreeModel tree) =>
        TreeCaches.GetValue(
            tree,
            static model => new Lazy<RadiusMembershipCache>(
                () =>
                {
                    var table = JewelRadiusTable.For(model.GameId, model.Version);
                    return new RadiusMembershipCache(
                        BuildForSockets(model, table),
                        BuildForKeystones(model, table));
                },
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static IReadOnlyDictionary<int, RadiusMembership> Build(
        TreeModel tree,
        JewelRadiusTable table,
        IEnumerable<Node> sources,
        bool excludeSockets)
    {
        var result = new Dictionary<int, RadiusMembership>();
        foreach (var source in sources)
        {
            var byRadius = table.Bands.ToDictionary<JewelRadiusBand, int, IReadOnlySet<int>>(
                band => band.Index,
                band => NodesInBand(tree, source, band, excludeSockets));
            result[source.Id] = new RadiusMembership(source.Id, byRadius);
        }
        return result;
    }

    private static IReadOnlySet<int> NodesInBand(TreeModel tree, Node source, JewelRadiusBand band, bool excludeSockets)
    {
        var innerSq = band.Inner * band.Inner;
        var outerSq = band.Outer * band.Outer;
        var nodes = new HashSet<int>();
        foreach (var node in tree.Nodes.Values)
        {
            if (node.Id == source.Id || !IsRadiusCandidate(node, excludeSockets))
            {
                continue;
            }
            var dx = node.X - source.X;
            var dy = node.Y - source.Y;
            var distSq = dx * dx + dy * dy;
            if (distSq <= outerSq && distSq >= innerSq)
            {
                nodes.Add(node.Id);
            }
        }
        return nodes;
    }

    private static bool IsRadiusCandidate(Node node, bool excludeSockets)
    {
        if (node.Type is NodeType.Proxy or NodeType.ClassStart or NodeType.AscendancyStart)
        {
            return false;
        }
        if (node.Type == NodeType.Mastery)
        {
            return false;
        }
        if (excludeSockets && node.Type == NodeType.JewelSocket)
        {
            return false;
        }
        if (node.ExpansionSocket?.ParentSocketId is not null)
        {
            return false;
        }
        return true;
    }
}

public sealed record RadiusMembershipCache(
    IReadOnlyDictionary<int, RadiusMembership> Sockets,
    IReadOnlyDictionary<int, RadiusMembership> Keystones);
