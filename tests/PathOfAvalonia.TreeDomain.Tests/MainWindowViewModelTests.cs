using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ImportUpdatesAscendancySelectionOnFirstImport()
    {
        var build = new ImportedBuild(
            ClassId: 1,
            AscendClassId: 2,
            SecondaryAscendClassId: 0,
            NodeHashes: Array.Empty<int>(),
            ClusterNodeHashes: Array.Empty<int>(),
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: null,
            Source: "test");
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };

        vm.ImportCommand.Execute(null);

        Assert.Equal(1, vm.SelectedClassIndex);
        Assert.Equal(new[] { "None", "Juggernaut", "Berserker", "Chieftain" }, vm.AscendancyNames);
        Assert.Equal(2, vm.SelectedAscendancyIndex);
        Assert.Equal("Berserker", vm.SelectedAscendancyName);
    }

    [Fact]
    public void ImportExposesPassiveTreeVariants()
    {
        var nodes = ImportableNodeIds();
        var build = BuildWithPassiveVariants(
            PassiveVariant(0, "Leveling", nodes[0]),
            PassiveVariant(1, "Endgame", nodes[1]));
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };

        vm.ImportCommand.Execute(null);

        Assert.True(vm.HasPassiveTreeVariants);
        Assert.Equal(new[] { "Leveling", "Endgame" }, vm.PassiveTreeVariantOptions.Select(option => option.DisplayName));
    }

    [Fact]
    public void SelectingPassiveTreeVariantReappliesSpec()
    {
        var nodes = ImportableNodeIds();
        var spec = LoadSpec();
        var build = BuildWithPassiveVariants(
            PassiveVariant(0, "Leveling", nodes[0]),
            PassiveVariant(1, "Endgame", nodes[1]));
        var vm = new MainWindowViewModel(spec, new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };
        vm.ImportCommand.Execute(null);

        vm.SelectedPassiveTreeVariantIndex = 1;

        Assert.Contains(nodes[1], spec.AllocatedNodes);
        Assert.DoesNotContain(nodes[0], spec.AllocatedNodes);
    }

    [Fact]
    public void ImportExposesItemSetVariants()
    {
        var build = BuildWithItemSetVariants(
            ItemSetVariant(0, 1, "Boss Gear", ImportedItem("First Ring")),
            ItemSetVariant(1, 2, "Mapping Gear", ImportedItem("Second Ring")));
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };

        vm.ImportCommand.Execute(null);

        Assert.True(vm.HasItemSetVariants);
        Assert.Equal(new[] { "Boss Gear", "Mapping Gear" }, vm.ItemSetVariantOptions.Select(option => option.DisplayName));
    }

    [Fact]
    public void SelectingItemSetVariantReloadsEquipment()
    {
        var equipment = new EquipmentViewModel();
        var build = BuildWithItemSetVariants(
            ItemSetVariant(0, 1, "Boss Gear", ImportedItem("First Ring")),
            ItemSetVariant(1, 2, "Mapping Gear", ImportedItem("Second Ring")));
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), equipment)
        {
            ImportInput = "test",
        };
        vm.ImportCommand.Execute(null);

        vm.SelectedItemSetVariantIndex = 1;

        Assert.Equal("Second Ring", Assert.Single(Assert.Single(equipment.Groups).Items).Name);
    }

    [Fact]
    public void ClearRemovesVariantSelectors()
    {
        var nodes = ImportableNodeIds();
        var build = BuildWithPassiveVariants(
            PassiveVariant(0, "Leveling", nodes[0]),
            PassiveVariant(1, "Endgame", nodes[1]));
        build = build with
        {
            ItemSetVariants =
            [
                ItemSetVariant(0, 1, "Boss Gear", ImportedItem("First Ring")),
                ItemSetVariant(1, 2, "Mapping Gear", ImportedItem("Second Ring")),
            ],
        };
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };
        vm.ImportCommand.Execute(null);

        vm.ClearCommand.Execute(null);

        Assert.False(vm.HasPassiveTreeVariants);
        Assert.False(vm.HasItemSetVariants);
        Assert.Empty(vm.PassiveTreeVariantOptions);
        Assert.Empty(vm.ItemSetVariantOptions);
    }

    [Fact]
    public void ImportStatusIncludesWeaponSetCountsWhenPresent()
    {
        var nodes = ImportableNodeIds();
        var build = new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: nodes,
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: null,
            Source: "test")
        {
            AllocationSets = new Dictionary<int, PassiveAllocationSet>
            {
                [nodes[0]] = PassiveAllocationSet.WeaponSet1,
                [nodes[1]] = PassiveAllocationSet.WeaponSet2,
            },
        };
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };

        vm.ImportCommand.Execute(null);

        Assert.Contains("weapon set 1: 1", vm.ImportStatus);
        Assert.Contains("weapon set 2: 1", vm.ImportStatus);
    }

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static int[] ImportableNodeIds()
    {
        var tree = LoadTree();
        return tree.Nodes.Values
            .Where(node => node.Type == NodeType.Normal)
            .Select(node => node.Id)
            .Distinct()
            .Take(2)
            .ToArray();
    }

    private static ImportedPassiveTreeVariant PassiveVariant(int index, string name, int nodeId) =>
        new(
            index,
            name,
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: [nodeId],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: null,
            ClusterHashFormatVersion: 2,
            ClassInternalId: null,
            AscendancyInternalId: null,
            AttributeOverrides: new Dictionary<int, AttributeNodeOverride>(),
            SocketedJewels: []);

    private static ImportedBuild BuildWithPassiveVariants(params ImportedPassiveTreeVariant[] variants)
    {
        var active = variants[0];
        return new ImportedBuild(
            active.ClassId,
            active.AscendClassId,
            active.SecondaryAscendClassId,
            active.NodeHashes,
            active.ClusterNodeHashes,
            active.MasterySelections,
            active.TreeVersion,
            "test")
        {
            ClusterHashFormatVersion = active.ClusterHashFormatVersion,
            AttributeOverrides = active.AttributeOverrides,
            SocketedJewels = active.SocketedJewels,
            PassiveTreeVariants = variants,
        };
    }

    private static ImportedItemSetVariant ItemSetVariant(int index, int id, string name, ImportedItem item) =>
        new(index, id, name, [item]);

    private static ImportedItem ImportedItem(string name) =>
        new("Ring 1", "Rare", name, "Ruby Ring", string.Empty);

    private static ImportedBuild BuildWithItemSetVariants(params ImportedItemSetVariant[] variants)
    {
        var itemsById = variants
            .SelectMany(variant => variant.Items)
            .Select((item, index) => item with { Id = index + 1 })
            .ToDictionary(item => item.Id);

        var activeItems = variants[0].Items
            .Select((item, index) => item with { Id = index + 1 })
            .ToArray();

        return new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: [],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: null,
            Source: "test")
        {
            Items = activeItems,
            ItemsById = itemsById,
            ItemSetVariants = variants,
        };
    }

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", "tree_3_28.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadFromJson(stream, "3.28");
    }

    private sealed class StubImportService(ImportedBuild build) : IImportService
    {
        public ImportedBuild Import(string text) => build;
    }
}
