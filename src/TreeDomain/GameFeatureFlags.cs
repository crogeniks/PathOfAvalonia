namespace PathOfAvalonia.TreeDomain;

public sealed record GameFeatureFlags(
    bool SupportsBuildCodeImport,
    bool SupportsTreeUrlImport,
    bool SupportsEquipmentImport,
    bool SupportsPassiveTreeJewels,
    bool SupportsClusterJewels,
    bool SupportsSocketedJewelOverlays,
    bool SupportsMasterySelections,
    bool SupportsAttributeOverrides)
{
    public static readonly GameFeatureFlags Poe1 = new(
        SupportsBuildCodeImport: true,
        SupportsTreeUrlImport: true,
        SupportsEquipmentImport: true,
        SupportsPassiveTreeJewels: true,
        SupportsClusterJewels: true,
        SupportsSocketedJewelOverlays: true,
        SupportsMasterySelections: true,
        SupportsAttributeOverrides: false);

    public static readonly GameFeatureFlags Poe2Milestone2 = new(
        SupportsBuildCodeImport: true,
        SupportsTreeUrlImport: false,
        SupportsEquipmentImport: true,
        SupportsPassiveTreeJewels: true,
        SupportsClusterJewels: false,
        SupportsSocketedJewelOverlays: true,
        SupportsMasterySelections: true,
        SupportsAttributeOverrides: true);

    public static readonly GameFeatureFlags Poe2Milestone1 = Poe2Milestone2;
}
