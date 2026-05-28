using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetLayout
{
    GameId GameId { get; }
    string TreeDataPath(string version);
    SpriteDataPaths SpriteDataPaths(string version);
    string BackgroundPath(string version);
    string ResolveBitmapPath(string relativePath, string version) => relativePath;
}
