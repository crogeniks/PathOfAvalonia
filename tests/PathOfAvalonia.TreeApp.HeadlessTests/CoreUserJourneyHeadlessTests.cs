using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Controls;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeApp.Views;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public sealed class CoreUserJourneyHeadlessTests
{
    [AvaloniaFact]
    public void LandingOffersEverySupportedGameAndOpensSelectedWorkspace()
    {
        var shell = CreateShell();
        var window = Show(shell);
        try
        {
            var landing = Assert.IsType<LandingView>(Required<ContentControl>(window, "ShellHost").Content);
            var gameButtons = landing.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Tag is GameId)
                .ToArray();

            Assert.Equal(2, Required<ItemsControl>(landing, "GameChoices").ItemCount);
            Assert.Equal(
                [GameId.PathOfExile1, GameId.PathOfExile2],
                gameButtons.Select(button => Assert.IsType<GameId>(button.Tag)).ToArray());

            Click(window, Assert.Single(gameButtons, button => Equals(button.Tag, GameId.PathOfExile2)));

            var workspace = Assert.IsType<GameWorkspaceView>(Required<ContentControl>(window, "ShellHost").Content);
            Assert.Equal(GameId.PathOfExile2, shell.ActiveWorkspace!.State.Game.Id);
            Assert.Same(shell.ActiveWorkspace, workspace.DataContext);
            Assert.Single(workspace.GetVisualDescendants().OfType<PassiveTreeView>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirtyWorkspaceIsNeverDiscardedWithoutConfirmation()
    {
        var shell = CreateShell();
        var window = Show(shell);
        try
        {
            OpenGame(window, GameId.PathOfExile1);
            var workspace = Assert.IsType<GameWorkspaceView>(Required<ContentControl>(window, "ShellHost").Content);
            var activeWorkspace = shell.ActiveWorkspace!;
            activeWorkspace.State.Spec.Toggle(NormalNodeId);

            Click(window, Required<Button>(workspace, "ChangeGameButton"));

            Assert.True(Required<Border>(window, "GameChangeConfirmation").IsVisible);
            Assert.Same(activeWorkspace, shell.ActiveWorkspace);
            Assert.Contains(NormalNodeId, activeWorkspace.State.Spec.AllocatedNodes);

            Click(window, Required<Button>(window, "CancelGameChangeButton"));

            Assert.False(Required<Border>(window, "GameChangeConfirmation").IsVisible);
            Assert.Same(activeWorkspace, shell.ActiveWorkspace);
            Assert.IsType<GameWorkspaceView>(Required<ContentControl>(window, "ShellHost").Content);

            Click(window, Required<Button>(workspace, "ChangeGameButton"));
            Click(window, Required<Button>(window, "ConfirmGameChangeButton"));

            Assert.Null(shell.ActiveWorkspace);
            Assert.Equal(ShellPage.Landing, shell.CurrentPage);
            Assert.IsType<LandingView>(Required<ContentControl>(window, "ShellHost").Content);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void WorkspaceOnlyOffersActionsValidForTheSelectedGame()
    {
        var registry = new GameRegistry();

        var poe1View = new GameWorkspaceView { DataContext = CreateWorkspace(registry.Get(GameId.PathOfExile1)) };
        var poe1Window = Show(poe1View);
        try
        {
            Assert.True(Required<TextBox>(poe1View, "BuildNameInput").IsVisible);
            Assert.True(Required<Button>(poe1View, "SaveBuildButton").IsVisible);
            Assert.True(Required<Button>(poe1View, "SaveBuildAsButton").IsVisible);
            Assert.True(Required<ComboBox>(poe1View, "SavedBuildSelector").IsVisible);
            Assert.True(Required<ComboBox>(poe1View, "TreeVersionSelector").IsVisible);
            Assert.True(Required<ComboBox>(poe1View, "DiffTreeVersionSelector").IsVisible);
            Assert.False(Required<Button>(poe1View, "ImportBuildPlannerButton").IsVisible);
            Assert.False(Required<Button>(poe1View, "ExportBuildPlannerButton").IsVisible);
            Assert.True(Required<TabItem>(poe1View, "EquipmentTab").IsEnabled);
            Assert.True(Required<TabItem>(poe1View, "BuildOutputTab").IsEnabled);
        }
        finally
        {
            poe1Window.Close();
        }

        var poe2View = new GameWorkspaceView { DataContext = CreateWorkspace(registry.Get(GameId.PathOfExile2)) };
        var poe2Window = Show(poe2View);
        try
        {
            Assert.Equal(2, Required<ComboBox>(poe2View, "TreeVersionSelector").ItemCount);
            Assert.True(Required<ComboBox>(poe2View, "DiffTreeVersionSelector").IsVisible);
            Assert.True(Required<Button>(poe2View, "DiffLegendButton").IsVisible);
            Assert.True(Required<Button>(poe2View, "ImportBuildPlannerButton").IsVisible);
            Assert.True(Required<Button>(poe2View, "ExportBuildPlannerButton").IsVisible);
            Assert.False(Required<Button>(poe2View, "ExportBuildPlannerButton").IsEnabled);
            Assert.True(Required<TabItem>(poe2View, "EquipmentTab").IsEnabled);
            Assert.True(Required<TabItem>(poe2View, "BuildOutputTab").IsEnabled);
        }
        finally
        {
            poe2Window.Close();
        }
    }

    [AvaloniaFact]
    public void PassiveTreeToolbarKeepsControlsBoundBelowTheCanvas()
    {
        var workspace = CreateWorkspace(GameRegistry.CreatePoe1());
        var view = new GameWorkspaceView { DataContext = workspace };
        var window = Show(view);
        try
        {
            var classSelector = Required<ComboBox>(view, "ClassSelector");
            classSelector.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, workspace.State.Spec.SelectedClassIndex);
            Assert.Contains(SecondClassStartNodeId, workspace.State.Spec.AllocatedNodes);
            Assert.DoesNotContain(FirstClassStartNodeId, workspace.State.Spec.AllocatedNodes);

            var toolbar = Required<Border>(view, "PassiveTreeToolbar");
            var canvas = Required<Grid>(view, "TreeCanvas");
            Assert.True(toolbar.Bounds.Top >= canvas.Bounds.Bottom);
            Assert.Single(view.GetVisualDescendants().OfType<PassiveTreeView>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PassiveSearchHighlightsNodesByNameAndStats()
    {
        var workspace = CreateWorkspace(GameRegistry.CreatePoe1());
        var view = new GameWorkspaceView { DataContext = workspace };
        var window = Show(view);
        try
        {
            Required<TextBox>(view, "PassiveSearchInput").Text = "strength";
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(NormalNodeId, workspace.State.Tree.SearchResultNodeIds);
            Assert.Equal(1, workspace.State.Tree.SearchResultCount);

            Required<TextBox>(view, "PassiveSearchInput").Text = "connected passive";
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(NormalNodeId, workspace.State.Tree.SearchResultNodeIds);
            Assert.DoesNotContain(FirstClassStartNodeId, workspace.State.Tree.SearchResultNodeIds);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CalculationsTabShowsLiveBasicStatsAndEditsCharacterLevel()
    {
        var workspace = CreateWorkspace(GameRegistry.CreatePoe1());
        var view = new GameWorkspaceView { DataContext = workspace };
        var window = Show(view);
        try
        {
            var tabs = Required<TabControl>(view, "WorkspaceTabs");
            var tab = Required<TabItem>(view, "BuildOutputTab");
            Assert.Equal("Calculations", tab.Header);

            tabs.SelectedItem = tab;
            Dispatcher.UIThread.RunJobs();

            Assert.True(RequiredVisual<ItemsControl>(view, "CalculatedStatsList").ItemCount > 0);
            var level = RequiredVisual<NumericUpDown>(view, "CharacterLevelInput");
            Assert.True(level.Bounds.Width >= 96, "The level editor must leave room beside its spinner buttons.");
            level.Value = 10;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(10, workspace.State.Equipment.CharacterLevel);
            Assert.Equal(10, workspace.State.Equipment.CalculatedStats!.Values.Level);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PassiveTreeSidebarPreviewsStatsWhilePointerHoversANode()
    {
        var workspace = CreateWorkspace(GameRegistry.CreatePoe1());
        var view = new GameWorkspaceView { DataContext = workspace };
        var window = Show(view);
        try
        {
            var sidebar = Required<BasicStatsSidebarView>(view, "TreeStatsSidebar");
            Assert.True(sidebar.IsVisible);
            var calculatedStats = Assert.IsType<BasicCharacterStatsViewModel>(
                workspace.State.Equipment.TreeCalculatedStats);
            var statsList = RequiredVisual<ItemsControl>(view, "TreeCalculatedStatsList");
            var previewSlot = RequiredVisual<Grid>(view, "PassivePreviewSlot");
            var previewBox = RequiredVisual<Border>(view, "PassivePreviewBox");
            Assert.Equal(
                ["Attributes", "Pools", "Recovery", "Defences", "Avoidance", "Resistances", "Movement"],
                calculatedStats.StatGroups.Select(group => group.Name));
            Assert.Equal(calculatedStats.StatGroups.Count, statsList.ItemCount);
            Assert.Equal(26, previewSlot.Bounds.Height);
            Assert.False(previewBox.IsVisible);
            Assert.True(
                calculatedStats.Stats.Select(stat => stat.Tone).Distinct().Count() >= 10,
                "The sidebar should expose distinct semantic tones for its stat values.");
            Assert.True(
                RequiredVisual<NumericUpDown>(view, "TreeCharacterLevelInput").Bounds.Width >= 96,
                "The sidebar level editor must leave room beside its spinner buttons.");
            var baselineStrength = workspace.State.Equipment.CalculatedStats!.Values.Strength;
            var treeView = Assert.Single(view.GetVisualDescendants().OfType<PassiveTreeView>());
            _ = window.CaptureRenderedFrame();

            var renderedStatRows = statsList.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.DataContext is CalculatedStatMetricViewModel)
                .ToArray();
            Assert.Equal(calculatedStats.Stats.Count, renderedStatRows.Length);
            Assert.All(renderedStatRows, row => Assert.True(row.Margin.Bottom >= 2));
            var renderedValueColorCount = statsList.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.DataContext is CalculatedStatMetricViewModel stat && text.Text == stat.Value)
                .Select(text => text.Foreground?.ToString())
                .Distinct()
                .Count();
            Assert.True(renderedValueColorCount >= 10, "Semantic stat colors should reach the rendered values.");
            var statsPositionBeforePreview = statsList.TranslatePoint(new Point(), sidebar);
            Assert.NotNull(statsPositionBeforePreview);

            MoveToTreeNode(
                window,
                treeView,
                workspace.State.Spec.Tree.Nodes[NormalNodeId],
                workspace.State.Spec.Tree.Bounds);

            Assert.DoesNotContain(NormalNodeId, workspace.State.Spec.AllocatedNodes);
            Assert.NotNull(workspace.State.Equipment.PassivePreview);
            Assert.True(previewBox.IsVisible);
            var statsPositionWithPreview = statsList.TranslatePoint(new Point(), sidebar);
            Assert.NotNull(statsPositionWithPreview);
            Assert.Equal(statsPositionBeforePreview.Value.Y, statsPositionWithPreview.Value.Y, precision: 3);
            Assert.Equal(baselineStrength + 10, workspace.State.Equipment.TreeCalculatedStats!.Values.Strength);
            Assert.Contains(
                workspace.State.Equipment.PassivePreview!.Changes,
                change => change.Label == "Strength" && change.DeltaText == "(+10)");

            ClickTreeNode(
                window,
                treeView,
                workspace.State.Spec.Tree.Nodes[NormalNodeId],
                workspace.State.Spec.Tree.Bounds);

            Assert.Contains(NormalNodeId, workspace.State.Spec.AllocatedNodes);
            Assert.Equal(2, workspace.State.Equipment.CharacterLevel);
            Assert.Equal(2, RequiredVisual<NumericUpDown>(view, "TreeCharacterLevelInput").Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ImportAndClearKeepTreeAndEquipmentInOneWorkspace()
    {
        var importedBuild = EmptyBuild() with
        {
            NodeHashes = [NormalNodeId],
            Items = [new ImportedItem("Ring 1", "Rare", "Vivid Loop", "Ruby Ring", "+75 to maximum Life")],
        };
        var game = GameRegistry.CreatePoe1() with { ImportStrategy = new StaticImportStrategy(importedBuild) };
        var workspace = CreateWorkspace(game);
        var view = new GameWorkspaceView { DataContext = workspace };
        var window = Show(view);
        try
        {
            OpenImportFlyout(view);
            Required<TextBox>(view, "ImportInput").Text = "test-build";
            Dispatcher.UIThread.RunJobs();
            workspace.ImportExport.ImportCommand.Execute(null);

            Assert.Contains(NormalNodeId, workspace.State.Spec.AllocatedNodes);
            Assert.Equal("Vivid Loop", Assert.Single(Assert.Single(workspace.State.Equipment.Groups).Items).Name);
            Assert.Contains("1 nodes applied", Required<TextBlock>(view, "ImportStatusText").Text);

            var tabs = Required<TabControl>(view, "WorkspaceTabs");
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            RequiredVisual<ListBox>(view, "SlotsList").SelectedItem =
                Assert.Single(workspace.State.Equipment.Slots, slot => slot.Name == "Ring 1");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, RequiredVisual<ListBox>(view, "LibraryList").ItemCount);

            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            workspace.ImportExport.ClearCommand.Execute(null);

            Assert.Equal([FirstClassStartNodeId], workspace.State.Spec.AllocatedNodes.Order());
            Assert.Empty(workspace.State.Equipment.Groups);
            Assert.Equal(string.Empty, Required<TextBox>(view, "ImportInput").Text);
            Assert.Equal("cleared", Required<TextBlock>(view, "ImportStatusText").Text);
        }
        finally
        {
            window.Close();
        }
    }

    private const int FirstClassStartNodeId = 1;
    private const int NormalNodeId = 2;
    private const int SecondClassStartNodeId = 3;

    private static void OpenImportFlyout(GameWorkspaceView view)
    {
        var button = Required<Button>(view, "ImportFlyoutButton");
        Assert.NotNull(button.Flyout);
        button.Flyout.ShowAt(button);
        Dispatcher.UIThread.RunJobs();
    }

    private static ShellViewModel CreateShell() => new(
        new GameRegistry(),
        new StubWorkspaceFactory(),
        new StubSettings());

    private static MainWindow Show(ShellViewModel shell)
    {
        var window = new MainWindow(shell, new StorageProviderAccessor());
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
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

    private static void OpenGame(MainWindow window, GameId gameId)
    {
        var landing = Assert.IsType<LandingView>(Required<ContentControl>(window, "ShellHost").Content);
        var button = Assert.Single(
            landing.GetVisualDescendants().OfType<Button>(),
            candidate => Equals(candidate.Tag, gameId));
        Click(window, button);
    }

    private static GameWorkspaceViewModel CreateWorkspace(
        GameDefinition game,
        Func<GameDefinition, string, Task>? switchTreeVersion = null,
        IRelayCommand? backToLandingCommand = null)
    {
        var tree = CreateTree(game.Id, game.DefaultTreeVersion);
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags);
        var equipment = new EquipmentViewModel(spec);
        var state = new BuildWorkspaceState(
            game,
            spec,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            new PassiveTreeViewModel(spec),
            equipment);
        return new GameWorkspaceViewModel(
            state,
            new TreeSelectionViewModel(state),
            new BuildImportExportViewModel(state, game.ImportStrategy, new NullBuildPlannerFileService()),
            new NullImageAssetResolver(),
            new StubAssetService(),
            switchTreeVersion ?? ((_, _) => Task.CompletedTask),
            backToLandingCommand ?? new RelayCommand(() => { }));
    }

    internal static TreeModel CreateTree(GameId gameId, string version)
    {
        var firstStart = Node(FirstClassStartNodeId, "Scion Start", NodeType.ClassStart, -350, -150, classStartIndex: 0);
        var normal = Node(
            NormalNodeId,
            "Connected Passive",
            NodeType.Normal,
            250,
            -150,
            stats: ["+10 to Strength"]);
        var secondStart = Node(SecondClassStartNodeId, "Marauder Start", NodeType.ClassStart, -350, 250, classStartIndex: 1);
        firstStart.LinkedNodes.Add(normal);
        normal.LinkedNodes.Add(firstStart);

        return new TreeModel
        {
            GameId = gameId,
            Version = version,
            Classes = new ClassCatalog
            {
                Classes =
                [
                    new CharacterClassInfo(0, 0, "Scion", [new AscendancyInfo(0, "None", string.Empty, null)]),
                    new CharacterClassInfo(1, 1, "Marauder", [new AscendancyInfo(0, "None", string.Empty, null)]),
                ],
            },
            Nodes = new Dictionary<int, Node>
            {
                [firstStart.Id] = firstStart,
                [normal.Id] = normal,
                [secondStart.Id] = secondStart,
            },
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = [new LineConnector(firstStart.Id, normal.Id, firstStart.X, firstStart.Y, normal.X, normal.Y)],
            Bounds = new TreeBounds(-500, -500, 500, 500),
            Groups = new Dictionary<int, GroupPosition> { [0] = new(0, 0) },
            SkillsPerOrbit = [1],
            OrbitRadii = [0],
            OrbitAngles = [[0]],
        };
    }

    private static Node Node(
        int id,
        string name,
        NodeType type,
        double x,
        double y,
        int? classStartIndex = null,
        IReadOnlyList<string>? stats = null) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        X = x,
        Y = y,
        ClassStartIndex = classStartIndex,
        GroupId = 0,
        Orbit = 0,
        OrbitIndex = 0,
        Stats = stats ?? [],
    };

    private static ImportedBuild EmptyBuild() => new(
        ClassId: 0,
        AscendClassId: 0,
        SecondaryAscendClassId: 0,
        NodeHashes: [],
        ClusterNodeHashes: [],
        MasterySelections: new Dictionary<int, int>(),
        TreeVersion: null,
        Source: "test");

    private static T Required<T>(Control root, string name) where T : Control =>
        root.FindControl<T>(name) ?? throw new Xunit.Sdk.XunitException($"Control '{name}' was not found.");

    private static T RequiredVisual<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().SingleOrDefault(control => control.Name == name)
        ?? throw new Xunit.Sdk.XunitException($"Visible control '{name}' was not found.");

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

    private static void MoveToTreeNode(Window window, Control treeView, Node node, TreeBounds bounds)
    {
        window.MouseMove(TreeNodeWindowPoint(window, treeView, node, bounds));
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickTreeNode(Window window, Control treeView, Node node, TreeBounds bounds)
    {
        var point = TreeNodeWindowPoint(window, treeView, node, bounds);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Point TreeNodeWindowPoint(Window window, Control treeView, Node node, TreeBounds bounds)
    {
        var scale = Math.Min(treeView.Bounds.Width / bounds.Width, treeView.Bounds.Height / bounds.Height) * 0.95;
        var point = new Point(
            node.X * scale + treeView.Bounds.Width * 0.5 - bounds.CenterX * scale,
            node.Y * scale + treeView.Bounds.Height * 0.5 - bounds.CenterY * scale);
        var windowPoint = treeView.TranslatePoint(point, window);
        Assert.NotNull(windowPoint);
        return windowPoint.Value;
    }

    private sealed class StubWorkspaceFactory : IGameWorkspaceFactory
    {
        public Task<GameWorkspaceViewModel> CreateAsync(
            GameDefinition game,
            string treeVersion,
            Func<GameDefinition, string, Task> switchTreeVersion,
            IRelayCommand backToLandingCommand) =>
            Task.FromResult(CreateWorkspace(
                game with { DefaultTreeVersion = treeVersion },
                switchTreeVersion,
                backToLandingCommand));
    }

    private sealed class StubSettings : IUserSettingsService
    {
        public GameId? LastGameId { get; set; }
        public Guid? LastBuildId { get; set; }
        public string? Poe2BuildPlannerDirectory { get; set; }
        public void Save() { }
    }

    private sealed class StaticImportStrategy(ImportedBuild build) : IImportStrategy
    {
        public bool IsSupported => true;
        public ImportedBuild Import(string text) => build;
        public Task<ImportedBuild> ImportAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(build);
    }

    private sealed class NullBuildPlannerFileService : IBuildPlannerFileService
    {
        public Task<BuildPlannerExportFileResult?> ExportAsync(
            BuildWorkspaceExportRequest request,
            CancellationToken cancellationToken) => Task.FromResult<BuildPlannerExportFileResult?>(null);

        public Task<BuildPlannerImportFileResult?> ImportAsync(
            BuildWorkspaceImportRequest request,
            CancellationToken cancellationToken) => Task.FromResult<BuildPlannerImportFileResult?>(null);
    }

    private sealed class NullImageAssetResolver : ITreeImageAssetResolver
    {
        public Bitmap? LoadBitmap(string relativePath) => null;
        public Bitmap? LoadBackground(string treeVersion) => null;
    }

    private sealed class StubAssetService : IGameAssetService
    {
        public Task<TreeModel> LoadTreeAsync(GameDefinition game, string? version = null) =>
            Task.FromResult(CreateTree(game.Id, version ?? game.DefaultTreeVersion));

        public Task<SpriteMap> LoadSpritesAsync(GameDefinition game, string? version = null) =>
            Task.FromResult(new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() });

        public Stream OpenAsset(GameDefinition game, string relativePath) => throw new NotSupportedException();
        public Bitmap? LoadBitmap(GameDefinition game, string relativePath, string? version = null) => null;
        public Bitmap? LoadSharedBitmap(string relativePath) => null;
    }
}
