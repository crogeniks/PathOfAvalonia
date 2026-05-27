using System;
using System.Collections.Generic;
using System.Linq;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeApp.ViewModels;

// Mediates between PassiveTreeView (rendering) and PassiveSpec (domain state).
// Owns hover state and all spec interactions so the Control stays pure rendering.
public sealed class PassiveTreeViewModel
{
    private readonly PassiveSpec _spec;
    private int? _hoverNodeId;
    private HoverPath _hoverPath = HoverPath.Empty;
    private HashSet<int> _hoverPathNodes = new();
    private TreeDiff _diff = TreeDiff.Empty;

    // Fired whenever visual state changes (hover update or spec change).
    // PassiveTreeView subscribes and calls InvalidateVisual().
    public event Action? RedrawRequested;

    public PassiveTreeViewModel(PassiveSpec spec)
    {
        _spec = spec;
        _spec.SpecChanged += OnSpecChanged;
    }

    public TreeModel Tree => _spec.Tree;
    public IReadOnlySet<int> AllocatedNodes => _spec.AllocatedNodes;
    public int? HoverNodeId => _hoverNodeId;
    public HoverPath HoverPath => _hoverPath;
    public HashSet<int> HoverPathNodes => _hoverPathNodes;
    public TreeDiff Diff => _diff;
    public Node? HoverNode
    {
        get
        {
            if (_hoverNodeId is not { } id)
            {
                return null;
            }
            if (_spec.Tree.Nodes.TryGetValue(id, out var n))
            {
                return n;
            }
            // Cluster nodes aren't in the base tree — check active subgraphs.
            foreach (var sub in _spec.ActiveSubgraphs.Values)
            {
                foreach (var cn in sub.Nodes)
                {
                    if (cn.Id == id)
                    {
                        return cn;
                    }
                }
            }
            return null;
        }
    }

    public IReadOnlyDictionary<int, ClusterSubgraph> ActiveClusters => _spec.ActiveSubgraphs;
    public IReadOnlyList<JewelRadiusVisual> ActiveJewelRadii => _spec.ActiveJewelRadii;

    public bool IsAllocated(int id) => _spec.IsAllocated(id);

    public PassiveAllocationSet AllocationSetOf(int nodeId) => _spec.AllocationSetOf(nodeId);

    public MasteryEffect? SelectedMasteryEffect(int nodeId) => _spec.SelectedMasteryEffect(nodeId);

    public EffectiveNodeView EffectiveNode(int nodeId) => _spec.EffectiveNode(nodeId);

    public IEnumerable<string> PassiveEffectLines(Node node) => _spec.EffectiveNode(node.Id).EffectiveStats;

    public ImportedItem? SocketedJewelAt(int socketNodeId) =>
        _spec.TryGetSocketedJewel(socketNodeId, out var item) ? item : null;

    public string? SocketedJewelOverlayAt(Node socketNode)
    {
        if (socketNode.Type != NodeType.JewelSocket)
        {
            return null;
        }

        if (_spec.TryGetSocketedJewel(socketNode.Id, out var item))
        {
            return SocketedJewelVisualClassifier.OverlayKey(item, socketNode.ExpansionSocket is not null);
        }

        return ClusterSizeAt(socketNode.Id) switch
        {
            ClusterJewelSize.Large => SocketedJewelVisualClassifier.OverlayKey(SocketedJewelVisualKind.LargeCluster, socketNode.ExpansionSocket is not null),
            ClusterJewelSize.Medium => SocketedJewelVisualClassifier.OverlayKey(SocketedJewelVisualKind.MediumCluster, socketNode.ExpansionSocket is not null),
            ClusterJewelSize.Small => SocketedJewelVisualClassifier.OverlayKey(SocketedJewelVisualKind.SmallCluster, socketNode.ExpansionSocket is not null),
            _ => null,
        };
    }

    // Returns the cluster size for the given socket node, or null if no cluster is active there.
    public ClusterJewelSize? ClusterSizeAt(int socketNodeId) =>
        _spec.ActiveSubgraphs.TryGetValue(socketNodeId, out var sub) ? sub.Size : null;

    public IReadOnlyList<ClusterJewelSize> AllowedClusterSizes(int socketId) => _spec.AllowedClusterSizes(socketId);

    public bool HasClusterAt(int socketId) => _spec.ActiveSubgraphs.ContainsKey(socketId);

    public IReadOnlyList<int> ManualPassiveCounts(ClusterJewelSize size)
    {
        var definition = ClusterJewelData.GetDefinition(size);
        return Enumerable.Range(definition.MinNodes, definition.MaxNodes - definition.MinNodes + 1).ToArray();
    }

    public IReadOnlyList<int> ManualNotableCounts(ClusterJewelSize size, int passiveCount)
    {
        var socketCount = DefaultSocketCount(size);
        var maxByStructure = Math.Max(0, passiveCount - socketCount - 1);
        var max = Math.Min(MaxManualNotables(size), maxByStructure);
        return Enumerable.Range(0, max + 1).ToArray();
    }

    public void SetHover(int? nodeId)
    {
        if (nodeId == _hoverNodeId)
        {
            return;
        }
        _hoverNodeId = nodeId;
        _hoverPath = nodeId is { } id ? _spec.HoverPathTo(id) : HoverPath.Empty;
        _hoverPathNodes = new HashSet<int>(_hoverPath.Nodes);
        RedrawRequested?.Invoke();
    }

    public void SetDiff(TreeDiff? diff)
    {
        _diff = diff ?? TreeDiff.Empty;
        RedrawRequested?.Invoke();
    }

    public void ToggleNode(int id) => _spec.Toggle(id);

    // Allocates all nodes on the current hover path (the queued path-to-target).
    public void AllocatePath() => _spec.AllocateMany(_hoverPath.Nodes);

    public void InsertCluster(int socketId, ClusterJewelSize size)
    {
        InsertCluster(socketId, size, DefaultPassiveCount(size), 0);
    }

    public void InsertCluster(int socketId, ClusterJewelSize size, int passiveCount, int notableCount)
    {
        var definition = ClusterJewelData.GetDefinition(size);
        passiveCount = Math.Clamp(passiveCount, definition.MinNodes, definition.MaxNodes);
        var allowedNotables = ManualNotableCounts(size, passiveCount);
        notableCount = allowedNotables.Count == 0 ? 0 : Math.Clamp(notableCount, allowedNotables[0], allowedNotables[^1]);

        var spec = new ClusterJewelSpec(
            socketId,
            size,
            passiveCount,
            DefaultSocketCount(size),
            BuildPlaceholderNotables(size, notableCount));
        _spec.SetClusterJewel(socketId, spec);
    }

    public void RemoveCluster(int socketId) => _spec.RemoveClusterJewel(socketId);

    private static int DefaultPassiveCount(ClusterJewelSize size) => size switch
    {
        ClusterJewelSize.Large => 8,
        ClusterJewelSize.Medium => 4,
        _ => 2,
    };

    private static int DefaultSocketCount(ClusterJewelSize size) => size switch
    {
        ClusterJewelSize.Large => 2,
        ClusterJewelSize.Medium => 1,
        _ => 0,
    };

    private static int MaxManualNotables(ClusterJewelSize size) => size switch
    {
        ClusterJewelSize.Large => 3,
        ClusterJewelSize.Medium => 2,
        _ => 1,
    };

    private static IReadOnlyList<string> BuildPlaceholderNotables(ClusterJewelSize size, int notableCount) =>
        Enumerable.Range(1, notableCount)
            .Select(index => $"{size} Cluster Notable {index}")
            .ToArray();

    private void OnSpecChanged()
    {
        // Hover path may be invalidated by the allocation change — recompute.
        if (_hoverNodeId is { } id)
        {
            _hoverPath = _spec.HoverPathTo(id);
            _hoverPathNodes = new HashSet<int>(_hoverPath.Nodes);
        }
        RedrawRequested?.Invoke();
    }
}
