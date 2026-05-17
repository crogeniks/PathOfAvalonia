namespace PathOfAvalonia.TreeDomain.Import;

public sealed record ImportedItem(
    string Slot,
    string Rarity,
    string Name,
    string BaseType,
    string RawText)
{
    public int Id { get; init; }
    public IReadOnlyList<ImportedItemSocket> Sockets { get; init; } = [];
    public IReadOnlyList<string> Runes { get; init; } = [];
}

public sealed record ImportedSocketedJewel(int SocketNodeId, int ItemId);

public sealed record ImportedItemSocket(string Kind, string? Label);
