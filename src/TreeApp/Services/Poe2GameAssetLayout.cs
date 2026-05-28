using System;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public sealed class Poe2GameAssetLayout : GameAssetLayoutBase
{
    public override GameId GameId => GameId.PathOfExile2;

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
