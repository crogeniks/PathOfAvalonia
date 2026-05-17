using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record RadiusJewelEffect(
    int SocketNodeId,
    ImportedItem Item,
    RadiusJewelKind Kind,
    int RadiusIndex,
    int? AlternateCenterNodeId,
    TimelessConqueror? Conqueror,
    IReadOnlyList<NodeStatTransform> NodeTransforms,
    bool AllowsUnconnectedAllocation);
