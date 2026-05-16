namespace PathOfAvalonia.TreeDomain.ClusterJewels;

public sealed record ClusterJewelSpec(
    int SocketNodeId,
    ClusterJewelSize Size,
    int PassiveCount,
    IReadOnlyList<string> NotableNames);
