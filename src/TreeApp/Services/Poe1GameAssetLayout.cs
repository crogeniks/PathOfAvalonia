using System.Collections.Generic;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Jewels;

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

    public override IReadOnlyList<string> AdditionalSpriteDataPaths(string version) =>
        ["TimelessJewels/sprites.json"];

    public override TimelessJewelAssetPaths TimelessJewelDataPaths(string version) => new(
        "TimelessJewels/definitions.json",
        "TimelessJewels/mapping.json",
        new Dictionary<TimelessJewelType, string>
        {
            [TimelessJewelType.GloriousVanity] = "TimelessJewels/glorious-vanity.z",
            [TimelessJewelType.LethalPride] = "TimelessJewels/lethal-pride.z",
            [TimelessJewelType.BrutalRestraint] = "TimelessJewels/brutal-restraint.z",
            [TimelessJewelType.MilitantFaith] = "TimelessJewels/militant-faith.z",
            [TimelessJewelType.ElegantHubris] = "TimelessJewels/elegant-hubris.z",
            [TimelessJewelType.HeroicTragedy] = "TimelessJewels/heroic-tragedy.z",
        });

    public override string BackgroundPath(string version) =>
        IsGggTreeVersion(version) ? $"{VersionFolder(version)}/assets/background-3.png" : $"background_{VersionFileSuffix(version)}.png";

    private static bool IsGggTreeVersion(string version) => version == "3.28.0";
}
