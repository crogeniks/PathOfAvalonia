using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;

namespace PathOfAvalonia.TreeApp;

public sealed class PassiveTreeView : Control
{
    private readonly PassiveTreeViewModel _vm;
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

    // Cached brushes / pens
    private static readonly IBrush BgBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x18));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x60));
    private static readonly IBrush AllocatedBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xc8, 0x4a));
    private static readonly IBrush ConnectorBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x48));
    private static readonly IBrush HoverPathBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xff, 0xc8, 0x4a));
    private static readonly IPen NodeOutlinePen = new Pen(Brushes.Black, 1.5);
    // Cluster background disc layers (programmatic stand-in for the missing art asset).
    // Colors approximate the PoB golden-medallion aesthetic.
    private static readonly IBrush ClusterDiscFillBrush   = new SolidColorBrush(Color.FromArgb(0xD8, 0x12, 0x14, 0x1C));
    private static readonly IBrush ClusterDiscBorderBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x68, 0x3E));
    private static readonly IBrush ClusterDiscRing1Brush  = new SolidColorBrush(Color.FromRgb(0x5E, 0x52, 0x32));
    private static readonly IBrush ClusterDiscRing2Brush  = new SolidColorBrush(Color.FromRgb(0x42, 0x3C, 0x24));
    private static readonly IBrush ClusterOrbitLineBrush  = new SolidColorBrush(Color.FromArgb(0x60, 0x90, 0x82, 0x56));

    public PassiveTreeView(PassiveTreeViewModel vm, SpriteMap sprites)
    {
        _vm = vm;
        _sprites = sprites;
        ClipToBounds = true;
        Focusable = true;
        _vm.RedrawRequested += InvalidateVisual;
        _bgTile = TryLoadBackground(_vm.Tree.Version);
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
        var b = _vm.Tree.Bounds;
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

        var activeClusters = _vm.ActiveClusters;

        // Cluster background discs, drawn under connectors and nodes.
        // We don't have the actual medallion art asset, so we approximate it with
        // concentric golden rings on a dark fill.
        // The disc is centred on the ring centre (offset outward from the socket).
        foreach (var sub in activeClusters.Values)
        {
            var centre = TreeToScreen(sub.ClusterCenterX, sub.ClusterCenterY);
            var r = sub.CircleRadius * _scale;

            // Proportional thicknesses so the disc looks reasonable at any zoom.
            var borderThick = Math.Max(1.5, r * 0.10);
            var ringThick1  = Math.Max(1.0, r * 0.04);
            var ringThick2  = Math.Max(0.5, r * 0.025);

            // Dark fill + outer golden border (the "edge" of the medallion).
            ctx.DrawEllipse(ClusterDiscFillBrush,
                new Pen(ClusterDiscBorderBrush, borderThick),
                centre, r * 0.88, r * 0.88);

            // First inner decorative ring.
            ctx.DrawEllipse(null,
                new Pen(ClusterDiscRing1Brush, ringThick1),
                centre, r * 0.66, r * 0.66);

            // Second inner ring (tighter).
            ctx.DrawEllipse(null,
                new Pen(ClusterDiscRing2Brush, ringThick2),
                centre, r * 0.46, r * 0.46);

            // Orbit ring at the node positions (semi-transparent, scales like connectors).
            var orbitThick = Math.Max(0.5, ConnectorThicknessTree * _scale * 0.5);
            ctx.DrawEllipse(null,
                new Pen(ClusterOrbitLineBrush, orbitThick),
                centre, r, r);
        }

        // Draw connectors: base tree first, then cluster subgraph connectors.
        var allocated = _vm.AllocatedNodes;
        var connThick = Math.Max(0.5, ConnectorThicknessTree * _scale);
        var connPen = new Pen(ConnectorBrush, connThick);
        var connActivePen = new Pen(AllocatedBrush, connThick);
        var connHoverPen = new Pen(HoverPathBrush, connThick);
        var pathEdges = _vm.HoverPath.Edges;

        IEnumerable<Connector> allConnectors = _vm.Tree.Connectors;
        foreach (var sub in activeClusters.Values)
        {
            allConnectors = allConnectors.Concat(sub.Connectors);
        }

        foreach (var c in allConnectors)
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
        var allocated = _vm.AllocatedNodes;

        // Base-tree nodes (skip proxies; skip jewel sockets that have an active cluster —
        // the cluster ring's slot 0 sits at the socket position and replaces it visually).
        foreach (var n in _vm.Tree.Nodes.Values)
        {
            if (n.Type == NodeType.Proxy)
            {
                continue;
            }
            if (n.Type == NodeType.JewelSocket && _vm.ActiveClusters.ContainsKey(n.Id))
            {
                continue;
            }
            var isHover = _vm.HoverNodeId == n.Id;
            var onPath = _vm.HoverPathNodes.Contains(n.Id);
            DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath);
        }

        // Cluster subgraph nodes (only rendered when the jewel is active).
        foreach (var sub in _vm.ActiveClusters.Values)
        {
            foreach (var n in sub.Nodes)
            {
                var isHover = _vm.HoverNodeId == n.Id;
                var onPath = _vm.HoverPathNodes.Contains(n.Id);
                DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath);
            }
        }

        // HUD: alloc count + hover label
        var hud = $"v{_vm.Tree.Version} • allocated: {allocated.Count}";
        if (_vm.HoverNode is { } hn)
        {
            hud += $"  •  hover: {hn.Name} [{hn.Type}]";
            if (hn.Type == NodeType.Mastery && allocated.Contains(hn.Id)
                && _vm.SelectedMasteryEffect(hn.Id) is { Stats.Count: > 0 } eff)
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
        // For JewelSocket nodes, check whether a cluster is active to pick the right sprite.
        var clusterSize = n.Type == NodeType.JewelSocket ? _vm.ClusterSizeAt(n.Id) : null;
        var frameKey = FrameKey(n.Type, alloc, hover, clusterSize);
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

    private static string? FrameKey(NodeType type, bool alloc, bool hover,
        ClusterJewelSize? clusterSize = null) => type switch
    {
        NodeType.Normal   => alloc ? "PSSkillFrameActive" : hover ? "PSSkillFrameHighlighted" : "PSSkillFrame",
        NodeType.Notable  => alloc ? "NotableFrameAllocated" : hover ? "NotableFrameCanAllocate" : "NotableFrameUnallocated",
        NodeType.Keystone => alloc ? "KeystoneFrameAllocated" : hover ? "KeystoneFrameCanAllocate" : "KeystoneFrameUnallocated",
        // Cluster socket: use size-specific sprites when a cluster jewel is active.
        // There is no cluster-specific active sprite, so allocated falls back to the base jewel frame.
        NodeType.JewelSocket when clusterSize is { } size =>
            alloc ? "JewelFrameAllocated" :
            hover ? $"JewelSocketClusterAltCanAllocate1{SizeLabel(size)}" :
                    $"JewelSocketClusterAltNormal1{SizeLabel(size)}",
        NodeType.JewelSocket => alloc ? "JewelFrameAllocated" : hover ? "JewelFrameCanAllocate" : "JewelFrameUnallocated",
        _ => null,
    };

    private static string SizeLabel(ClusterJewelSize size) => size switch
    {
        ClusterJewelSize.Large  => "Large",
        ClusterJewelSize.Medium => "Medium",
        _                       => "Small",
    };

    private int? HitTest(Point screen)
    {
        var (tx, ty) = ScreenToTree(screen);
        var best = double.MaxValue;
        int? bestId = null;

        // Base-tree nodes (same exclusions as drawing: skip proxy, skip cluster-replaced sockets).
        foreach (var n in _vm.Tree.Nodes.Values)
        {
            if (n.Type == NodeType.Proxy)
            {
                continue;
            }
            if (n.Type == NodeType.JewelSocket && _vm.ActiveClusters.ContainsKey(n.Id))
            {
                continue;
            }
            CheckNode(n);
        }

        // Cluster subgraph nodes.
        foreach (var sub in _vm.ActiveClusters.Values)
        {
            foreach (var n in sub.Nodes)
            {
                CheckNode(n);
            }
        }

        return bestId;

        void CheckNode(Node n)
        {
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
        if (hit != _vm.HoverNodeId)
        {
            _vm.SetHover(hit); // fires RedrawRequested → InvalidateVisual
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
                    if (!_vm.IsAllocated(id) && !_vm.HoverPath.IsEmpty)
                    {
                        _vm.AllocatePath(); // SpecChanged → RedrawRequested → InvalidateVisual
                    }
                    else
                    {
                        _vm.ToggleNode(id); // SpecChanged → RedrawRequested → InvalidateVisual
                    }
                }
            }
            return;
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
