namespace PathOfAvalonia.TreeApp.Services;

public enum SpriteDataKind
{
    Json,
    Poe1GggTree,
    Poe2GggAssets,
}

public sealed record SpriteDataPaths(SpriteDataKind Kind, string[] Paths, string? AssetPrefix = null);
