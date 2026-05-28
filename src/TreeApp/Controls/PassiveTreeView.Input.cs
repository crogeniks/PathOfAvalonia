using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;

namespace PathOfAvalonia.TreeApp.Controls;

public sealed partial class PassiveTreeView
{
    private int? HitTest(Point screen)
    {
        var (tx, ty) = ScreenToTree(screen);
        var best = double.MaxValue;
        int? bestId = null;

        foreach (var n in DrawableBaseNodes())
        {
            CheckNode(n);
        }

        foreach (var n in ActiveClusterNodes())
        {
            CheckNode(n);
        }

        return bestId;

        void CheckNode(Node n)
        {
            var dx = tx - n.X;
            var dy = ty - n.Y;
            if (Math.Abs(dx) > HitMaxRadius || Math.Abs(dy) > HitMaxRadius)
            {
                return;
            }
            var d = dx * dx + dy * dy;
            var rsq = n.Type switch
            {
                NodeType.Keystone => HitRsqKeystone,
                NodeType.Notable => HitRsqNotable,
                NodeType.JewelSocket => HitRsqSocket,
                NodeType.Mastery => HitRsqMastery,
                NodeType.ClassStart => HitRsqKeystone,
                NodeType.AscendancyStart => HitRsqKeystone,
                _ => HitRsqNormal,
            };
            if (d < rsq && d < best)
            {
                best = d;
                bestId = n.Id;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        _lastPointerPosition = p;
        if (_panning)
        {
            UpdatePan(p);
            return;
        }

        var hit = HitTest(p);
        if (hit != _vm.HoverNodeId)
        {
            _lastTooltipRedrawPosition = p;
            _vm.SetHover(hit);
        }
        else if (hit is not null && DistanceSquared(p, _lastTooltipRedrawPosition) > 16)
        {
            _lastTooltipRedrawPosition = p;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        var p = e.GetPosition(this);
        _lastPointerPosition = p;
        if (props.IsLeftButtonPressed)
        {
            BeginPan(e, p);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_panning)
        {
            EndPan(e);
            return;
        }

        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            var p = e.GetPosition(this);
            var hit = HitTest(p);
            if (hit is { } socketId && ShowClusterContextMenu(socketId, p))
            {
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var p = e.GetPosition(this);
        var (txBefore, tyBefore) = ScreenToTree(p);
        var factor = Math.Pow(1.2, e.Delta.Y);
        var maxZoom = _vm.Tree.GameId == GameId.PathOfExile2 ? Poe2MaxZoomFactor : MaxZoomFactor;
        _scale = Math.Clamp(_scale * factor, _fitScale * MinZoomFactor, _fitScale * maxZoom);
        _offsetX = p.X - txBefore * _scale;
        _offsetY = p.Y - tyBefore * _scale;
        InvalidateVisual();
        e.Handled = true;
    }

    private void BeginPan(PointerPressedEventArgs e, Point pointerPosition)
    {
        _panning = true;
        _panMoved = false;
        _panStartScreen = pointerPosition;
        _panStartOffX = _offsetX;
        _panStartOffY = _offsetY;
        e.Pointer.Capture(this);
    }

    private void UpdatePan(Point pointerPosition)
    {
        _offsetX = _panStartOffX + (pointerPosition.X - _panStartScreen.X);
        _offsetY = _panStartOffY + (pointerPosition.Y - _panStartScreen.Y);
        var ddx = pointerPosition.X - _panStartScreen.X;
        var ddy = pointerPosition.Y - _panStartScreen.Y;
        if (ddx * ddx + ddy * ddy > 16)
        {
            _panMoved = true;
        }
        InvalidateVisual();
    }

    private void EndPan(PointerReleasedEventArgs e)
    {
        _panning = false;
        e.Pointer.Capture(null);
        if (_panMoved)
        {
            return;
        }

        var hit = HitTest(e.GetPosition(this));
        if (hit is not { } id)
        {
            return;
        }

        if (!_vm.IsAllocated(id) && !_vm.HoverPath.IsEmpty)
        {
            _vm.AllocatePath();
        }
        else
        {
            _vm.ToggleNode(id);
        }
    }

    private bool ShowClusterContextMenu(int socketId, Point pointerPosition)
    {
        _vm.SetHover(socketId);
        var socket = _vm.HoverNodeId == socketId ? _vm.HoverNode : null;
        if (socket?.Type != NodeType.JewelSocket)
        {
            return false;
        }

        var allowedSizes = _vm.AllowedClusterSizes(socketId);
        if (allowedSizes.Count == 0)
        {
            return false;
        }

        var items = new List<MenuItem>();
        if (_vm.HasClusterAt(socketId))
        {
            items.Add(new MenuItem
            {
                Header = "Replace Cluster",
                ItemsSource = BuildSizeMenuItems(socketId, allowedSizes, replacePrefix: true),
            });
            var removeItem = new MenuItem { Header = "Remove Cluster" };
            removeItem.Click += (_, _) => _vm.RemoveCluster(socketId);
            items.Add(removeItem);
        }
        else
        {
            items.AddRange(BuildSizeMenuItems(socketId, allowedSizes, replacePrefix: false));
        }

        if (items.Count == 0)
        {
            return false;
        }

        _clusterMenu?.Close();
        _clusterMenu = new ContextMenu
        {
            Placement = PlacementMode.Custom,
            PlacementTarget = this,
            CustomPopupPlacementCallback = placement =>
            {
                placement.AnchorRectangle = new Rect(pointerPosition, new Size(1, 1));
                placement.Anchor = PopupAnchor.TopLeft;
                placement.Gravity = PopupGravity.BottomRight;
                placement.ConstraintAdjustment =
                    PopupPositionerConstraintAdjustment.SlideX |
                    PopupPositionerConstraintAdjustment.SlideY |
                    PopupPositionerConstraintAdjustment.FlipX |
                    PopupPositionerConstraintAdjustment.FlipY;
            },
            ItemsSource = items,
        };
        _clusterMenu.Closed += (_, _) =>
        {
            _clusterMenu = null;
        };
        _clusterMenu.Open(this);
        return true;
    }

    private IReadOnlyList<MenuItem> BuildSizeMenuItems(int socketId, IReadOnlyList<ClusterJewelSize> sizes, bool replacePrefix)
    {
        var items = new List<MenuItem>(sizes.Count);
        foreach (var size in sizes)
        {
            var label = replacePrefix ? $"Replace with {size} Cluster" : $"Insert {size} Cluster";
            items.Add(new MenuItem
            {
                Header = label,
                ItemsSource = BuildPassiveCountMenuItems(socketId, size),
            });
        }
        return items;
    }

    private IReadOnlyList<MenuItem> BuildPassiveCountMenuItems(int socketId, ClusterJewelSize size)
    {
        var passiveCounts = _vm.ManualPassiveCounts(size);
        var items = new List<MenuItem>(passiveCounts.Count);
        foreach (var passiveCount in passiveCounts)
        {
            items.Add(new MenuItem
            {
                Header = $"{passiveCount} passives",
                ItemsSource = BuildNotableCountMenuItems(socketId, size, passiveCount),
            });
        }
        return items;
    }

    private IReadOnlyList<MenuItem> BuildNotableCountMenuItems(int socketId, ClusterJewelSize size, int passiveCount)
    {
        var notableCounts = _vm.ManualNotableCounts(size, passiveCount);
        var items = new List<MenuItem>(notableCounts.Count);
        foreach (var notableCount in notableCounts)
        {
            var item = new MenuItem { Header = $"{notableCount} notables" };
            item.Click += (_, _) => _vm.InsertCluster(socketId, size, passiveCount, notableCount);
            items.Add(item);
        }
        return items;
    }
}
