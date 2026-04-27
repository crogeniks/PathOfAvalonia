using System;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface ITreeAssetService
{
    TreeModel Load(string version);
}

public sealed class TreeAssetService : ITreeAssetService
{
    public TreeModel Load(string version)
    {
        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/Assets/tree_{version.Replace('.', '_')}.json");
        using var stream = AssetLoader.Open(uri);
        return TreeLoader.LoadFromJson(stream, version);
    }
}
