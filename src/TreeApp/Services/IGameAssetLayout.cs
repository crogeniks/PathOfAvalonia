using System.Collections.Generic;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetLayout
{
    GameId GameId { get; }
    string TreeDataPath(string version);
    SpriteDataPaths SpriteDataPaths(string version);
    IReadOnlyList<string> AdditionalSpriteDataPaths(string version) => [];
    TimelessJewelAssetPaths? TimelessJewelDataPaths(string version) => null;
    string BackgroundPath(string version);
    string? AtlasTreeDataPath(string version) => null;
    SpriteDataPaths? AtlasSpriteDataPaths(string version) => null;
    string? AtlasBackgroundPath(string version) => null;
    string ResolveBitmapPath(string relativePath, string version) => relativePath;
}
