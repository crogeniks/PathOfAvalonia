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

public sealed class GameAssetService : IGameAssetService
{
    public TreeModel LoadTree(GameDefinition game, string? version = null)
    {
        version ??= game.DefaultTreeVersion;
        using var stream = OpenAsset(game, GameAssetPaths.For(game).TreeDataPath(version));
        return game.TreeLoader.Load(stream, version, game.Id);
    }

    public SpriteMap LoadSprites(GameDefinition game, string? version = null)
    {
        version ??= game.DefaultTreeVersion;
        var layout = GameAssetPaths.For(game);
        var spritePaths = layout.SpriteDataPaths(version);
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
            var path = GameAssetPaths.For(game).ResolveBitmapPath(relativePath, version ?? game.DefaultTreeVersion);
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
    string? version = null) : ITreeImageAssetResolver
{
    private readonly string _version = version ?? game.DefaultTreeVersion;

    public Bitmap? LoadBitmap(string relativePath)
        => assets.LoadBitmap(game, relativePath, _version);

    public Bitmap? LoadBackground(string treeVersion) =>
        LoadBitmap(GameAssetPaths.For(game).BackgroundPath(version ?? treeVersion));

    public Bitmap? LoadJewelRadiusBitmap(string relativePath) =>
        LoadBitmap($"JewelRadius/{relativePath}") ?? assets.LoadSharedBitmap($"JewelRadius/{relativePath}");
}

internal enum SpriteDataKind
{
    Json,
    Poe1GggTree,
    Poe2GggAssets,
}

internal sealed record SpriteDataPaths(SpriteDataKind Kind, string[] Paths, string? AssetPrefix = null);

internal abstract class GameAssetPaths
{
    public static GameAssetPaths For(GameDefinition game) =>
        game.Id switch
        {
            GameId.PathOfExile1 => Poe1GameAssetPaths.Instance,
            GameId.PathOfExile2 => Poe2GameAssetPaths.Instance,
            _ => throw new NotSupportedException($"Unsupported game: {game.Id}"),
        };

    public abstract string TreeDataPath(string version);
    public abstract SpriteDataPaths SpriteDataPaths(string version);
    public abstract string BackgroundPath(string version);
    public virtual string ResolveBitmapPath(string relativePath, string version) => relativePath;

    protected static string VersionFolder(string version) => version.Replace('.', '_');
    protected static string VersionFileSuffix(string version) => version.Replace('.', '_');
}

internal sealed class Poe1GameAssetPaths : GameAssetPaths
{
    public static Poe1GameAssetPaths Instance { get; } = new();

    public override string TreeDataPath(string version) =>
        IsGggTreeVersion(version) ? $"{VersionFolder(version)}/data.json" : $"tree_{VersionFileSuffix(version)}.json";

    public override SpriteDataPaths SpriteDataPaths(string version) =>
        IsGggTreeVersion(version)
            ? new SpriteDataPaths(SpriteDataKind.Poe1GggTree, [TreeDataPath(version)], $"{VersionFolder(version)}/assets")
            : new SpriteDataPaths(SpriteDataKind.Json, [$"sprites_{VersionFileSuffix(version)}.json"]);

    public override string BackgroundPath(string version) =>
        IsGggTreeVersion(version) ? $"{VersionFolder(version)}/assets/background-3.png" : $"background_{VersionFileSuffix(version)}.png";

    private static bool IsGggTreeVersion(string version) => version == "3.28.0";
}

internal sealed class Poe2GameAssetPaths : GameAssetPaths
{
    public static Poe2GameAssetPaths Instance { get; } = new();

    public override string TreeDataPath(string version) => $"{VersionFolder(version)}/data.json";

    public override SpriteDataPaths SpriteDataPaths(string version)
    {
        var prefix = $"{VersionFolder(version)}/assets";
        return new SpriteDataPaths(
            SpriteDataKind.Poe2GggAssets,
            [$"{prefix}/skills.json", $"{prefix}/frame.json", $"{prefix}/jewel.json"]);
    }

    public override string BackgroundPath(string version) => "assets/background.webp";

    public override string ResolveBitmapPath(string relativePath, string version)
    {
        var folder = VersionFolder(version);
        return relativePath.StartsWith($"{folder}/", StringComparison.Ordinal)
            ? relativePath
            : $"{folder}/{relativePath}";
    }
}
