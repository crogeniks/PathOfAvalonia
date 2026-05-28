using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    public TreeModel Tree { get; }
    public ClassCatalog Classes { get; }
    public GameFeatureFlags Features { get; }

    private readonly HashSet<int> _allocated = new();
    private readonly Dictionary<int, int> _masterySelections = new();
    private readonly Dictionary<int, AttributeNodeOverride> _attributeOverrides = new();
    private readonly Dictionary<int, PassiveAllocationSet> _allocationSets = new();
    private readonly Dictionary<int, int> _classStartNodeByIndex;
    private readonly Dictionary<string, int> _ascendancyStartNodeByName;
    private int _selectedClassIndex;
    private int _selectedAscendancyIndex;

    private readonly Dictionary<int, ClusterSubgraph> _activeSubgraphs = new();
    // Flat lookup for all currently-active cluster nodes by ID.
    private readonly Dictionary<int, Node> _clusterNodes = new();
    private readonly Dictionary<int, ImportedItem> _socketedJewels = new();
    private readonly JewelRadiusTable _jewelRadiusTable;
    private readonly IReadOnlyDictionary<int, RadiusMembership> _socketRadiusMembership;
    private readonly IReadOnlyDictionary<int, RadiusMembership> _keystoneRadiusMembership;
    private readonly Dictionary<string, int> _keystoneNodeIdsByName;
    private readonly List<RadiusJewelEffect> _activeRadiusEffects = new();
    private readonly List<JewelRadiusVisual> _activeJewelRadii = new();

    public IReadOnlyDictionary<int, ClusterSubgraph> ActiveSubgraphs => _activeSubgraphs;
    public IReadOnlyDictionary<int, ImportedItem> SocketedJewels => _socketedJewels;
    public IReadOnlyDictionary<int, AttributeNodeOverride> AttributeOverrides => _attributeOverrides;
    public IReadOnlyDictionary<int, PassiveAllocationSet> AllocationSets => _allocationSets;
    public IReadOnlyList<JewelRadiusVisual> ActiveJewelRadii => _activeJewelRadii;

    public PassiveSpec(TreeModel tree)
        : this(tree, tree.Classes, tree.GameId == GameId.PathOfExile2 ? GameFeatureFlags.Poe2Milestone2 : GameFeatureFlags.Poe1)
    {
    }

    public PassiveSpec(TreeModel tree, ClassCatalog classes, GameFeatureFlags features)
    {
        Tree = tree;
        Classes = classes;
        Features = features;
        _jewelRadiusTable = JewelRadiusTable.For(tree.GameId, tree.Version);
        _socketRadiusMembership = RadiusMembership.BuildForSockets(tree, _jewelRadiusTable);
        _keystoneRadiusMembership = RadiusMembership.BuildForKeystones(tree, _jewelRadiusTable);
        _keystoneNodeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _classStartNodeByIndex = new Dictionary<int, int>();
        _ascendancyStartNodeByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in tree.Nodes.Values)
        {
            if (n is { Type: NodeType.ClassStart, ClassStartIndex: { } idx })
            {
                _classStartNodeByIndex[idx] = n.Id;
            }
            switch (n.Type)
            {
                case NodeType.ClassStart:
                {
                    foreach (var classStartIdx in n.ClassStartIndexes)
                    {
                        _classStartNodeByIndex[classStartIdx] = n.Id;
                    }

                    break;
                }
                case NodeType.AscendancyStart when n.AscendancyName is { } ascendancyName:
                    _ascendancyStartNodeByName[ascendancyName] = n.Id;
                    break;
                case NodeType.Keystone:
                    _keystoneNodeIdsByName[n.Name] = n.Id;
                    break;
            }
        }
        _selectedClassIndex = 0;
        _selectedAscendancyIndex = 0;
        if (_classStartNodeByIndex.TryGetValue(0, out var startId))
        {
            _allocated.Add(startId);
        }
        RebuildActiveRadiusEffects();
    }

    public IReadOnlySet<int> AllocatedNodes => _allocated;
    public bool IsAllocated(int id) => _allocated.Contains(id);
    public PassiveAllocationSet AllocationSetOf(int nodeId) =>
        _allocationSets.GetValueOrDefault(nodeId, PassiveAllocationSet.Normal);

    public bool TryGetSocketedJewel(int socketNodeId, out ImportedItem item) =>
        _socketedJewels.TryGetValue(socketNodeId, out item!);

    public int SelectedClassIndex => _selectedClassIndex;
    public int SelectedAscendancyIndex => _selectedAscendancyIndex;

    public event Action? SpecChanged;

    // Looks up a node by ID in both the base tree and any active cluster subgraphs.
    private bool TryGetNode(int id, out Node? node)
    {
        if (_clusterNodes.TryGetValue(id, out node))
        {
            return true;
        }
        return Tree.Nodes.TryGetValue(id, out node);
    }
}
