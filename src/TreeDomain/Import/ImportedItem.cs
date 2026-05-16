namespace PathOfAvalonia.TreeDomain.Import;

public sealed record ImportedItem(
    string Slot,
    string Rarity,
    string Name,
    string BaseType,
    string RawText)
{
    public int Id { get; init; }
}

public sealed record ImportedSocketedJewel(int SocketNodeId, int ItemId);
