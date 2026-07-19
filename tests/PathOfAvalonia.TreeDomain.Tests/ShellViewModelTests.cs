using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Moq;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class ShellViewModelTests
{
    [Fact]
    public void StartsOnLandingWithoutRememberedGame()
    {
        var vm = CreateViewModel(new StubSettings());

        Assert.Equal(ShellPage.Landing, vm.CurrentPage);
        Assert.Null(vm.ActiveWorkspace);
    }

    [Fact]
    public void OpensRememberedGame()
    {
        var vm = CreateViewModel(new StubSettings { LastGameId = GameId.PathOfExile2 });

        Assert.Equal(ShellPage.Workspace, vm.CurrentPage);
        Assert.Equal(GameId.PathOfExile2, vm.ActiveWorkspace!.Workspace.Game.Id);
        Assert.True(vm.ActiveWorkspace.TreePanel.IsImportSupported);
    }

    [Fact]
    public void Poe1ExposesItsTreeVersionSelectorBeforeMultipleTreesAreAvailable()
    {
        var vm = CreateViewModel(new StubSettings { LastGameId = GameId.PathOfExile1 });
        var workspace = vm.ActiveWorkspace!;

        Assert.True(workspace.HasTreeVersionOptions);
        Assert.Equal(["3.28.0"], workspace.TreeVersionOptions);
        Assert.False(workspace.HasDiffVersionOptions);
    }

    [Fact]
    public void DirtyWorkspaceRequestsConfirmation()
    {
        var vm = CreateViewModel(new StubSettings());

        vm.SelectGameCommand.Execute(GameId.PathOfExile1);
        vm.ActiveWorkspace!.Workspace.Spec.Toggle(2);
        vm.BackToLandingCommand.Execute(null);

        Assert.True(vm.IsConfirmingGameChange);
        Assert.Equal(ShellPage.Workspace, vm.CurrentPage);
    }

    private static ShellViewModel CreateViewModel(IUserSettingsService settings) =>
        new(
            new GameRegistry(),
            CreateWorkspaceFactory(),
            settings);

    private static IGameWorkspaceFactory CreateWorkspaceFactory() =>
        new GameWorkspaceFactory(
            new StubAssets(),
            new GameAssetLayoutRegistry([new Poe1GameAssetLayout(), new Poe2GameAssetLayout()]),
            Mock.Of<IBuildPlannerExportService>(),
            Mock.Of<IBuildPlannerImportService>(),
            Mock.Of<IStorageProviderAccessor>());

    private sealed class StubSettings : IUserSettingsService
    {
        public GameId? LastGameId { get; set; }
        public string? Poe2BuildPlannerDirectory { get; set; }
        public bool Saved { get; private set; }
        public void Save() => Saved = true;
    }

    private sealed class StubAssets : IGameAssetService
    {
        public Task<TreeModel> LoadTreeAsync(GameDefinition game, string? version = null)
        {
            version ??= game.DefaultTreeVersion;
            var start = new Node
            {
                Id = 1,
                Name = "Start",
                Type = NodeType.ClassStart,
                X = 0,
                Y = 0,
                ClassStartIndex = 0,
                GroupId = 0,
                Orbit = 0,
                OrbitIndex = 0,
            };
            var normal = new Node
            {
                Id = 2,
                Name = "Node",
                Type = NodeType.Normal,
                X = 1,
                Y = 0,
                GroupId = 0,
                Orbit = 0,
                OrbitIndex = 1,
            };
            start.LinkedNodes.Add(normal);
            normal.LinkedNodes.Add(start);
            return Task.FromResult(new TreeModel
            {
                GameId = game.Id,
                Version = version,
                Classes = game.Id == GameId.PathOfExile1
                    ? ClassCatalog.CreatePoe1()
                    : new ClassCatalog
                    {
                        Classes =
                        [
                            new CharacterClassInfo(0, 2, "Ranger", [new AscendancyInfo(0, "None", string.Empty, null)])
                        ],
                    },
                Nodes = new Dictionary<int, Node> { [1] = start, [2] = normal },
                ClusterNodeTemplates = new Dictionary<string, Node>(),
                Connectors = [new LineConnector(1, 2, 0, 0, 1, 0)],
                Bounds = new TreeBounds(-1, -1, 2, 2),
                Groups = new Dictionary<int, GroupPosition> { [0] = new(0, 0) },
                SkillsPerOrbit = [2],
                OrbitRadii = [1],
                OrbitAngles = [new[] { 0.0, 1.0 }],
            });
        }

        public Task<SpriteMap> LoadSpritesAsync(GameDefinition game, string? version = null) =>
            Task.FromResult(new SpriteMap
            {
                Atlases = new Dictionary<string, SpriteAtlas>(),
            });

        public Stream OpenAsset(GameDefinition game, string relativePath) =>
            throw new NotSupportedException();

        public Bitmap? LoadBitmap(GameDefinition game, string relativePath, string? version = null) => null;

        public Bitmap? LoadSharedBitmap(string relativePath) => null;
    }
}
