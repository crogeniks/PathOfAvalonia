using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp;

public sealed class PassiveTreeView : Control
{
    private readonly TreeModel _tree;
    private readonly PassiveSpec _spec;

    // View transform (tree-space → screen-space): screen = tree * scale + offset
    private double _scale = 0.05;
    private double _offsetX, _offsetY;
    private bool _viewInitialised;
    private double _fitScale = 0.05;     // scale where the whole tree just fits the viewport
    private const double MinZoomFactor = 0.9;  // can't shrink below fit
    private const double MaxZoomFactor = 10.0; // can zoom 10× past fit to inspect a single cluster

    // Pan state
    private bool _panning;
    private Point _panStartScreen;
    private double _panStartOffX, _panStartOffY;
    private bool _panMoved;

    private int? _hoverNodeId;

    private readonly Bitmap? _bgTile;
    private const double BgTileScreen = 98; // tile size in screen-px (matches PoB asset, no zoom scaling)

    // Tree-space sizes: scaled by _scale into screen-px each frame so they all
    // shrink together when the user zooms out.
    private const double NodeRadius = 45;
    private const double HitRadius = 75;
    private const double ConnectorThicknessTree = 18;

    // Cached brushes
    private static readonly IBrush BgBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x18));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x60));
    private static readonly IBrush AllocatedBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xc8, 0x4a));
    private static readonly IBrush NotableBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x90));
    private static readonly IBrush KeystoneBrush = new SolidColorBrush(Color.FromRgb(0xc0, 0x80, 0x40));
    private static readonly IBrush MasteryBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x80, 0xc0));
    private static readonly IBrush JewelBrush = new SolidColorBrush(Color.FromRgb(0xc0, 0xc0, 0x40));
    private static readonly IBrush ConnectorBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x48));
    // Node outlines stay in screen-px so nodes always have a crisp border at any zoom.
    private static readonly IPen NodeOutlinePen = new Pen(Brushes.Black, 1.5);
    private static readonly IPen HoverOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff)), 2.5);

    public PassiveTreeView(TreeModel tree, PassiveSpec spec)
    {
        _tree = tree;
        _spec = spec;
        ClipToBounds = true;
        Focusable = true;
        _spec.SpecChanged += () => InvalidateVisual();
        _bgTile = TryLoadBackground(tree.Version);
    }

    private static Bitmap? TryLoadBackground(string version)
    {
        var name = $"background_{version.Replace('.', '_')}.png";
        var uri = new Uri($"avares://PathOfAvalonia.TreeApp/Assets/{name}");
        try
        {
            using var s = AssetLoader.Open(uri);
            return new Bitmap(s);
        }
        catch
        {
            return null;
        }
    }

    public int? HoverNodeId => _hoverNodeId;
    public Node? HoverNode => _hoverNodeId is { } id && _tree.Nodes.TryGetValue(id, out var n) ? n : null;
    public event Action? HoverChanged;

    private void EnsureViewInitialised()
    {
        if (_viewInitialised)
        {
            return;
        }
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var b = _tree.Bounds;
        var sx = Bounds.Width / b.Width;
        var sy = Bounds.Height / b.Height;
        _fitScale = Math.Min(sx, sy) * 0.95;
        _scale = _fitScale;
        _offsetX = Bounds.Width * 0.5 - b.CenterX * _scale;
        _offsetY = Bounds.Height * 0.5 - b.CenterY * _scale;
        _viewInitialised = true;
    }

    private Point TreeToScreen(double tx, double ty) =>
        new(tx * _scale + _offsetX, ty * _scale + _offsetY);

    private (double tx, double ty) ScreenToTree(Point p) =>
        ((p.X - _offsetX) / _scale, (p.Y - _offsetY) / _scale);

    public override void Render(DrawingContext ctx)
    {
        EnsureViewInitialised();
        ctx.FillRectangle(BgBrush, new Rect(Bounds.Size));
        DrawBackgroundTile(ctx);

        // Draw connectors. Pen thickness is in tree-space so it scales with zoom.
        var allocated = _spec.AllocatedNodes;
        var connThick = Math.Max(0.5, ConnectorThicknessTree * _scale);
        var connPen = new Pen(ConnectorBrush, connThick);
        var connActivePen = new Pen(AllocatedBrush, connThick);
        foreach (var c in _tree.Connectors)
        {
            var pen = (allocated.Contains(c.FromId) && allocated.Contains(c.ToId))
                ? connActivePen
                : connPen;
            switch (c)
            {
                case LineConnector lc:
                    ctx.DrawLine(pen, TreeToScreen(lc.X1, lc.Y1), TreeToScreen(lc.X2, lc.Y2));
                    break;
                case ArcConnector ac:
                    DrawArc(ctx, pen, ac);
                    break;
            }
        }

        DrawNodesAndHud(ctx);
    }

    private void DrawBackgroundTile(DrawingContext ctx)
    {
        if (_bgTile is null)
        {
            return;
        }
        // Anchor the tile pattern to tree-space (0,0) so it shifts with pan/zoom.
        // Modulo keeps the destination rect close to the visible area; Tile mode
        // fills outward from there.
        var dx = ((_offsetX % BgTileScreen) + BgTileScreen) % BgTileScreen - BgTileScreen;
        var dy = ((_offsetY % BgTileScreen) + BgTileScreen) % BgTileScreen - BgTileScreen;
        var brush = new ImageBrush(_bgTile)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(dx, dy, BgTileScreen, BgTileScreen, RelativeUnit.Absolute),
        };
        ctx.FillRectangle(brush, new Rect(Bounds.Size));
    }

    private void DrawArc(DrawingContext ctx, IPen pen, ArcConnector ac)
    {
        // Endpoints in tree-space, then mapped to screen.
        var a0 = ac.StartAngle;
        var a1 = ac.StartAngle + ac.SweepAngle;
        var p0 = TreeToScreen(ac.Cx + Math.Sin(a0) * ac.Radius, ac.Cy - Math.Cos(a0) * ac.Radius);
        var p1 = TreeToScreen(ac.Cx + Math.Sin(a1) * ac.Radius, ac.Cy - Math.Cos(a1) * ac.Radius);
        var rScreen = ac.Radius * _scale;
        var isLargeArc = Math.Abs(ac.SweepAngle) > Math.PI;
        // Increasing PoB angle = clockwise in screen space (Y-down).
        var sweepDir = ac.SweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(p0, isFilled: false);
            g.ArcTo(p1, new Size(rScreen, rScreen), 0, isLargeArc, sweepDir);
            g.EndFigure(isClosed: false);
        }
        ctx.DrawGeometry(brush: null, pen: pen, geometry: geo);
    }

    private void DrawNodesAndHud(DrawingContext ctx)
    {
        var allocated = _spec.AllocatedNodes;
        // Draw nodes
        var r = NodeRadius * _scale;
        foreach (var n in _tree.Nodes.Values)
        {
            if (n.Type == NodeType.Proxy)
            {
                continue;
            }
            var alloc = allocated.Contains(n.Id);
            var hover = _hoverNodeId == n.Id;
            var brush = alloc
                ? AllocatedBrush
                : n.Type switch
                {
                    NodeType.Keystone => KeystoneBrush,
                    NodeType.Notable => NotableBrush,
                    NodeType.Mastery => MasteryBrush,
                    NodeType.JewelSocket => JewelBrush,
                    NodeType.ClassStart => AllocatedBrush,
                    NodeType.AscendancyStart => AllocatedBrush,
                    _ => NormalBrush,
                };

            var nodeR = n.Type switch
            {
                NodeType.Keystone => r * 2.0,
                NodeType.Notable => r * 1.5,
                NodeType.JewelSocket => r * 1.7,
                NodeType.Mastery => r * 1.2,
                NodeType.ClassStart => r * 2.2,
                _ => r,
            };
            var screen = TreeToScreen(n.X, n.Y);
            ctx.DrawEllipse(brush, hover ? HoverOutlinePen : NodeOutlinePen, screen, nodeR, nodeR);
        }

        // HUD: alloc count + hover label
        var hud = $"v{_tree.Version} • allocated: {allocated.Count}";
        if (HoverNode is { } hn)
        {
            hud += $"  •  hover: {hn.Name} [{hn.Type}]";
        }
        var ft = new FormattedText(hud, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.White);
        ctx.DrawText(ft, new Point(8, 8));
    }

    private int? HitTest(Point screen)
    {
        var (tx, ty) = ScreenToTree(screen);
        var best = HitRadius * HitRadius;
        int? bestId = null;
        foreach (var n in _tree.Nodes.Values)
        {
            if (n.Type == NodeType.Proxy)
            {
                continue;
            }
            var dx = tx - n.X;
            var dy = ty - n.Y;
            var d = dx * dx + dy * dy;
            // Slightly bigger hit radius for big nodes — quick & cheap.
            var rsq = n.Type switch
            {
                NodeType.Keystone => HitRadius * HitRadius * 4,
                NodeType.Notable => HitRadius * HitRadius * 2,
                NodeType.JewelSocket => HitRadius * HitRadius * 2.5,
                _ => HitRadius * HitRadius,
            };
            if (d < rsq && d < best)
            {
                best = d;
                bestId = n.Id;
            }
        }
        return bestId;
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        if (_panning)
        {
            _offsetX = _panStartOffX + (p.X - _panStartScreen.X);
            _offsetY = _panStartOffY + (p.Y - _panStartScreen.Y);
            var ddx = p.X - _panStartScreen.X;
            var ddy = p.Y - _panStartScreen.Y;
            if (ddx * ddx + ddy * ddy > 16)
            {
                _panMoved = true;
            }
            InvalidateVisual();
            return;
        }

        var hit = HitTest(p);
        if (hit != _hoverNodeId)
        {
            _hoverNodeId = hit;
            HoverChanged?.Invoke();
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        var p = e.GetPosition(this);
        if (props.IsLeftButtonPressed)
        {
            _panning = true;
            _panMoved = false;
            _panStartScreen = p;
            _panStartOffX = _offsetX;
            _panStartOffY = _offsetY;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_panning)
        {
            _panning = false;
            e.Pointer.Capture(null);
            if (!_panMoved)
            {
                var hit = HitTest(e.GetPosition(this));
                if (hit is { } id)
                {
                    _spec.Toggle(id);
                }
            }
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var p = e.GetPosition(this);
        var (txBefore, tyBefore) = ScreenToTree(p);
        var factor = Math.Pow(1.2, e.Delta.Y);
        _scale = Math.Clamp(_scale * factor, _fitScale * MinZoomFactor, _fitScale * MaxZoomFactor);
        // Keep cursor anchored.
        _offsetX = p.X - txBefore * _scale;
        _offsetY = p.Y - tyBefore * _scale;
        InvalidateVisual();
        e.Handled = true;
    }
}
