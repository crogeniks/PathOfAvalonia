using System.Collections.Generic;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeApp.Services;

public sealed record TimelessJewelAssetPaths(
    string Definitions,
    string Mapping,
    IReadOnlyDictionary<TimelessJewelType, string> Lookups);
