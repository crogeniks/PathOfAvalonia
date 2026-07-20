using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Items;

public enum EquipmentSlotCategory
{
    Weapons,
    Armour,
    Jewellery,
    Flasks,
    Charms,
    Jewels,
}

public sealed record EquipmentSlotDefinition(
    string Name,
    string ShortName,
    EquipmentSlotCategory Category,
    int SortOrder);

/// <summary>
/// Canonical equipment slots and the small amount of slot-family logic needed by
/// the item workspace. Exact base-type rules remain an upstream data-porting task;
/// the item's imported/author-selected slot is the compatibility source of truth.
/// </summary>
public static class EquipmentSlotCatalog
{
    private static readonly EquipmentSlotDefinition[] CoreSlots =
    [
        new("Weapon 1", "W1", EquipmentSlotCategory.Weapons, 0),
        new("Weapon 2", "W2", EquipmentSlotCategory.Weapons, 1),
        new("Weapon 1 Swap", "W1", EquipmentSlotCategory.Weapons, 2),
        new("Weapon 2 Swap", "W2", EquipmentSlotCategory.Weapons, 3),
        new("Helmet", "HD", EquipmentSlotCategory.Armour, 10),
        new("Body Armour", "CH", EquipmentSlotCategory.Armour, 11),
        new("Gloves", "GL", EquipmentSlotCategory.Armour, 12),
        new("Boots", "BT", EquipmentSlotCategory.Armour, 13),
        new("Amulet", "AM", EquipmentSlotCategory.Jewellery, 20),
        new("Ring 1", "R1", EquipmentSlotCategory.Jewellery, 21),
        new("Ring 2", "R2", EquipmentSlotCategory.Jewellery, 22),
        new("Belt", "BL", EquipmentSlotCategory.Jewellery, 23),
        new("Flask 1", "F1", EquipmentSlotCategory.Flasks, 30),
        new("Flask 2", "F2", EquipmentSlotCategory.Flasks, 31),
        new("Flask 3", "F3", EquipmentSlotCategory.Flasks, 32),
        new("Flask 4", "F4", EquipmentSlotCategory.Flasks, 33),
        new("Flask 5", "F5", EquipmentSlotCategory.Flasks, 34),
        new("Charm 1", "C1", EquipmentSlotCategory.Charms, 40),
        new("Charm 2", "C2", EquipmentSlotCategory.Charms, 41),
        new("Charm 3", "C3", EquipmentSlotCategory.Charms, 42),
    ];

    private static readonly EquipmentSlotDefinition[] Poe2FlaskSlots =
    [
        new("Life Flask", "LF", EquipmentSlotCategory.Flasks, 30),
        new("Mana Flask", "MF", EquipmentSlotCategory.Flasks, 31),
    ];

    public static IReadOnlyList<EquipmentSlotDefinition> Core => CoreSlots;

    public static IReadOnlyList<EquipmentSlotDefinition> ForGame(GameId? gameId)
    {
        var slots = CoreSlots.Where(slot =>
            slot.Category != EquipmentSlotCategory.Flasks
            && (gameId == GameId.PathOfExile2 || slot.Category != EquipmentSlotCategory.Charms));
        var flasks = gameId == GameId.PathOfExile2
            ? Poe2FlaskSlots
            : CoreSlots.Where(slot => slot.Category == EquipmentSlotCategory.Flasks);
        return slots.Concat(flasks).OrderBy(slot => slot.SortOrder).ToArray();
    }

    public static string NormalizeForGame(string slotName, GameId? gameId) => (gameId, slotName) switch
    {
        (GameId.PathOfExile2, "Flask 1") => "Life Flask",
        (GameId.PathOfExile2, "Flask 2") => "Mana Flask",
        (GameId.PathOfExile1, "Life Flask") => "Flask 1",
        (GameId.PathOfExile1, "Mana Flask") => "Flask 2",
        _ => slotName,
    };

    public static bool IsAvailableForGame(string slotName, GameId? gameId) =>
        TryParseJewelSocket(slotName, out _)
        || ForGame(gameId).Any(slot => string.Equals(slot.Name, slotName, StringComparison.Ordinal));

    public static EquipmentSlotDefinition Jewel(int socketNodeId, int order) =>
        new($"Jewel {socketNodeId}", "JW", EquipmentSlotCategory.Jewels, 100 + order);

    public static bool TryParseJewelSocket(string? slotName, out int socketNodeId)
    {
        const string prefix = "Jewel ";
        if (slotName is not null
            && slotName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(slotName[prefix.Length..], out socketNodeId))
        {
            return true;
        }

        socketNodeId = 0;
        return false;
    }

    public static bool IsCompatible(ImportedItem item, string slotName) =>
        string.Equals(Family(item.Slot, item), Family(slotName, item: null), StringComparison.OrdinalIgnoreCase);

    public static string Family(string? slotName, ImportedItem? item)
    {
        if (TryParseJewelSocket(slotName, out _)
            || Contains(slotName, "Jewel")
            || Contains(item?.BaseType, "Jewel"))
        {
            return "Jewel";
        }
        if (Contains(slotName, "Life Flask"))
        {
            return "Life Flask";
        }
        if (Contains(slotName, "Mana Flask"))
        {
            return "Mana Flask";
        }
        if (Contains(slotName, "Flask"))
        {
            return "Flask";
        }
        if (Contains(item?.BaseType, "Life Flask"))
        {
            return "Life Flask";
        }
        if (Contains(item?.BaseType, "Mana Flask"))
        {
            return "Mana Flask";
        }
        if (Contains(item?.BaseType, "Flask"))
        {
            return "Flask";
        }
        if (Contains(slotName, "Charm") || Contains(item?.BaseType, "Charm"))
        {
            return "Charm";
        }
        if (Contains(slotName, "Weapon"))
        {
            return "Weapon";
        }
        if (Contains(slotName, "Ring") || Contains(item?.BaseType, "Ring"))
        {
            return "Ring";
        }

        foreach (var exact in new[] { "Helmet", "Body Armour", "Gloves", "Boots", "Amulet", "Belt" })
        {
            if (Contains(slotName, exact) || Contains(item?.BaseType, exact))
            {
                return exact;
            }
        }

        return slotName?.Trim() ?? string.Empty;
    }

    private static bool Contains(string? value, string candidate) =>
        value?.Contains(candidate, StringComparison.OrdinalIgnoreCase) == true;
}
