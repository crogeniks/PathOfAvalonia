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
    private readonly SpriteMap _sprites;
    // One Bitmap per unique atlas filename (multiple atlas keys share a file).
    private readonly Dictionary<string, Bitmap> _atlasBitmaps = new();

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
    private HoverPath _hoverPath = HoverPath.Empty;
    private HashSet<int> _hoverPathNodes = new();

    private readonly Bitmap? _bgTile;
    private const double BgTileScreen = 98; // tile size in screen-px (matches PoB asset, no zoom scaling)

    // Tree-space sizes: scaled by _scale into screen-px each frame so they all
    // shrink together when the user zooms out.
    private const double NodeRadius = 45;
    private const double ConnectorThicknessTree = 18;
    // PoB draws each sprite at (atlas_px * SpriteDisplayScale) tree-units per half-dimension
    // (`DrawAsset` in PassiveTreeView.lua uses data.width * scale * 1.33, rect spans 2x).
    private const double SpriteDisplayScale = 1.33;

    // Hit-test radii (tree-units) per node type. From PoB's nodeOverlay.artWidth * 1.33
    // (PassiveTree.lua:387–438). Squared here to skip the sqrt in the loop.
    private static readonly double HitRsqNormal   = Sq(40 * SpriteDisplayScale);
    private static readonly double HitRsqNotable  = Sq(58 * SpriteDisplayScale);
    private static readonly double HitRsqKeystone = Sq(84 * SpriteDisplayScale);
    private static readonly double HitRsqSocket   = Sq(58 * SpriteDisplayScale);
    private static readonly double HitRsqMastery  = Sq(65 * SpriteDisplayScale);
    private static double Sq(double x) => x * x;

    // Cached brushes
    private static readonly IBrush BgBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x18));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x60));
    private static readonly IBrush AllocatedBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xc8, 0x4a));
    private static readonly IBrush ConnectorBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x48));
    private static readonly IBrush HoverPathBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xff, 0xc8, 0x4a));
    private static readonly IPen NodeOutlinePen = new Pen(Brushes.Black, 1.5);

    public PassiveTreeView(TreeModel tree, PassiveSpec spec, SpriteMap sprites)
    {
        _tree = tree;
        _spec = spec;
        _sprites = sprites;
        ClipToBounds = true;
        Focusable = true;
        _spec.SpecChanged += () =>
        {
            if (_hoverNodeId is { } id)
            {
                _hoverPath = _spec.HoverPathTo(id);
                _hoverPathNodes = new HashSet<int>(_hoverPath.Nodes);
            }
            InvalidateVisual();
        };
        _bgTile = TryLoadBackground(tree.Version);
        LoadAtlasBitmaps();
    }

    private void LoadAtlasBitmaps()
    {
        foreach (var atlas in _sprites.Atlases.Values)
        {
            if (_atlasBitmaps.ContainsKey(atlas.File))
            {
                continue;
            }
            var uri = new Uri($"avares://PathOfAvalonia.TreeApp/Assets/{atlas.File}");
            try
            {
                using var s = AssetLoader.Open(uri);
                _atlasBitmaps[atlas.File] = new Bitmap(s);
            }
            catch
            {
                // Missing atlas (e.g. ascendancy WebP on a build without WebP codec)
                // is non-fatal — affected nodes will simply skip their art.
            }
        }
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
        var connHoverPen = new Pen(HoverPathBrush, connThick);
        var pathEdges = _hoverPath.Edges;
        foreach (var c in _tree.Connectors)
        {
            var key = (Math.Min(c.FromId, c.ToId), Math.Max(c.FromId, c.ToId));
            IPen pen;
            if (allocated.Contains(c.FromId) && allocated.Contains(c.ToId))
            {
                pen = connActivePen;
            }
            else if (pathEdges.Contains(key))
            {
                pen = connHoverPen;
            }
            else
            {
                pen = connPen;
            }
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
        foreach (var n in _tree.Nodes.Values)
        {
            if (n.Type == NodeType.Proxy)
            {
                continue;
            }
            var isHover = _hoverNodeId == n.Id;
            var onPath = _hoverPathNodes.Contains(n.Id);
            DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath);
        }

        // HUD: alloc count + hover label
        var hud = $"v{_tree.Version} • allocated: {allocated.Count}";
        if (HoverNode is { } hn)
        {
            hud += $"  •  hover: {hn.Name} [{hn.Type}]";
            if (hn.Type == NodeType.Mastery && allocated.Contains(hn.Id)
                && _spec.SelectedMasteryEffect(hn.Id) is { Stats.Count: > 0 } eff)
            {
                hud += $"\n    selected: {string.Join(" | ", eff.Stats)}";
            }
        }
        var ft = new FormattedText(hud, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.White);
        ctx.DrawText(ft, new Point(8, 8));
    }

    private void DrawNode(DrawingContext ctx, Node n, bool alloc, bool hover)
    {
        var screen = TreeToScreen(n.X, n.Y);
        // Icon: skills atlas (normal/notable/keystone) or mastery atlas.
        var (iconAtlas, iconPath) = IconSprite(n, alloc, hover: false);
        if (iconAtlas is not null && iconPath is not null)
        {
            DrawSprite(ctx, iconAtlas, iconPath, screen);
        }
        // Hover overlay: for masteries, the masteryConnected sprite glows on top of
        // the base icon rather than replacing it.
        if (hover && !alloc && n.Type == NodeType.Mastery && n.InactiveIcon is { } ii)
        {
            DrawSprite(ctx, "masteryConnected", ii, screen);
        }
        // Frame: ornate border. Mastery has no separate frame (baked in).
        var frameKey = FrameKey(n.Type, alloc, hover);
        if (frameKey is not null)
        {
            DrawSprite(ctx, "frame", frameKey, screen);
        }
        // Fallback for node types we haven't wired art for yet (e.g. class start),
        // so they don't disappear.
        if (iconAtlas is null && frameKey is null)
        {
            var r = NodeRadius * _scale;
            ctx.DrawEllipse(alloc ? AllocatedBrush : NormalBrush, NodeOutlinePen, screen, r, r);
        }
    }

    private void DrawSprite(DrawingContext ctx, string atlasKey, string spriteKey, Point centre)
    {
        if (!_sprites.Atlases.TryGetValue(atlasKey, out var atlas))
        {
            return;
        }
        if (!atlas.Coords.TryGetValue(spriteKey, out var rect))
        {
            return;
        }
        if (!_atlasBitmaps.TryGetValue(atlas.File, out var bmp))
        {
            return;
        }
        var halfW = rect.W * SpriteDisplayScale * _scale;
        var halfH = rect.H * SpriteDisplayScale * _scale;
        var dst = new Rect(centre.X - halfW, centre.Y - halfH, halfW * 2, halfH * 2);
        var src = new Rect(rect.X, rect.Y, rect.W, rect.H);
        ctx.DrawImage(bmp, src, dst);
    }

    private static (string? atlas, string? path) IconSprite(Node n, bool alloc, bool hover) => n.Type switch
    {
        NodeType.Normal => (alloc ? "normalActive" : "normalInactive", n.Icon),
        NodeType.Notable => (alloc ? "notableActive" : "notableInactive", n.Icon),
        NodeType.Keystone => (alloc ? "keystoneActive" : "keystoneInactive", n.Icon),
        // Clickable masteries (those with InactiveIcon/ActiveIcon) live in dedicated
        // per-state atlases. The plain `mastery` sheet only carries static cluster
        // decorations (no effect — e.g. Atlas Tree charm masteries).
        NodeType.Mastery when alloc && n.ActiveIcon is not null => ("masteryActiveSelected", n.ActiveIcon),
        NodeType.Mastery when n.InactiveIcon is not null => ("masteryInactive", n.InactiveIcon),
        NodeType.Mastery => ("mastery", n.Icon),
        _ => (null, null),
    };

    private static string? FrameKey(NodeType type, bool alloc, bool hover) => type switch
    {
        NodeType.Normal => alloc ? "PSSkillFrameActive" : hover ? "PSSkillFrameHighlighted" : "PSSkillFrame",
        NodeType.Notable => alloc ? "NotableFrameAllocated" : hover ? "NotableFrameCanAllocate" : "NotableFrameUnallocated",
        NodeType.Keystone => alloc ? "KeystoneFrameAllocated" : hover ? "KeystoneFrameCanAllocate" : "KeystoneFrameUnallocated",
        NodeType.JewelSocket => alloc ? "JewelFrameAllocated" : hover ? "JewelFrameCanAllocate" : "JewelFrameUnallocated",
        _ => null,
    };

    private int? HitTest(Point screen)
    {
        var (tx, ty) = ScreenToTree(screen);
        var best = double.MaxValue;
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
            _hoverPath = hit is { } id ? _spec.HoverPathTo(id) : HoverPath.Empty;
            _hoverPathNodes = new HashSet<int>(_hoverPath.Nodes);
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
                    if (!_spec.IsAllocated(id) && !_hoverPath.IsEmpty)
                    {
                        _spec.AllocateMany(_hoverPath.Nodes);
                        _hoverPath = HoverPath.Empty;
                        _hoverPathNodes.Clear();
                    }
                    else
                    {
                        _spec.Toggle(id);
                    }
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
