using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public sealed class EquipmentViewHeadlessTests
{
    [AvaloniaFact]
    public void EquipmentWorkspaceRendersAndNewItemButtonOpensEditor()
    {
        var tree = LoadPoe1Tree();
        var allocatedSocketId = tree.Nodes.Values.First(node => node.Type == NodeType.JewelSocket && node.Name != "Charm Socket").Id;
        var spec = new PassiveSpec(tree);
        spec.ApplyImport(EmptyBuild() with { NodeHashes = [allocatedSocketId] });
        var viewModel = new EquipmentViewModel(spec);
        var view = new EquipmentView { DataContext = viewModel };
        var window = Show(view);
        try
        {
            var slots = Required<ListBox>(view, "SlotsList");
            var loadouts = Required<ComboBox>(view, "LoadoutSelector");
            var editor = Required<Grid>(view, "ItemEditor");

            Assert.Equal(16, slots.ItemCount);
            Assert.DoesNotContain(viewModel.Slots, slot => slot.Name.StartsWith("Charm ", StringComparison.Ordinal));
            Assert.Equal(
                $"Jewel {allocatedSocketId}",
                Assert.Single(viewModel.Slots, slot => slot.Name.StartsWith("Jewel ", StringComparison.Ordinal)).Name);
            Assert.Equal(1, loadouts.ItemCount);
            Assert.False(editor.IsVisible);
            Assert.Equal(new PixelSize(1280, 800), window.CaptureRenderedFrame()!.PixelSize);

            Click(window, Required<Button>(view, "NewItemButton"));

            Assert.True(editor.IsVisible);
            Assert.Contains("Rarity: Rare", Required<TextBox>(view, "EditorTextBox").Text);
            Assert.Equal("Weapon 1", Required<ComboBox>(view, "EditorSlotSelector").SelectedItem);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemCanBeAuthoredAndEquippedThroughRenderedControls()
    {
        var viewModel = new EquipmentViewModel();
        var view = new EquipmentView { DataContext = viewModel };
        var window = Show(view);
        try
        {
            var slots = Required<ListBox>(view, "SlotsList");
            slots.SelectedItem = Assert.Single(viewModel.Slots, slot => slot.Name == "Ring 1");
            Dispatcher.UIThread.RunJobs();

            Click(window, Required<Button>(view, "NewItemButton"));
            var editorText = Required<TextBox>(view, "EditorTextBox");
            editorText.Text = "Rarity: Rare\nVivid Loop\nRuby Ring\n--------\n+75 to maximum Life";
            Dispatcher.UIThread.RunJobs();

            Click(window, Required<Button>(view, "SaveItemButton"));

            Assert.False(Required<Grid>(view, "ItemEditor").IsVisible);
            Assert.Equal(1, Required<ListBox>(view, "LibraryList").ItemCount);
            Assert.True(Required<ScrollViewer>(view, "ItemDetailPanel").IsVisible);
            Assert.Equal("Vivid Loop", viewModel.SelectedSlot!.Item!.Name);
            Assert.True(Required<Button>(view, "UnequipButton").IsEnabled);

            Click(window, Required<Button>(view, "DuplicateLoadoutButton"));

            Assert.Equal(2, Required<ComboBox>(view, "LoadoutSelector").ItemCount);
            Assert.Equal("Vivid Loop", viewModel.SelectedSlot.Item!.Name);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Poe2RendersOneLifeAndOneManaFlaskSlot()
    {
        var viewModel = new EquipmentViewModel(new PassiveSpec(LoadPoe2Tree()));
        var view = new EquipmentView { DataContext = viewModel };
        var window = Show(view);
        try
        {
            var slots = Required<ListBox>(view, "SlotsList");

            Assert.Equal(15, slots.ItemCount);
            Assert.Equal(
                ["Life Flask", "Mana Flask"],
                viewModel.Slots.Where(slot => slot.Category == "Flasks").Select(slot => slot.DisplayName));
            Assert.Equal(3, viewModel.Slots.Count(slot => slot.Category == "Charms"));
            Assert.DoesNotContain(viewModel.Slots, slot => slot.Name is "Flask 3" or "Flask 4" or "Flask 5");
            Assert.Equal(new PixelSize(1280, 800), window.CaptureRenderedFrame()!.PixelSize);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Show(Control content)
    {
        var window = new Window
        {
            Width = 1280,
            Height = 800,
            Content = content,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ImportedBuild EmptyBuild() => new(
        ClassId: 0,
        AscendClassId: 0,
        SecondaryAscendClassId: 0,
        NodeHashes: [],
        ClusterNodeHashes: [],
        MasterySelections: new Dictionary<int, int>(),
        TreeVersion: null,
        Source: "test");

    private static TreeModel LoadPoe1Tree()
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

    private static T Required<T>(Control root, string name) where T : Control =>
        root.FindControl<T>(name) ?? throw new Xunit.Sdk.XunitException($"Control '{name}' was not found.");

    private static void Click(Window window, Control control)
    {
        Dispatcher.UIThread.RunJobs();
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(center, window);
        Assert.NotNull(point);
        window.MouseMove(point.Value);
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
