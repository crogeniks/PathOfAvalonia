using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public sealed class Poe1GameAssetLayout : GameAssetLayoutBase
{
    public override GameId GameId => GameId.PathOfExile1;

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
