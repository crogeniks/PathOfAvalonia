using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PathOfAvalonia.TreeApp;
using PathOfAvalonia.TreeApp.Controls;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeApp.Views;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Atlas;

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public sealed class AtlasTreeViewHeadlessTests
{
    [AvaloniaFact]
    public async Task Poe1WorkspacePlacesAtlasTabAfterCalculations()
    {
        var factory = App.Services.GetRequiredService<IGameWorkspaceFactory>();
        var workspace = await factory.CreateAsync(
            GameRegistry.CreatePoe1(),
            "3.29.0",
            (_, _) => Task.CompletedTask,
            new RelayCommand(() => { }));
        var view = new GameWorkspaceView { DataContext = workspace };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var tabs = view.GetVisualDescendants().OfType<TabControl>().Single(control => control.Name == "WorkspaceTabs");
            var tabItems = tabs.Items.OfType<TabItem>().ToArray();
            var calculationsIndex = Array.FindIndex(tabItems, item => item.Name == "BuildOutputTab");
            var atlasIndex = Array.FindIndex(tabItems, item => item.Name == "AtlasTreeTab");

            Assert.True(workspace.HasAtlasTree);
            Assert.Equal(calculationsIndex + 1, atlasIndex);
            Assert.True(tabItems[atlasIndex].IsVisible);

            tabs.SelectedItem = tabItems[atlasIndex];
            Dispatcher.UIThread.RunJobs();
            Assert.Single(view.GetVisualDescendants().OfType<AtlasTreeView>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AtlasCanvasRendersBundledTreeAndSearchHighlights()
    {
        var tree = LoadTree();
        var sprites = LoadSprites();
        var viewModel = new AtlasTreeViewModel(
            GameRegistry.CreatePoe1(),
            tree,
            sprites,
            new FileImageResolver(),
            new StubAssets(),
            new StubLayouts());
        viewModel.SearchText = "Ritual Altars";
        var view = new AtlasTreeView(viewModel, sprites, new FileImageResolver());
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.Equal(1200, frame.PixelSize.Width);
            Assert.Equal(800, frame.PixelSize.Height);
            Assert.NotEmpty(viewModel.SearchResultNodeIds);
        }
        finally
        {
            window.Close();
        }
    }

    private static AtlasTreeModel LoadTree()
    {
        using var stream = File.OpenRead(AtlasAsset("data.json"));
        return new Poe1AtlasTreeLoader().Load(stream, "3.29.0", GameId.PathOfExile1);
    }

    private static SpriteMap LoadSprites()
    {
        using var stream = File.OpenRead(AtlasAsset("data.json"));
        return SpriteMap.LoadPoe1FromGggTree(stream, "3_29_0/Atlas/assets");
    }

    private static string AtlasAsset(params string[] parts) =>
        Path.GetFullPath(Path.Combine([
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "assets", "PoE1", "3_29_0", "Atlas",
            .. parts,
        ]));

    private sealed class FileImageResolver : ITreeImageAssetResolver
    {
        public Bitmap? LoadBitmap(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "assets", "PoE1",
                relativePath));
            return File.Exists(path) ? new Bitmap(path) : null;
        }

        public Bitmap? LoadBackground(string treeVersion) =>
            new(AtlasAsset("assets", "background-3.png"));
    }

    private sealed class StubAssets : IGameAssetService
    {
        public Task<TreeModel> LoadTreeAsync(GameDefinition game, string? version = null) => throw new NotSupportedException();
        public Task<SpriteMap> LoadSpritesAsync(GameDefinition game, string? version = null) => throw new NotSupportedException();
        public Stream OpenAsset(GameDefinition game, string relativePath) => throw new NotSupportedException();
        public Bitmap? LoadBitmap(GameDefinition game, string relativePath, string? version = null) => null;
        public Bitmap? LoadSharedBitmap(string relativePath) => null;
    }

    private sealed class StubLayouts : IGameAssetLayoutRegistry
    {
        public IGameAssetLayout Get(GameId gameId) => throw new NotSupportedException();
    }
}
