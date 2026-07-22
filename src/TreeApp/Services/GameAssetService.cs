using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Atlas;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetService
{
    Task<TreeModel> LoadTreeAsync(GameDefinition game, string? version = null);
    Task<SpriteMap> LoadSpritesAsync(GameDefinition game, string? version = null);
    Task<AtlasTreeModel> LoadAtlasTreeAsync(GameDefinition game, string? version = null) =>
        Task.FromException<AtlasTreeModel>(new NotSupportedException($"Atlas tree assets are not available for {game.DisplayName}."));
    Task<SpriteMap> LoadAtlasSpritesAsync(GameDefinition game, string? version = null) =>
        Task.FromException<SpriteMap>(new NotSupportedException($"Atlas tree assets are not available for {game.DisplayName}."));
    Task<TimelessJewelData?> LoadTimelessJewelDataAsync(GameDefinition game, string? version = null) =>
        Task.FromResult<TimelessJewelData?>(null);
    Stream OpenAsset(GameDefinition game, string relativePath);
    Bitmap? LoadBitmap(GameDefinition game, string relativePath, string? version = null);
    Bitmap? LoadSharedBitmap(string relativePath);
}

public interface ITreeImageAssetResolver
{
    Bitmap? LoadBitmap(string relativePath);
    Bitmap? LoadJewelRadiusBitmap(string relativePath) => LoadBitmap($"JewelRadius/{relativePath}");
    Bitmap? LoadBackground(string treeVersion);
}

public readonly record struct GameAssetKey(GameId GameId, string Version);

public sealed class GameAssetService(GameRegistry games, IGameAssetLayoutRegistry layouts) : IGameAssetService
{
    private readonly ConcurrentDictionary<GameAssetKey, Lazy<Task<TreeModel>>> _trees = new();
    private readonly ConcurrentDictionary<GameAssetKey, Lazy<Task<SpriteMap>>> _sprites = new();
    private readonly ConcurrentDictionary<GameAssetKey, Lazy<Task<AtlasTreeModel>>> _atlasTrees = new();
    private readonly ConcurrentDictionary<GameAssetKey, Lazy<Task<SpriteMap>>> _atlasSprites = new();
    private readonly ConcurrentDictionary<GameAssetKey, Lazy<Task<TimelessJewelData?>>> _timelessJewels = new();

    public Task<TreeModel> LoadTreeAsync(GameDefinition game, string? version = null)
    {
        var key = new GameAssetKey(game.Id, version ?? game.DefaultTreeVersion);
        return _trees.GetOrAdd(key, static (assetKey, state) =>
            new Lazy<Task<TreeModel>>(
                () => Task.Run(() => state.LoadTree(assetKey)),
                LazyThreadSafetyMode.ExecutionAndPublication), this).Value;
    }

    public Task<SpriteMap> LoadSpritesAsync(GameDefinition game, string? version = null)
    {
        var key = new GameAssetKey(game.Id, version ?? game.DefaultTreeVersion);
        return _sprites.GetOrAdd(key, static (assetKey, state) =>
            new Lazy<Task<SpriteMap>>(
                () => Task.Run(() => state.LoadSprites(assetKey)),
                LazyThreadSafetyMode.ExecutionAndPublication), this).Value;
    }

    public Task<AtlasTreeModel> LoadAtlasTreeAsync(GameDefinition game, string? version = null)
    {
        var key = new GameAssetKey(game.Id, version ?? game.DefaultTreeVersion);
        return _atlasTrees.GetOrAdd(key, static (assetKey, state) =>
            new Lazy<Task<AtlasTreeModel>>(
                () => Task.Run(() => state.LoadAtlasTree(assetKey)),
                LazyThreadSafetyMode.ExecutionAndPublication), this).Value;
    }

    public Task<SpriteMap> LoadAtlasSpritesAsync(GameDefinition game, string? version = null)
    {
        var key = new GameAssetKey(game.Id, version ?? game.DefaultTreeVersion);
        return _atlasSprites.GetOrAdd(key, static (assetKey, state) =>
            new Lazy<Task<SpriteMap>>(
                () => Task.Run(() => state.LoadAtlasSprites(assetKey)),
                LazyThreadSafetyMode.ExecutionAndPublication), this).Value;
    }

    public Task<TimelessJewelData?> LoadTimelessJewelDataAsync(GameDefinition game, string? version = null)
    {
        var key = new GameAssetKey(game.Id, version ?? game.DefaultTreeVersion);
        if (layouts.Get(game.Id).TimelessJewelDataPaths(key.Version) is null)
        {
            return Task.FromResult<TimelessJewelData?>(null);
        }
        return _timelessJewels.GetOrAdd(key, static (assetKey, state) =>
            new Lazy<Task<TimelessJewelData?>>(
                () => Task.Run(() => state.LoadTimelessJewelData(assetKey)),
                LazyThreadSafetyMode.ExecutionAndPublication), this).Value;
    }

    private TreeModel LoadTree(GameAssetKey key)
    {
        var game = GameDefinitionFor(key.GameId);
        using var stream = OpenAsset(game, layouts.Get(game.Id).TreeDataPath(key.Version));
        var tree = game.TreeLoader.Load(stream, key.Version, game.Id);
        // Radius memberships are immutable tree data. Build the shared cache on
        // this loader task rather than making the first UI-created spec pay the
        // source × radius × node cost.
        _ = RadiusMembership.ForTree(tree);
        return tree;
    }

    private AtlasTreeModel LoadAtlasTree(GameAssetKey key)
    {
        var game = GameDefinitionFor(key.GameId);
        var path = layouts.Get(game.Id).AtlasTreeDataPath(key.Version)
            ?? throw new NotSupportedException($"Atlas tree assets are not registered for {game.DisplayName}.");
        using var stream = OpenAsset(game, path);
        return new Poe1AtlasTreeLoader().Load(stream, key.Version, game.Id);
    }

    private SpriteMap LoadSprites(GameAssetKey key)
    {
        var game = GameDefinitionFor(key.GameId);
        var spritePaths = layouts.Get(game.Id).SpriteDataPaths(key.Version);
        if (spritePaths.Kind == SpriteDataKind.Poe2GggAssets)
        {
            using var skills = OpenAsset(game, spritePaths.Paths[0]);
            using var frames = OpenAsset(game, spritePaths.Paths[1]);
            using var jewels = OpenAsset(game, spritePaths.Paths[2]);
            using var masteryEffectDisabled = OpenAsset(game, spritePaths.Paths[3]);
            using var masteryEffectActive = OpenAsset(game, spritePaths.Paths[4]);
            return AddSupplementalSprites(game, key.Version,
                SpriteMap.LoadPoe2FromGggAssets(skills, frames, jewels, masteryEffectDisabled, masteryEffectActive));
        }

        if (spritePaths.Kind == SpriteDataKind.Poe1GggTree)
        {
            using var treeStream = OpenAsset(game, spritePaths.Paths[0]);
            return AddSupplementalSprites(game, key.Version,
                SpriteMap.LoadPoe1FromGggTree(treeStream, spritePaths.AssetPrefix!));
        }

        using var stream = OpenAsset(game, spritePaths.Paths[0]);
        return AddSupplementalSprites(game, key.Version, SpriteMap.LoadFromJson(stream));
    }

    private SpriteMap LoadAtlasSprites(GameAssetKey key)
    {
        var game = GameDefinitionFor(key.GameId);
        var spritePaths = layouts.Get(game.Id).AtlasSpriteDataPaths(key.Version)
            ?? throw new NotSupportedException($"Atlas sprites are not registered for {game.DisplayName}.");
        if (spritePaths.Kind != SpriteDataKind.Poe1GggTree)
        {
            throw new NotSupportedException($"Unsupported Atlas sprite format: {spritePaths.Kind}.");
        }

        using var treeStream = OpenAsset(game, spritePaths.Paths[0]);
        return SpriteMap.LoadPoe1FromGggTree(treeStream, spritePaths.AssetPrefix!);
    }

    private SpriteMap AddSupplementalSprites(GameDefinition game, string version, SpriteMap sprites)
    {
        foreach (var path in layouts.Get(game.Id).AdditionalSpriteDataPaths(version))
        {
            using var stream = OpenAsset(game, path);
            sprites = sprites.Merge(SpriteMap.LoadFromJson(stream));
        }
        return sprites;
    }

    private TimelessJewelData? LoadTimelessJewelData(GameAssetKey key)
    {
        var game = GameDefinitionFor(key.GameId);
        var paths = layouts.Get(game.Id).TimelessJewelDataPaths(key.Version)
            ?? throw new InvalidOperationException($"No timeless jewel assets are registered for {game.DisplayName} {key.Version}.");
        using var definitions = OpenAsset(game, paths.Definitions);
        using var mapping = OpenAsset(game, paths.Mapping);
        var lookupFactories = new Dictionary<TimelessJewelType, Func<Stream>>();
        foreach (var (type, path) in paths.Lookups)
        {
            lookupFactories[type] = () => OpenAsset(game, path);
        }
        return TimelessJewelData.Load(definitions, mapping, lookupFactories);
    }

    private GameDefinition GameDefinitionFor(GameId gameId) => games.Get(gameId);

    public Stream OpenAsset(GameDefinition game, string relativePath)
    {
        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/{game.AssetRoot.TrimEnd('/')}/{relativePath}");
        return AssetLoader.Open(uri);
    }

    public Bitmap? LoadBitmap(GameDefinition game, string relativePath, string? version = null)
    {
        try
        {
            var path = layouts.Get(game.Id).ResolveBitmapPath(relativePath, version ?? game.DefaultTreeVersion);
            using var stream = OpenAsset(game, path);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public Bitmap? LoadSharedBitmap(string relativePath)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://PathOfAvalonia.TreeApp/Assets/Shared/{relativePath}"));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class TreeImageAssetResolver(
    GameDefinition game,
    IGameAssetService assets,
    IGameAssetLayoutRegistry layouts,
    string? version = null) : ITreeImageAssetResolver
{
    private readonly string _version = version ?? game.DefaultTreeVersion;

    public Bitmap? LoadBitmap(string relativePath)
        => assets.LoadBitmap(game, relativePath, _version);

    public Bitmap? LoadBackground(string treeVersion) =>
        LoadBitmap(layouts.Get(game.Id).BackgroundPath(version ?? treeVersion));

    public Bitmap? LoadJewelRadiusBitmap(string relativePath) =>
        LoadBitmap($"JewelRadius/{relativePath}") ?? assets.LoadSharedBitmap($"JewelRadius/{relativePath}");
}

public sealed class AtlasTreeImageAssetResolver(
    GameDefinition game,
    IGameAssetService assets,
    IGameAssetLayoutRegistry layouts,
    string version) : ITreeImageAssetResolver
{
    public Bitmap? LoadBitmap(string relativePath) =>
        assets.LoadBitmap(game, relativePath, version);

    public Bitmap? LoadBackground(string treeVersion)
    {
        var path = layouts.Get(game.Id).AtlasBackgroundPath(version);
        return path is null ? null : assets.LoadBitmap(game, path, version);
    }
}
