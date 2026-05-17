using System;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetService
{
    TreeModel LoadTree(GameDefinition game);
    SpriteMap LoadSprites(GameDefinition game);
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
    public TreeModel LoadTree(GameDefinition game)
    {
        using var stream = OpenAsset(game, $"tree_{game.DefaultTreeVersion.Replace('.', '_')}.json");
        return game.TreeLoader.Load(stream, game.DefaultTreeVersion, game.Id);
    }

    public SpriteMap LoadSprites(GameDefinition game)
    {
        using var stream = OpenAsset(game, $"sprites_{game.DefaultTreeVersion.Replace('.', '_')}.json");
        return SpriteMap.LoadFromJson(stream);
    }

    public Stream OpenAsset(GameDefinition game, string relativePath)
    {
        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/{game.AssetRoot.TrimEnd('/')}/{relativePath}");
        return AssetLoader.Open(uri);
    }
}

public sealed class TreeImageAssetResolver(GameDefinition game) : ITreeImageAssetResolver
{
    public Bitmap? LoadBitmap(string relativePath)
    {
        try
        {
            using var s = AssetLoader.Open(new Uri($"avares://PathOfAvalonia.TreeApp/{game.AssetRoot.TrimEnd('/')}/{relativePath}"));
            return new Bitmap(s);
        }
        catch
        {
            return null;
        }
    }

    public Bitmap? LoadBackground(string treeVersion) =>
        LoadBitmap($"background_{treeVersion.Replace('.', '_')}.png");

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
        using var stream = new GameAssetService().OpenAsset(game, $"tree_{version.Replace('.', '_')}.json");
        return game.TreeLoader.Load(stream, version, game.Id);
    }
}
