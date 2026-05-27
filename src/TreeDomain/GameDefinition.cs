using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed record GameDefinition(
    GameId Id,
    string DisplayName,
    string ShortName,
    string DefaultTreeVersion,
    string AssetRoot,
    ITreeLoader TreeLoader,
    IImportStrategy ImportStrategy,
    GameFeatureFlags FeatureFlags,
    IReadOnlyList<string>? AvailableTreeVersions = null)
{
    public IReadOnlyList<string> TreeVersions =>
        AvailableTreeVersions is { Count: > 0 } ? AvailableTreeVersions : [DefaultTreeVersion];
}

public interface ITreeLoader
{
    TreeModel Load(Stream stream, string version, GameId gameId);
}

public interface IImportStrategy
{
    bool IsSupported { get; }
    ImportedBuild Import(string text);
}

public sealed class Poe1ImportStrategy : IImportStrategy
{
    public bool IsSupported => true;
    public ImportedBuild Import(string text) => BuildImporter.Import(text);
}

public sealed class UnsupportedImportStrategy : IImportStrategy
{
    public bool IsSupported => false;
    public ImportedBuild Import(string text) =>
        throw new NotSupportedException("Build import is not available for Path of Exile 2 yet.");
}

public sealed class Poe2ImportStrategy : IImportStrategy
{
    public bool IsSupported => true;
    public ImportedBuild Import(string text) => Poe2BuildImporter.Import(text);
}
