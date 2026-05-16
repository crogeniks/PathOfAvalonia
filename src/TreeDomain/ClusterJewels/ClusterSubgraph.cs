namespace PathOfAvalonia.TreeDomain.ClusterJewels;

public sealed class ClusterSubgraph
{
    public required int SocketNodeId { get; init; }
    public required int EntranceNodeId { get; init; }
    public required int LineageIdBase { get; init; }
    public required ClusterJewelSize Size { get; init; }
    public required double CircleRadius { get; init; }
    // Center of the ring in tree-space. This is offset from the socket outward
    // (away from the tree center), so the socket sits at the ring edge, not the center.
    public required double ClusterCenterX { get; init; }
    public required double ClusterCenterY { get; init; }
    public required IReadOnlyList<Node> Nodes { get; init; }
    public required IReadOnlyDictionary<int, Node> NodesById { get; init; }
    public required IReadOnlyList<Connector> Connectors { get; init; }
}
