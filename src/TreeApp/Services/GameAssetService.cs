using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetService
{
    TreeModel LoadTree(GameDefinition game, string? version = null);
    SpriteMap LoadSprites(GameDefinition game, string? version = null);
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

public sealed class GameAssetService(IGameAssetLayoutRegistry layouts) : IGameAssetService
{
    public TreeModel LoadTree(GameDefinition game, string? version = null)
    {
        version ??= game.DefaultTreeVersion;
        using var stream = OpenAsset(game, layouts.Get(game.Id).TreeDataPath(version));
        return game.TreeLoader.Load(stream, version, game.Id);
    }

    public SpriteMap LoadSprites(GameDefinition game, string? version = null)
    {
        version ??= game.DefaultTreeVersion;
        var spritePaths = layouts.Get(game.Id).SpriteDataPaths(version);
        if (spritePaths.Kind == SpriteDataKind.Poe2GggAssets)
        {
            using var skills = OpenAsset(game, spritePaths.Paths[0]);
            using var frames = OpenAsset(game, spritePaths.Paths[1]);
            using var jewels = OpenAsset(game, spritePaths.Paths[2]);
            return SpriteMap.LoadPoe2FromGggAssets(skills, frames, jewels);
        }

        if (spritePaths.Kind == SpriteDataKind.Poe1GggTree)
        {
            using var treeStream = OpenAsset(game, spritePaths.Paths[0]);
            return SpriteMap.LoadPoe1FromGggTree(treeStream, spritePaths.AssetPrefix!);
        }

        using var stream = OpenAsset(game, spritePaths.Paths[0]);
        return SpriteMap.LoadFromJson(stream);
    }

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
