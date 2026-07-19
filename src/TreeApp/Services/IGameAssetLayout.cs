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
    string ResolveBitmapPath(string relativePath, string version) => relativePath;
}
