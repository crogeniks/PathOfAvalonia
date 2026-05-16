using System;
using System.Collections.Generic;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;

namespace PathOfAvalonia.TreeApp.ViewModels;

// Mediates between PassiveTreeView (rendering) and PassiveSpec (domain state).
// Owns hover state and all spec interactions so the Control stays pure rendering.
public sealed class PassiveTreeViewModel
{
    private readonly PassiveSpec _spec;
    private int? _hoverNodeId;
    private HoverPath _hoverPath = HoverPath.Empty;
    private HashSet<int> _hoverPathNodes = new();

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

    public bool IsAllocated(int id) => _spec.IsAllocated(id);

    public MasteryEffect? SelectedMasteryEffect(int nodeId) => _spec.SelectedMasteryEffect(nodeId);

    // Returns the cluster size for the given socket node, or null if no cluster is active there.
    public ClusterJewelSize? ClusterSizeAt(int socketNodeId) =>
        _spec.ActiveSubgraphs.TryGetValue(socketNodeId, out var sub) ? sub.Size : null;

    public IReadOnlyList<ClusterJewelSize> AllowedClusterSizes(int socketId) => _spec.AllowedClusterSizes(socketId);

    public bool HasClusterAt(int socketId) => _spec.ActiveSubgraphs.ContainsKey(socketId);

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

    public void ToggleNode(int id) => _spec.Toggle(id);

    // Allocates all nodes on the current hover path (the queued path-to-target).
    public void AllocatePath() => _spec.AllocateMany(_hoverPath.Nodes);

    public void InsertCluster(int socketId, ClusterJewelSize size)
    {
        var spec = size switch
        {
            ClusterJewelSize.Large => new ClusterJewelSpec(socketId, size, 8, 2, Array.Empty<string>()),
            ClusterJewelSize.Medium => new ClusterJewelSpec(socketId, size, 4, 1, Array.Empty<string>()),
            _ => new ClusterJewelSpec(socketId, size, 2, 0, Array.Empty<string>()),
        };
        _spec.SetClusterJewel(socketId, spec);
    }

    public void RemoveCluster(int socketId) => _spec.RemoveClusterJewel(socketId);

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
