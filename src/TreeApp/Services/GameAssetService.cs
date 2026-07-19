using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetService
{
    Task<TreeModel> LoadTreeAsync(GameDefinition game, string? version = null);
    Task<SpriteMap> LoadSpritesAsync(GameDefinition game, string? version = null);
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

    private TreeModel LoadTree(GameAssetKey key)
    {
        var game = GameDefinitionFor(key.GameId);
        using var stream = OpenAsset(game, layouts.Get(game.Id).TreeDataPath(key.Version));
        return game.TreeLoader.Load(stream, key.Version, game.Id);
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
            return SpriteMap.LoadPoe2FromGggAssets(skills, frames, jewels, masteryEffectDisabled, masteryEffectActive);
        }

        if (spritePaths.Kind == SpriteDataKind.Poe1GggTree)
        {
            using var treeStream = OpenAsset(game, spritePaths.Paths[0]);
            return SpriteMap.LoadPoe1FromGggTree(treeStream, spritePaths.AssetPrefix!);
        }

        using var stream = OpenAsset(game, spritePaths.Paths[0]);
        return SpriteMap.LoadFromJson(stream);
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
