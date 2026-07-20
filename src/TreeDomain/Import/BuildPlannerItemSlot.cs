namespace PathOfAvalonia.TreeDomain.Import;

/// <summary>Canonical mapping between PoE2 equipment slot labels and Build Planner inventory ids.</summary>
public sealed record BuildPlannerItemSlot(string DisplayName, string InventoryId, int SortOrder);

public static class BuildPlannerItemSlots
{
    private static readonly BuildPlannerItemSlot[] AllSlots =
    [
        new("Weapon 1", "Weapon1", 0), new("Weapon 2", "Weapon2", 1),
        new("Weapon 1 Swap", "Weapon1Swap", 2), new("Weapon 2 Swap", "Weapon2Swap", 3),
        new("Helmet", "Helm1", 4), new("Body Armour", "BodyArmour1", 5),
        new("Gloves", "Gloves1", 6), new("Boots", "Boots1", 7), new("Amulet", "Amulet1", 8),
        new("Ring 1", "Ring1", 9), new("Ring 2", "Ring2", 10), new("Belt", "Belt1", 11),
        new("Life Flask", "Flask1", 12), new("Mana Flask", "Flask2", 13),
        new("Charm 1", "Charm1", 17), new("Charm 2", "Charm2", 18), new("Charm 3", "Charm3", 19),
    ];

    private static readonly IReadOnlyDictionary<string, BuildPlannerItemSlot> ByDisplayName =
        AllSlots.ToDictionary(slot => slot.DisplayName, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, BuildPlannerItemSlot> ByInventoryId =
        AllSlots.ToDictionary(slot => slot.InventoryId, StringComparer.Ordinal);

    public static IReadOnlyList<BuildPlannerItemSlot> All => AllSlots;

    public static bool TryGetByDisplayName(string? displayName, out BuildPlannerItemSlot slot)
    {
        displayName = displayName switch
        {
            "Flask 1" => "Life Flask",
            "Flask 2" => "Mana Flask",
            _ => displayName,
        };
        if (displayName is not null && ByDisplayName.TryGetValue(displayName, out var found))
        {
            slot = found;
            return true;
        }

        slot = null!;
        return false;
    }

    public static bool TryGetByInventoryId(string? inventoryId, out BuildPlannerItemSlot slot)
    {
        if (inventoryId is not null && ByInventoryId.TryGetValue(inventoryId, out var found))
        {
            slot = found;
            return true;
        }

        slot = null!;
        return false;
    }

    public static int SortOrder(string? displayName)
    {
        if (TryGetByDisplayName(displayName, out var slot))
        {
            return slot.SortOrder;
        }

        return displayName switch
        {
            "Flask 3" => 14,
            "Flask 4" => 15,
            "Flask 5" => 16,
            _ => int.MaxValue,
        };
    }
}
