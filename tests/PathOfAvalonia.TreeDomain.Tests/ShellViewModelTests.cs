using System.IO;
using Avalonia.Media.Imaging;
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
        var vm = new ShellViewModel(new GameRegistry(), new StubAssets(), new StubSettings());

        Assert.Equal(ShellPage.Landing, vm.CurrentPage);
        Assert.Null(vm.ActiveWorkspace);
    }

    [Fact]
    public void OpensRememberedGame()
    {
        var vm = new ShellViewModel(new GameRegistry(), new StubAssets(), new StubSettings { LastGameId = GameId.PathOfExile2 });

        Assert.Equal(ShellPage.Workspace, vm.CurrentPage);
        Assert.Equal(GameId.PathOfExile2, vm.ActiveWorkspace!.Workspace.Game.Id);
        Assert.True(vm.ActiveWorkspace.TreePanel.IsImportSupported);
    }

    [Fact]
    public void DirtyWorkspaceRequestsConfirmation()
    {
        var vm = new ShellViewModel(new GameRegistry(), new StubAssets(), new StubSettings());

        vm.SelectGameCommand.Execute(GameId.PathOfExile1);
        vm.ActiveWorkspace!.Workspace.Spec.Toggle(2);
        vm.BackToLandingCommand.Execute(null);

        Assert.True(vm.IsConfirmingGameChange);
        Assert.Equal(ShellPage.Workspace, vm.CurrentPage);
    }

    [Fact]
    public void SavesBackendSettings()
    {
        var settings = new StubSettings();
        var vm = new ShellViewModel(new GameRegistry(), new StubAssets(), settings)
        {
            EnablePobBackend = false,
            Poe1PobPath = " /pob1 ",
            Poe2PobPath = " /pob2 ",
            LuaExecutablePath = " luajit ",
            PobBackendTimeoutSeconds = "180",
        };

        vm.SaveBackendSettingsCommand.Execute(null);

        Assert.False(settings.EnablePobBackend);
        Assert.Equal("/pob1", settings.Poe1PobPath);
        Assert.Equal("/pob2", settings.Poe2PobPath);
        Assert.Equal("luajit", settings.LuaExecutablePath);
        Assert.Equal(180, settings.PobBackendTimeoutSeconds);
        Assert.True(settings.Saved);
    }

    private sealed class StubSettings : IUserSettingsService
    {
        public GameId? LastGameId { get; set; }
        public string? Poe1PobPath { get; set; }
        public string? Poe2PobPath { get; set; }
        public string? LuaExecutablePath { get; set; }
        public bool EnablePobBackend { get; set; } = true;
        public int PobBackendTimeoutSeconds { get; set; } = 120;
        public bool Saved { get; private set; }
        public void Save() => Saved = true;
    }

    private sealed class StubAssets : IGameAssetService
    {
        public TreeModel LoadTree(GameDefinition game)
        {
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
            return new TreeModel
            {
                GameId = game.Id,
                Version = game.DefaultTreeVersion,
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
            };
        }

        public SpriteMap LoadSprites(GameDefinition game) => new()
        {
            Atlases = new Dictionary<string, SpriteAtlas>(),
        };

        public Stream OpenAsset(GameDefinition game, string relativePath) =>
            throw new NotSupportedException();
    }
}
