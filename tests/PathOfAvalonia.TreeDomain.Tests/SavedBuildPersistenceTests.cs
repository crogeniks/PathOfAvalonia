using CommunityToolkit.Mvvm.Input;
using Moq;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Atlas;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class SavedBuildPersistenceTests
{
    [Fact]
    public async Task BuildLibraryRoundTripsCharacterAtlasAndItems()
    {
        using var paths = new TemporaryUserPaths();
        var library = new BuildLibraryService(paths);
        var id = Guid.NewGuid();
        var item = new ImportedItem(
            "Ring 1",
            "Rare",
            "Vivid Loop",
            "Ruby Ring",
            "Rarity: Rare\nVivid Loop\nRuby Ring\n--------\n+75 to maximum Life")
        {
            Id = 7,
            Sockets = [new ImportedItemSocket("R", "red")],
        };
        var character = EmptyBuild() with
        {
            NodeHashes = [1, 2],
            Items = [item],
            ItemsById = new Dictionary<int, ImportedItem> { [item.Id] = item },
            SocketedJewels = [new ImportedSocketedJewel(2, item.Id)],
            AllocationSets = new Dictionary<int, PassiveAllocationSet>
            {
                [2] = PassiveAllocationSet.WeaponSet1,
            },
        };

        var saved = await library.SaveAsync(new SavedBuild(
            id,
            "  Spark Atlas  ",
            GameId.PathOfExile1,
            "3.29.0",
            character,
            "3.29.0",
            [100, 101],
            DateTimeOffset.MinValue));
        var loaded = await library.LoadAsync(id);
        var summaries = await library.ListAsync(GameId.PathOfExile1);

        Assert.NotNull(loaded);
        Assert.Equal("Spark Atlas", saved.Name);
        Assert.Equal(saved.Name, loaded.Name);
        Assert.Equal([100, 101], loaded.AtlasNodeIds);
        Assert.Equal(PassiveAllocationSet.WeaponSet1, loaded.CharacterBuild.AllocationSets[2]);
        var loadedItem = Assert.Single(loaded.CharacterBuild.Items);
        Assert.Equal("Rare", loadedItem.Text.Rarity);
        Assert.Contains(loadedItem.Text.BodyLines, line => line.Text.Contains("maximum Life", StringComparison.Ordinal));
        Assert.Equal(id, Assert.Single(summaries).Id);

        await library.DeleteAsync(id);
        Assert.Null(await library.LoadAsync(id));
    }

    [Fact]
    public async Task BuildLibraryListReadsMetadataWithoutMaterializingBuildPayload()
    {
        using var paths = new TemporaryUserPaths();
        var library = new BuildLibraryService(paths);
        var id = Guid.NewGuid();
        var buildsDirectory = Path.Combine(paths.ConfigRoot, "PathOfAvalonia", "builds");
        Directory.CreateDirectory(buildsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(buildsDirectory, $"{id:N}.json"),
            $$"""
            {
              "formatVersion": 1,
              "build": {
                "id": "{{id:D}}",
                "name": "Metadata only",
                "gameId": "PathOfExile1",
                "treeVersion": "3.29.0",
                "characterBuild": 42,
                "atlasNodeIds": [],
                "updatedAt": "2026-07-23T12:00:00+00:00"
              }
            }
            """);

        var summary = Assert.Single(await library.ListAsync(GameId.PathOfExile1));

        Assert.Equal(id, summary.Id);
        Assert.Equal("Metadata only", summary.Name);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => library.LoadAsync(id));
    }

    [Fact]
    public async Task WorkspaceSaveAndRestoreIncludesAtlasAllocations()
    {
        var library = new RecordingBuildLibrary();
        var settings = new StubSettings();
        var first = CreateWorkspace(library, settings);
        first.State.Spec.Toggle(2);
        first.Atlas!.ToggleNode(101);
        first.BuildName = "Storm Weaver";

        await first.SaveBuildCommand.ExecuteAsync(null);

        Assert.NotNull(library.Saved);
        var saved = library.Saved;
        Assert.Contains(2, saved.CharacterBuild.NodeHashes);
        Assert.Contains(101, saved.AtlasNodeIds);
        Assert.Equal(saved.Id, settings.LastBuildId);
        Assert.False(first.IsDirty);

        var restored = CreateWorkspace(library, settings);
        await restored.RestoreSavedBuildAsync(saved);

        Assert.Equal("Storm Weaver", restored.BuildName);
        Assert.Contains(2, restored.State.Spec.AllocatedNodes);
        Assert.Contains(101, restored.Atlas!.AllocatedNodes);
        Assert.False(restored.IsDirty);

        restored.State.Spec.Toggle(2);
        Assert.True(restored.IsDirty);
    }

    private static GameWorkspaceViewModel CreateWorkspace(
        IBuildLibraryService library,
        IUserSettingsService settings)
    {
        var game = GameRegistry.CreatePoe1();
        var tree = CharacterTree();
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags);
        var equipment = new EquipmentViewModel(spec);
        var state = new BuildWorkspaceState(
            game,
            spec,
            EmptySprites(),
            new PassiveTreeViewModel(spec),
            equipment);
        var atlas = new AtlasTreeViewModel(
            game,
            AtlasTree(),
            EmptySprites(),
            Mock.Of<ITreeImageAssetResolver>(),
            Mock.Of<IGameAssetService>(),
            Mock.Of<IGameAssetLayoutRegistry>());
        return new GameWorkspaceViewModel(
            state,
            new TreeSelectionViewModel(state),
            new BuildImportExportViewModel(state, game.ImportStrategy, Mock.Of<IBuildPlannerFileService>()),
            Mock.Of<ITreeImageAssetResolver>(),
            Mock.Of<IGameAssetService>(),
            (_, _) => Task.CompletedTask,
            new RelayCommand(() => { }),
            atlas,
            library,
            settings);
    }

    private static TreeModel CharacterTree()
    {
        var start = CharacterNode(1, "Start", NodeType.ClassStart, classStartIndex: 0);
        var passive = CharacterNode(2, "Passive", NodeType.Normal);
        start.LinkedNodes.Add(passive);
        passive.LinkedNodes.Add(start);
        return new TreeModel
        {
            GameId = GameId.PathOfExile1,
            Version = "3.29.0",
            Classes = new ClassCatalog
            {
                Classes = [new CharacterClassInfo(0, 0, "Scion", [new AscendancyInfo(0, "None", string.Empty, null)])],
            },
            Nodes = new Dictionary<int, Node> { [1] = start, [2] = passive },
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = [new LineConnector(1, 2, 0, 0, 1, 0)],
            Bounds = new TreeBounds(-1, -1, 2, 2),
            Groups = new Dictionary<int, GroupPosition> { [0] = new(0, 0) },
            SkillsPerOrbit = [2],
            OrbitRadii = [1],
            OrbitAngles = [[0, 1]],
        };
    }

    private static Node CharacterNode(int id, string name, NodeType type, int? classStartIndex = null) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        X = id - 1,
        Y = 0,
        GroupId = 0,
        Orbit = 0,
        OrbitIndex = id - 1,
        ClassStartIndex = classStartIndex,
    };

    private static AtlasTreeModel AtlasTree()
    {
        var start = AtlasNode(100, "Start", AtlasNodeType.Start);
        var passive = AtlasNode(101, "Maps", AtlasNodeType.Normal);
        start.LinkedNodes.Add(passive);
        passive.LinkedNodes.Add(start);
        return new AtlasTreeModel
        {
            GameId = GameId.PathOfExile1,
            Version = "3.29.0",
            StartNodeId = start.Id,
            PointLimit = 138,
            Nodes = new Dictionary<int, AtlasNode> { [start.Id] = start, [passive.Id] = passive },
            Connectors = [new LineConnector(start.Id, passive.Id, 0, 0, 1, 0)],
            Bounds = new TreeBounds(-1, -1, 2, 2),
            Groups = new Dictionary<int, GroupPosition> { [0] = new(0, 0) },
            SkillsPerOrbit = [2],
            OrbitRadii = [1],
            OrbitAngles = [[0, 1]],
            GroupVisuals = [],
        };
    }

    private static AtlasNode AtlasNode(int id, string name, AtlasNodeType type) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        X = id - 100,
        Y = 0,
        GroupId = 0,
        Orbit = 0,
        OrbitIndex = id - 100,
    };

    private static SpriteMap EmptySprites() => new()
    {
        Atlases = new Dictionary<string, SpriteAtlas>(),
    };

    private static ImportedBuild EmptyBuild() => new(
        0,
        0,
        0,
        [],
        [],
        new Dictionary<int, int>(),
        "3.29.0",
        "test");

    private sealed class RecordingBuildLibrary : IBuildLibraryService
    {
        public SavedBuild? Saved { get; private set; }

        public Task<IReadOnlyList<SavedBuildSummary>> ListAsync(GameId? gameId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SavedBuildSummary>>(Saved is { } saved && (gameId is null || gameId == saved.GameId)
                ? [new SavedBuildSummary(saved.Id, saved.Name, saved.GameId, saved.TreeVersion, saved.UpdatedAt)]
                : []);

        public Task<SavedBuild?> LoadAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved?.Id == id ? Saved : null);

        public Task<SavedBuild> SaveAsync(SavedBuild build, CancellationToken cancellationToken = default)
        {
            Saved = build with { UpdatedAt = DateTimeOffset.UtcNow };
            return Task.FromResult(Saved);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (Saved?.Id == id) Saved = null;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSettings : IUserSettingsService
    {
        public GameId? LastGameId { get; set; }
        public Guid? LastBuildId { get; set; }
        public string? Poe2BuildPlannerDirectory { get; set; }
        public void Save() { }
    }

    private sealed class TemporaryUserPaths : IUserPathService, IDisposable
    {
        public TemporaryUserPaths()
        {
            ConfigRoot = Path.Combine(Path.GetTempPath(), "PathOfAvalonia.Tests", Guid.NewGuid().ToString("N"));
        }

        public string ConfigRoot { get; }
        public string DefaultPoe2BuildPlannerDirectory => Path.Combine(ConfigRoot, "BuildPlanner");

        public void Dispose()
        {
            if (Directory.Exists(ConfigRoot)) Directory.Delete(ConfigRoot, recursive: true);
        }
    }
}
