using System;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface ISpritesAssetService
{
    SpriteMap Load(string version);
}

public sealed class SpritesAssetService : ISpritesAssetService
{
    public SpriteMap Load(string version)
    {
        if (version == "3.28.0")
        {
            var treeUri = new Uri($"avares://PathOfAvalonia.TreeApp/Assets/PoE1/{version.Replace('.', '_')}/data.json");
            using var treeStream = AssetLoader.Open(treeUri);
            return SpriteMap.LoadPoe1FromGggTree(treeStream, $"{version.Replace('.', '_')}/assets");
        }

        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/Assets/PoE1/sprites_{version.Replace('.', '_')}.json");
        using var stream = AssetLoader.Open(uri);
        return SpriteMap.LoadFromJson(stream);
    }
}
