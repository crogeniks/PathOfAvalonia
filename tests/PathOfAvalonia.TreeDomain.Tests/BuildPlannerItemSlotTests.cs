using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class BuildPlannerItemSlotTests
{
    [Fact]
    public void EachCanonicalSlotRoundTripsBetweenDisplayNameAndInventoryId()
    {
        foreach (var slot in BuildPlannerItemSlots.All)
        {
            Assert.True(BuildPlannerItemSlots.TryGetByDisplayName(slot.DisplayName, out var byName));
            Assert.Equal(slot, byName);
            Assert.True(BuildPlannerItemSlots.TryGetByInventoryId(slot.InventoryId, out var byInventoryId));
            Assert.Equal(slot, byInventoryId);
        }
    }

    [Fact]
    public void CanonicalSlotsHaveAscendingEquipmentOrder()
    {
        Assert.Equal(
            BuildPlannerItemSlots.All.Select(slot => slot.SortOrder).Order(),
            BuildPlannerItemSlots.All.Select(slot => BuildPlannerItemSlots.SortOrder(slot.DisplayName)));
    }

    [Fact]
    public void Poe2IncludesAllThreeCharmSlots()
    {
        Assert.Equal(
            ["Charm1", "Charm2", "Charm3"],
            BuildPlannerItemSlots.All
                .Where(slot => slot.DisplayName.StartsWith("Charm ", StringComparison.Ordinal))
                .Select(slot => slot.InventoryId));
    }

    [Fact]
    public void Poe2UsesOneLifeAndOneManaFlaskSlot()
    {
        Assert.Equal(
            [("Life Flask", "Flask1"), ("Mana Flask", "Flask2")],
            BuildPlannerItemSlots.All
                .Where(slot => slot.DisplayName.Contains("Flask", StringComparison.Ordinal))
                .Select(slot => (slot.DisplayName, slot.InventoryId)));
        Assert.False(BuildPlannerItemSlots.TryGetByInventoryId("Flask3", out _));
    }
}
