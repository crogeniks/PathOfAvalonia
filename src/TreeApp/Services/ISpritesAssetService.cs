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
        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/Assets/sprites_{version.Replace('.', '_')}.json");
        using var stream = AssetLoader.Open(uri);
        return SpriteMap.LoadFromJson(stream);
    }
}
