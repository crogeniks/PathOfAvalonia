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
}
