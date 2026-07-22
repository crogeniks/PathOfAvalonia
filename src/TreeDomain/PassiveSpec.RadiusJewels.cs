using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    private readonly HashSet<int> _timelessConqueredNodeIds = new();
    private readonly HashSet<int> _radiusAllocatableNodeIds = new();
    private readonly Dictionary<int, EffectiveNodeView> _effectiveNodeCache = new();

    public EffectiveNodeView EffectiveNode(int nodeId)
    {
        if (_effectiveNodeCache.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }
        if (!TryGetNode(nodeId, out var node) || node is null)
        {
            throw new KeyNotFoundException($"Node {nodeId} is not present in the passive tree.");
        }

        var effectiveName = node.Name;
        var effectiveIcon = node.Icon;
        var stats = (IReadOnlyList<string>)node.Stats;
        var replacesNode = false;
        var affectedBy = new List<int>();
        TimelessConqueror? conqueror = null;
        var affectingEffects = _activeRadiusEffects
            .Where(effect => EffectAffectsNode(effect, nodeId))
            .OrderBy(effect => effect.SocketNodeId)
            .ToArray();
        var timelessEffect = affectingEffects.FirstOrDefault(effect => effect.TimelessJewel is not null);
        foreach (var effect in affectingEffects)
        {
            affectedBy.Add(effect.SocketNodeId);
            if (effect.Conqueror is { } c)
            {
                conqueror = c;
            }
        }

        if (timelessEffect is not null)
        {
            conqueror = timelessEffect.Conqueror;
            if (timelessEffect.TimelessNodeEffects.TryGetValue(nodeId, out var timelessNode))
            {
                effectiveName = timelessNode.EffectiveName;
                effectiveIcon = timelessNode.EffectiveIcon;
                stats = timelessNode.EffectiveStats;
                replacesNode = timelessNode.ReplacesNode;
            }
        }
        else
        {
            foreach (var effect in affectingEffects)
            {
                foreach (var transform in effect.NodeTransforms)
                {
                    stats = transform.Apply(node, stats);
                }
            }
        }

        var result = new EffectiveNodeView(node, effectiveName, effectiveIcon, stats, replacesNode, conqueror is not null, conqueror, affectedBy);
        _effectiveNodeCache[nodeId] = result;
        return result;
    }

    public bool IsAllocatedByRadiusJewel(int nodeId) =>
        _radiusAllocatableNodeIds.Contains(nodeId);

    public bool IsConqueredByTimelessJewel(int nodeId) =>
        _timelessConqueredNodeIds.Contains(nodeId);

    private void RebuildActiveRadiusEffects()
    {
        RebuildForbiddenJewelAllocations();
        _activeRadiusEffects.Clear();
        _activeJewelRadii.Clear();
        _timelessConqueredNodeIds.Clear();
        _radiusAllocatableNodeIds.Clear();
        _effectiveNodeCache.Clear();
        if (!Features.SupportsPassiveTreeJewels)
        {
            return;
        }

        foreach (var (socketNodeId, item) in _socketedJewels)
        {
            if (!_allocated.Contains(socketNodeId))
            {
                continue;
            }
            if (!Tree.Nodes.TryGetValue(socketNodeId, out var socket))
            {
                continue;
            }
            var effect = RadiusJewelParser.Parse(item, socketNodeId, Tree, _jewelRadiusTable, _keystoneNodeIdsByName);
            if (effect is null || !_jewelRadiusTable.TryGet(effect.RadiusIndex, out var band))
            {
                continue;
            }

            effect = ResolveTimelessNodeEffects(effect);

            _activeRadiusEffects.Add(effect);
            var center = effect.AlternateCenterNodeId is { } centerNodeId && Tree.Nodes.TryGetValue(centerNodeId, out var alternate)
                ? alternate
                : socket;
            _activeJewelRadii.Add(new JewelRadiusVisual(
                socketNodeId,
                center.X,
                center.Y,
                band.Inner,
                band.Outer,
                VisualStyle(effect, band),
                effect.Conqueror));
        }

        AddOracleKeystoneAllocationRadii();
        RebuildDerivedRadiusEffectCaches();
    }

    private void RebuildDerivedRadiusEffectCaches()
    {
        foreach (var effect in _activeRadiusEffects)
        {
            var sourceId = effect.AlternateCenterNodeId ?? effect.SocketNodeId;
            var memberships = effect.AlternateCenterNodeId is null
                ? _socketRadiusMembership
                : _keystoneRadiusMembership;
            if (!memberships.TryGetValue(sourceId, out var membership)
                || !membership.NodesByRadiusIndex.TryGetValue(effect.RadiusIndex, out var nodeIds))
            {
                continue;
            }

            if (effect.TimelessJewel is not null)
            {
                _timelessConqueredNodeIds.UnionWith(nodeIds);
            }
            if (effect.AllowsUnconnectedAllocation)
            {
                _radiusAllocatableNodeIds.UnionWith(nodeIds);
            }
        }
    }

    private RadiusJewelEffect ResolveTimelessNodeEffects(RadiusJewelEffect effect)
    {
        if (effect.TimelessJewel is not { } timelessJewel
            || _timelessJewelData.IsEmpty
            || !_socketRadiusMembership.TryGetValue(effect.SocketNodeId, out var membership)
            || !membership.NodesByRadiusIndex.TryGetValue(effect.RadiusIndex, out var nodeIds))
        {
            return effect;
        }

        var nodes = nodeIds
            .Select(nodeId => Tree.Nodes.GetValueOrDefault(nodeId))
            .Where(node => node is not null)
            .Cast<Node>();
        return effect with
        {
            TimelessNodeEffects = _timelessJewelData.Resolve(timelessJewel, nodes),
        };
    }

    private void AddOracleKeystoneAllocationRadii()
    {
        if (Tree.GameId != GameId.PathOfExile2)
        {
            return;
        }
        var radiusIndex = _jewelRadiusTable.NormalRadiusIndex("Medium");
        if (radiusIndex is not { } mediumRadiusIndex || !_jewelRadiusTable.TryGet(mediumRadiusIndex, out var band))
        {
            return;
        }

        foreach (var oracleNode in Tree.Nodes.Values.Where(IsAllocatedOracleKeystoneAllocationNode))
        {
            var item = new ImportedItem(
                string.Empty,
                "Ascendancy",
                oracleNode.Name,
                "Oracle Ascendancy Passive",
                string.Join('\n', oracleNode.Stats));
            foreach (var keystone in Tree.Nodes.Values.Where(node => node.Type == NodeType.Keystone && _allocated.Contains(node.Id)))
            {
                _activeRadiusEffects.Add(new RadiusJewelEffect(
                    oracleNode.Id,
                    item,
                    RadiusJewelKind.OracleKeystoneAllocation,
                    mediumRadiusIndex,
                    keystone.Id,
                    Conqueror: null,
                    NodeTransforms: [],
                    AllowsUnconnectedAllocation: true));
                _activeJewelRadii.Add(new JewelRadiusVisual(
                    oracleNode.Id,
                    keystone.X,
                    keystone.Y,
                    band.Inner,
                    band.Outer,
                    JewelRadiusVisualStyle.OracleKeystoneCentered,
                    Conqueror: null));
            }
        }
    }

    private bool IsAllocatedOracleKeystoneAllocationNode(Node node) =>
        _allocated.Contains(node.Id)
        && IsOracleKeystoneAllocationNode(node);

    private bool IsOracleKeystoneAllocationNode(Node node) =>
        CanAllocateNodeRules(node)
        && node.AscendancyName == "Oracle"
        && node.Stats.Any(stat => stat.Contains("Non-Keystone Passive Skills in Medium Radius of allocated Keystone Passive Skills can be allocated without being connected to your tree", StringComparison.OrdinalIgnoreCase));

    private bool AllocationChangeAffectsRadiusEffects(int nodeId)
    {
        if (_activeRadiusEffects.Any(effect => effect.SocketNodeId == nodeId))
        {
            return true;
        }

        return TryGetNode(nodeId, out var node)
            && node is not null
            && (node.Type is NodeType.JewelSocket or NodeType.Keystone
                || (Tree.GameId == GameId.PathOfExile2 && IsOracleKeystoneAllocationNode(node)));
    }

    private static JewelRadiusVisualStyle VisualStyle(RadiusJewelEffect effect, JewelRadiusBand band)
    {
        if (effect.Conqueror is not null)
        {
            return JewelRadiusVisualStyle.Timeless;
        }
        if (effect.AlternateCenterNodeId is not null)
        {
            return JewelRadiusVisualStyle.KeystoneCentered;
        }
        return band.IsAnnulus ? JewelRadiusVisualStyle.Annulus : JewelRadiusVisualStyle.Normal;
    }

    private bool EffectAffectsNode(RadiusJewelEffect effect, int nodeId)
    {
        if (effect.Kind == RadiusJewelKind.OracleKeystoneAllocation
            && Tree.Nodes.TryGetValue(nodeId, out var node)
            && node.Type == NodeType.Keystone)
        {
            return false;
        }
        var sourceId = effect.AlternateCenterNodeId ?? effect.SocketNodeId;
        var memberships = effect.AlternateCenterNodeId is not null ? _keystoneRadiusMembership : _socketRadiusMembership;
        return memberships.TryGetValue(sourceId, out var membership)
            && membership.Contains(effect.RadiusIndex, nodeId);
    }

    private void PruneInvalidRadiusOnlyAllocations()
    {
        var roots = DependencyRoots(excludeId: -1).Select(r => r.Id).ToArray();
        if (roots.Length == 0)
        {
            return;
        }

        var reachable = NormallyReachableAllocatedNodes(roots);
        var changed = false;
        foreach (var id in _allocated.ToArray())
        {
            if (reachable.Contains(id) || IsAllocatedByRadiusJewel(id))
            {
                continue;
            }
            _allocated.Remove(id);
            _masterySelections.Remove(id);
            _allocationSets.Remove(id);
            changed = true;
        }
        if (changed)
        {
            RebuildActiveRadiusEffects();
        }
    }
}
