using System;
using System.Collections.Generic;
using PathOfAvalonia.TreeDomain;

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
    public Node? HoverNode =>
        _hoverNodeId is { } id && _spec.Tree.Nodes.TryGetValue(id, out var n) ? n : null;

    public bool IsAllocated(int id) => _spec.IsAllocated(id);

    public MasteryEffect? SelectedMasteryEffect(int nodeId) => _spec.SelectedMasteryEffect(nodeId);

    public void SetHover(int? nodeId)
    {
        if (nodeId == _hoverNodeId)
            return;
        _hoverNodeId = nodeId;
        _hoverPath = nodeId is { } id ? _spec.HoverPathTo(id) : HoverPath.Empty;
        _hoverPathNodes = new HashSet<int>(_hoverPath.Nodes);
        RedrawRequested?.Invoke();
    }

    public void ToggleNode(int id) => _spec.Toggle(id);

    // Allocates all nodes on the current hover path (the queued path-to-target).
    public void AllocatePath() => _spec.AllocateMany(_hoverPath.Nodes);

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
