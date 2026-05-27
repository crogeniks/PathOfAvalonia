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
        using var stream = OpenAsset(game, GetTreePath(game, version));
        return game.TreeLoader.Load(stream, version, game.Id);
    }

    public SpriteMap LoadSprites(GameDefinition game, string? version = null)
    {
        version ??= game.DefaultTreeVersion;
        if (game.Id == GameId.PathOfExile2)
        {
            var prefix = $"{VersionFolder(version)}/assets";
            using var skills = OpenAsset(game, $"{prefix}/skills.json");
            using var frames = OpenAsset(game, $"{prefix}/frame.json");
            using var jewels = OpenAsset(game, $"{prefix}/jewel.json");
            return SpriteMap.LoadPoe2FromGggAssets(skills, frames, jewels);
        }

        if (game.Id == GameId.PathOfExile1 && version == "3.28.0")
        {
            using var treeStream = OpenAsset(game, $"{VersionFolder(version)}/data.json");
            return SpriteMap.LoadPoe1FromGggTree(treeStream, $"{VersionFolder(version)}/assets");
        }

        using var stream = OpenAsset(game, $"sprites_{version.Replace('.', '_')}.json");
        return SpriteMap.LoadFromJson(stream);
    }

    public Stream OpenAsset(GameDefinition game, string relativePath)
    {
        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/{game.AssetRoot.TrimEnd('/')}/{relativePath}");
        return AssetLoader.Open(uri);
    }

    private static string GetTreePath(GameDefinition game, string version) =>
        game.Id switch
        {
            GameId.PathOfExile2 => $"{VersionFolder(version)}/data.json",
            GameId.PathOfExile1 when version == "3.28.0" => $"{VersionFolder(version)}/data.json",
            _ => $"tree_{version.Replace('.', '_')}.json",
        };

    private static string VersionFolder(string version) => version.Replace('.', '_');
}

public sealed class TreeImageAssetResolver(GameDefinition game, string? version = null) : ITreeImageAssetResolver
{
    public Bitmap? LoadBitmap(string relativePath)
    {
        try
        {
            var path = ResolvePath(relativePath);
            using var s = AssetLoader.Open(new Uri($"avares://PathOfAvalonia.TreeApp/{game.AssetRoot.TrimEnd('/')}/{path}"));
            return new Bitmap(s);
        }
        catch
        {
            return null;
        }
    }

    public Bitmap? LoadBackground(string treeVersion) =>
        game.Id switch
        {
            GameId.PathOfExile2 => LoadBitmap("assets/background.webp"),
            GameId.PathOfExile1 when (version ?? treeVersion) == "3.28.0" => LoadBitmap($"{VersionFolder(version ?? treeVersion)}/assets/background-3.png"),
            _ => LoadBitmap($"background_{treeVersion.Replace('.', '_')}.png"),
        };

    public Bitmap? LoadJewelRadiusBitmap(string relativePath) =>
        LoadBitmap($"JewelRadius/{relativePath}") ?? LoadSharedBitmap($"JewelRadius/{relativePath}");

    private static Bitmap? LoadSharedBitmap(string relativePath)
    {
        try
        {
            using var s = AssetLoader.Open(new Uri($"avares://PathOfAvalonia.TreeApp/Assets/Shared/{relativePath}"));
            return new Bitmap(s);
        }
        catch
        {
            return null;
        }
    }

    private static string VersionFolder(string version) => version.Replace('.', '_');

    private string ResolvePath(string relativePath)
    {
        if (game.Id != GameId.PathOfExile2)
        {
            return relativePath;
        }

        var folder = VersionFolder(version ?? game.DefaultTreeVersion);
        return relativePath.StartsWith($"{folder}/", StringComparison.Ordinal)
            ? relativePath
            : $"{folder}/{relativePath}";
    }
}

public interface ITreeAssetService
{
    TreeModel Load(string version);
}

public sealed class TreeAssetService : ITreeAssetService
{
    public TreeModel Load(string version)
    {
        var game = GameRegistry.CreatePoe1();
        var path = version == "3.28.0"
            ? $"{version.Replace('.', '_')}/data.json"
            : $"tree_{version.Replace('.', '_')}.json";
        using var stream = new GameAssetService().OpenAsset(game, path);
        return game.TreeLoader.Load(stream, version, game.Id);
    }
}
