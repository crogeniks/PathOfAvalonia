namespace PathOfAvalonia.TreeDomain.Import;

public sealed record ImportedBuild(
    int ClassId,
    int AscendClassId,
    int SecondaryAscendClassId,
    IReadOnlyList<int> NodeHashes,
    IReadOnlyList<int> ClusterNodeHashes,
    IReadOnlyDictionary<int, int> MasterySelections,
    string? TreeVersion,
    string Source)
{
    public int TotalAllocatedCount => NodeHashes.Count + ClusterNodeHashes.Count;
    public int ClusterHashFormatVersion { get; init; } = 2;
    public string? ClassInternalId { get; init; }
    public string? AscendancyInternalId { get; init; }
    public IReadOnlyDictionary<int, AttributeNodeOverride> AttributeOverrides { get; init; } =
        new Dictionary<int, AttributeNodeOverride>();
    public IReadOnlyDictionary<int, PassiveAllocationSet> AllocationSets { get; init; } =
        new Dictionary<int, PassiveAllocationSet>();
    public IReadOnlyList<ImportedItem> Items { get; init; } = [];
    public IReadOnlyDictionary<int, ImportedItem> ItemsById { get; init; } = new Dictionary<int, ImportedItem>();
    public IReadOnlyList<ImportedSocketedJewel> SocketedJewels { get; init; } = [];
    public IReadOnlyList<ImportedPassiveTreeVariant> PassiveTreeVariants { get; init; } = [];
    public IReadOnlyList<ImportedItemSetVariant> ItemSetVariants { get; init; } = [];
    public int ActivePassiveTreeVariantIndex { get; init; }
    public int ActiveItemSetVariantIndex { get; init; }
    public string? RawXml { get; init; }
    public ImportedSkills Skills { get; init; } = ImportedSkills.Empty;
    public ImportedBuildMetrics Metrics { get; init; } = ImportedBuildMetrics.Empty;

    public ImportedBuild WithPassiveTreeVariant(int index)
    {
        var variant = PassiveTreeVariants.FirstOrDefault(v => v.Index == index);
        if (variant is null)
        {
            if (PassiveTreeVariants.Count == 0 && index == 0)
            {
                return this;
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return this with
        {
            ClassId = variant.ClassId,
            AscendClassId = variant.AscendClassId,
            SecondaryAscendClassId = variant.SecondaryAscendClassId,
            NodeHashes = variant.NodeHashes,
            ClusterNodeHashes = variant.ClusterNodeHashes,
            MasterySelections = variant.MasterySelections,
            TreeVersion = variant.TreeVersion,
            ClusterHashFormatVersion = variant.ClusterHashFormatVersion,
            ClassInternalId = variant.ClassInternalId,
            AscendancyInternalId = variant.AscendancyInternalId,
            AttributeOverrides = variant.AttributeOverrides,
            AllocationSets = variant.AllocationSets,
            SocketedJewels = variant.SocketedJewels,
            ActivePassiveTreeVariantIndex = index,
        };
    }

    public ImportedBuild WithItemSetVariant(int index)
    {
        var variant = ItemSetVariants.FirstOrDefault(v => v.Index == index);
        if (variant is null)
        {
            if (ItemSetVariants.Count == 0 && index == 0)
            {
                return this;
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return this with
        {
            Items = variant.Items,
            ActiveItemSetVariantIndex = index,
        };
    }

    public ImportedBuild WithVariants(int passiveIndex, int itemSetIndex) =>
        WithPassiveTreeVariant(passiveIndex).WithItemSetVariant(itemSetIndex);
}

public sealed record ImportedPassiveTreeVariant(
    int Index,
    string DisplayName,
    int ClassId,
    int AscendClassId,
    int SecondaryAscendClassId,
    IReadOnlyList<int> NodeHashes,
    IReadOnlyList<int> ClusterNodeHashes,
    IReadOnlyDictionary<int, int> MasterySelections,
    string? TreeVersion,
    int ClusterHashFormatVersion,
    string? ClassInternalId,
    string? AscendancyInternalId,
    IReadOnlyDictionary<int, AttributeNodeOverride> AttributeOverrides,
    IReadOnlyList<ImportedSocketedJewel> SocketedJewels)
{
    public IReadOnlyDictionary<int, PassiveAllocationSet> AllocationSets { get; init; } =
        new Dictionary<int, PassiveAllocationSet>();
}

public sealed record ImportedItemSetVariant(
    int Index,
    int Id,
    string DisplayName,
    IReadOnlyList<ImportedItem> Items);

public enum AttributeNodeOverride
{
    Strength = 1,
    Dexterity = 2,
    Intelligence = 3,
}

public sealed record ImportedSkills(
    IReadOnlyList<ImportedSkillSet> SkillSets,
    int ActiveSkillSetIndex,
    int MainSocketGroupIndex)
{
    public static ImportedSkills Empty { get; } = new([], 0, 0);
}

public sealed record ImportedSkillSet(
    int Index,
    int Id,
    string DisplayName,
    IReadOnlyList<ImportedSkillGroup> Groups);

public sealed record ImportedSkillGroup(
    int Index,
    string Label,
    string? Slot,
    string? Source,
    bool Enabled,
    bool IncludeInFullDps,
    int GroupCount,
    int MainActiveSkillIndex,
    int MainActiveSkillCalcsIndex,
    IReadOnlyList<ImportedGem> Gems);

public sealed record ImportedGem(
    string NameSpec,
    string? GemId,
    string? SkillId,
    string? VariantId,
    int? Level,
    int? Quality,
    bool Enabled,
    bool EnableGlobal1,
    bool EnableGlobal2,
    int Count,
    int? SkillPart,
    int? SkillPartCalcs,
    int? SkillStageCount,
    int? SkillStageCountCalcs,
    int? SkillMineCount,
    int? SkillMineCountCalcs,
    string? SkillMinion,
    string? SkillMinionCalcs,
    int? SkillMinionItemSet,
    int? SkillMinionItemSetCalcs,
    int? SkillMinionSkill,
    int? SkillMinionSkillCalcs);

public enum ImportedMetricSource
{
    None,
    SavedXmlSnapshot,
    PobBackend,
}

public sealed record ImportedBuildMetrics(
    ImportedMetricSource Source,
    string? BackendName,
    string? BackendVersion,
    string? BackendPath,
    IReadOnlyList<ImportedStatMetric> PlayerStats,
    IReadOnlyList<ImportedSkillDpsMetric> SkillDps,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage)
{
    public static ImportedBuildMetrics Empty { get; } = new(
        ImportedMetricSource.None,
        null,
        null,
        null,
        [],
        [],
        [],
        null);
}

public sealed record ImportedStatMetric(
    string Stat,
    string Label,
    double? NumericValue,
    string DisplayValue);

public sealed record ImportedSkillDpsMetric(
    string Name,
    double? Dps,
    string DisplayDps,
    int Count,
    string? SkillPart,
    string? Source);
