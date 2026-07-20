using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Items;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class EquipmentWorkspaceTests
{
    [Fact]
    public void ImportedItemsAreSharedWhileLoadoutsSwitchSlotAssignments()
    {
        var first = Item(1, string.Empty, "First Ring", "Ruby Ring");
        var second = Item(2, string.Empty, "Second Ring", "Sapphire Ring");
        var build = EmptyBuild() with
        {
            Items = [first with { Slot = "Ring 1" }],
            ItemsById = new Dictionary<int, ImportedItem> { [1] = first, [2] = second },
            ItemSetVariants =
            [
                new ImportedItemSetVariant(0, 1, "Bossing", [first with { Slot = "Ring 1" }]),
                new ImportedItemSetVariant(1, 2, "Mapping", [second with { Slot = "Ring 1" }]),
            ],
        };
        var workspace = new EquipmentWorkspace();

        workspace.Load(build);

        Assert.Equal(2, workspace.Items.Count);
        Assert.Equal("Ring 1", workspace.Items[1].Slot);
        Assert.Equal("First Ring", Assert.Single(workspace.ActiveGearItems()).Name);

        Assert.True(workspace.SetActiveLoadout(1));

        Assert.Equal("Second Ring", Assert.Single(workspace.ActiveGearItems()).Name);
        Assert.Equal(2, workspace.Items.Count);
    }

    [Fact]
    public void PassiveJewelsStayEquippedWhenGearLoadoutsChange()
    {
        var workspace = new EquipmentWorkspace();
        var flask = workspace.AddItem("Rarity: Magic\nQuicksilver Flask\n--------\n20% increased Duration", "Flask 1");
        var jewel = workspace.AddItem("Rarity: Rare\nMind Stone\nCobalt Jewel\n--------\n+10 to Intelligence", "Jewel 123");

        Assert.True(workspace.Equip("Flask 3", flask.Id));
        Assert.True(workspace.Equip("Jewel 123", jewel.Id));

        workspace.CreateLoadout("Bossing", copyActive: false);

        Assert.Null(workspace.EquippedItemId("Flask 3"));
        Assert.Equal(jewel.Id, workspace.EquippedItemId("Jewel 123"));

        var snapshot = workspace.ApplyTo(EmptyBuild());
        Assert.Empty(snapshot.Items);
        Assert.Equal(new ImportedSocketedJewel(123, jewel.Id), Assert.Single(snapshot.SocketedJewels));
        Assert.Equal(2, snapshot.ItemSetVariants.Count);
    }

    [Fact]
    public void CustomItemCanBeCreatedAndEquippedWithoutImportingABuild()
    {
        var viewModel = new EquipmentViewModel();
        viewModel.SelectedSlot = Assert.Single(viewModel.Slots.Where(slot => slot.Name == "Ring 1"));

        viewModel.NewItemCommand.Execute(null);
        viewModel.EditorRawText = "Rarity: Rare\nVivid Loop\nRuby Ring\n--------\n+75 to maximum Life";
        viewModel.SaveItemCommand.Execute(null);

        Assert.True(viewModel.HasItems);
        Assert.False(viewModel.IsEditorOpen);
        Assert.Equal("Vivid Loop", viewModel.SelectedSlot.Item!.Name);
        Assert.Equal("Vivid Loop", Assert.Single(Assert.Single(viewModel.Groups).Items).Name);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void EquippingCustomTreeJewelUpdatesPassiveSpec()
    {
        var tree = LoadTree();
        var spec = new PassiveSpec(tree);
        var allocatedSocketId = tree.Nodes.Values.First(node => node.Type == NodeType.JewelSocket && node.Name != "Charm Socket").Id;
        spec.ApplyImport(EmptyBuild() with { NodeHashes = [allocatedSocketId] });
        var viewModel = new EquipmentViewModel(spec);
        var socket = Assert.Single(viewModel.Slots, slot => slot.Name == $"Jewel {allocatedSocketId}");
        viewModel.SelectedSlot = socket;

        viewModel.NewItemCommand.Execute(null);
        viewModel.EditorRawText = "Rarity: Rare\nMind Stone\nCobalt Jewel\n--------\n+10 to Intelligence";
        viewModel.SaveItemCommand.Execute(null);

        Assert.True(spec.TryGetSocketedJewel(allocatedSocketId, out var equipped));
        Assert.Equal("Mind Stone", equipped.Name);
    }

    [Fact]
    public void Poe1HidesCharmsAndUnallocatedJewelSockets()
    {
        var tree = LoadTree();
        var sockets = tree.Nodes.Values
            .Where(node => node.Type == NodeType.JewelSocket && node.Name != "Charm Socket")
            .Take(2)
            .ToArray();
        var spec = new PassiveSpec(tree);
        spec.ApplyImport(EmptyBuild() with { NodeHashes = [sockets[0].Id] });

        var viewModel = new EquipmentViewModel(spec);

        Assert.DoesNotContain(viewModel.Slots, slot => slot.Name.StartsWith("Charm ", StringComparison.Ordinal));
        var jewel = Assert.Single(viewModel.Slots, slot => slot.Name.StartsWith("Jewel ", StringComparison.Ordinal));
        Assert.Equal($"Jewel {sockets[0].Id}", jewel.Name);
        Assert.DoesNotContain(viewModel.Slots, slot => slot.Name == $"Jewel {sockets[1].Id}");
    }

    [Fact]
    public void Poe2ShowsTwoDedicatedFlaskSlotsAndThreeCharmSlots()
    {
        var viewModel = new EquipmentViewModel(new PassiveSpec(LoadPoe2Tree()));

        Assert.Equal(
            ["Life Flask", "Mana Flask"],
            viewModel.Slots.Where(slot => slot.Category == "Flasks").Select(slot => slot.Name));
        Assert.Equal(
            ["Charm 1", "Charm 2", "Charm 3"],
            viewModel.Slots.Where(slot => slot.Name.StartsWith("Charm ", StringComparison.Ordinal)).Select(slot => slot.Name));
    }

    [Fact]
    public void Poe1KeepsFiveGenericFlaskSlots()
    {
        var viewModel = new EquipmentViewModel(new PassiveSpec(LoadTree()));

        Assert.Equal(
            ["Flask 1", "Flask 2", "Flask 3", "Flask 4", "Flask 5"],
            viewModel.Slots.Where(slot => slot.Category == "Flasks").Select(slot => slot.Name));
    }

    [Fact]
    public void Poe2FlaskSlotsOnlyAcceptTheirMatchingFlaskType()
    {
        var workspace = new EquipmentWorkspace(GameId.PathOfExile2);
        var life = workspace.AddItem(
            "Rarity: Magic\nSeething Life Flask\n--------\n50% increased Amount Recovered",
            "Life Flask");
        var mana = workspace.AddItem(
            "Rarity: Magic\nEnduring Mana Flask\n--------\n50% increased Amount Recovered",
            "Mana Flask");

        Assert.True(workspace.Equip("Life Flask", life.Id));
        Assert.True(workspace.Equip("Mana Flask", mana.Id));
        Assert.False(workspace.Equip("Life Flask", mana.Id));
        Assert.False(workspace.Equip("Mana Flask", life.Id));
        Assert.False(workspace.Equip("Flask 3", life.Id));
    }

    [Fact]
    public void Poe2NormalizesLegacyNumberedFlaskAssignments()
    {
        var life = Item(1, "Flask 1", "Seething Flask", "Life Flask");
        var mana = Item(2, "Flask 2", "Enduring Flask", "Mana Flask");
        var workspace = new EquipmentWorkspace(GameId.PathOfExile2);

        workspace.Load(EmptyBuild() with
        {
            Items = [life, mana],
            ItemsById = new Dictionary<int, ImportedItem> { [life.Id] = life, [mana.Id] = mana },
        });

        Assert.Equal(life.Id, workspace.EquippedItemId("Life Flask"));
        Assert.Equal(mana.Id, workspace.EquippedItemId("Mana Flask"));
        Assert.Equal(["Life Flask", "Mana Flask"], workspace.ActiveGearItems().Select(item => item.Slot));
    }

    [Fact]
    public void CharmCompatibilityIsRestrictedToPoe2()
    {
        const string raw = "Rarity: Rare\nProtective Charm\nCharm\n--------\n+10% to Fire Resistance";
        var poe1 = new EquipmentWorkspace(GameId.PathOfExile1);
        var poe1Charm = poe1.AddItem(raw, "Charm 1");
        var poe2 = new EquipmentWorkspace(GameId.PathOfExile2);
        var poe2Charm = poe2.AddItem(raw, "Charm 1");

        Assert.False(poe1.Equip("Charm 1", poe1Charm.Id));
        Assert.True(poe2.Equip("Charm 3", poe2Charm.Id));
    }

    private static ImportedItem Item(int id, string slot, string name, string baseType) =>
        RawItemParser.Parse(slot, $"Rarity: Rare\n{name}\n{baseType}\n--------\n+20 to maximum Life") with { Id = id };

    private static ImportedBuild EmptyBuild() => new(
        ClassId: 0,
        AscendClassId: 0,
        SecondaryAscendClassId: 0,
        NodeHashes: [],
        ClusterNodeHashes: [],
        MasterySelections: new Dictionary<int, int>(),
        TreeVersion: null,
        Source: "test");

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "assets", "PoE1", "3_28_0", "data.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadFromJson(stream, "3.28.0");
    }

    private static TreeModel LoadPoe2Tree()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "assets", "PoE2", "0_5_0", "data.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadPoe2FromJson(stream, "0.5.0");
    }
}
