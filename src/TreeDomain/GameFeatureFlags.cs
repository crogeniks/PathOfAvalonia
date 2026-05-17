namespace PathOfAvalonia.TreeDomain;

public sealed record GameFeatureFlags(
    bool SupportsBuildImport,
    bool SupportsClusterJewels,
    bool SupportsSocketedJewelOverlays,
    bool SupportsEquipmentImport)
{
    public static readonly GameFeatureFlags Poe1 = new(true, true, true, true);
    public static readonly GameFeatureFlags Poe2Milestone1 = new(false, false, false, false);
}

