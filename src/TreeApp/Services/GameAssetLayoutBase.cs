using System.Collections.Generic;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public abstract class GameAssetLayoutBase : IGameAssetLayout
{
    public abstract GameId GameId { get; }
    public abstract string TreeDataPath(string version);
    public abstract SpriteDataPaths SpriteDataPaths(string version);
    public virtual IReadOnlyList<string> AdditionalSpriteDataPaths(string version) => [];
    public virtual TimelessJewelAssetPaths? TimelessJewelDataPaths(string version) => null;
    public abstract string BackgroundPath(string version);
    public virtual string ResolveBitmapPath(string relativePath, string version) => relativePath;

    protected static string VersionFolder(string version) => version.Replace('.', '_');
    protected static string VersionFileSuffix(string version) => version.Replace('.', '_');
}
