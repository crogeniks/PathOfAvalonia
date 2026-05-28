using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeApp.Controls;

public sealed partial class PassiveTreeView : Control
{
    private readonly PassiveTreeViewModel _vm;
    private readonly SpriteMap _sprites;
    private readonly ITreeImageAssetResolver _assetResolver;
    private ContextMenu? _clusterMenu;
    // One Bitmap per unique atlas filename (multiple atlas keys share a file).
    private readonly Dictionary<string, Bitmap> _atlasBitmaps = new();
    private readonly Dictionary<string, Bitmap?> _jewelRadiusBitmaps = new();

    // View transform (tree-space → screen-space): screen = tree * scale + offset
    private double _scale = 0.05;
    private double _offsetX, _offsetY;
    private bool _viewInitialised;
    private double _fitScale = 0.05;     // scale where the whole tree just fits the viewport
    private const double MinZoomFactor = 0.9;  // can't shrink below fit
    private const double MaxZoomFactor = 10.0; // can zoom 10× past fit to inspect a single cluster
    private const double Poe2MaxZoomFactor = 40.0;

    // Pan state
    private bool _panning;
    private Point _panStartScreen;
    private Point _lastPointerPosition;
    private Point _lastTooltipRedrawPosition;
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
    private static readonly double HitRsqNormal = Sq(40 * SpriteDisplayScale);
    private static readonly double HitRsqNotable = Sq(58 * SpriteDisplayScale);
    private static readonly double HitRsqKeystone = Sq(84 * SpriteDisplayScale);
    private static readonly double HitRsqSocket = Sq(58 * SpriteDisplayScale);
    private static readonly double HitRsqMastery = Sq(65 * SpriteDisplayScale);
    private static readonly double HitMaxRadius = 90 * SpriteDisplayScale;
    private static double Sq(double x) => x * x;
    private static double DistanceSquared(Point a, Point b) => Sq(a.X - b.X) + Sq(a.Y - b.Y);

    // Cached brushes / pens
    private static readonly IBrush BgBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x18));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x60));
    private static readonly IBrush AllocatedBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xc8, 0x4a));
    private static readonly IBrush WeaponSet1Brush = new SolidColorBrush(Color.FromRgb(0x35, 0xD4, 0xFF));
    private static readonly IBrush WeaponSet2Brush = new SolidColorBrush(Color.FromRgb(0x66, 0xE3, 0x78));
    private static readonly IBrush TooltipFillBrush = new SolidColorBrush(Color.FromArgb(0xEE, 0x06, 0x08, 0x0B));
    private static readonly IBrush TooltipHeaderBrush = new SolidColorBrush(Color.FromArgb(0xEE, 0x39, 0x2B, 0x16));
    private static readonly IBrush TooltipBorderBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x76, 0x22));
    private static readonly IBrush TooltipTitleBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xDF, 0xC4));
    private static readonly IBrush TooltipStatBrush = new SolidColorBrush(Color.FromRgb(0x8D, 0x98, 0xFF));
    private static readonly IBrush TooltipReminderBrush = new SolidColorBrush(Color.FromRgb(0xA6, 0xB1, 0xA4));
    private static readonly IBrush TooltipFlavourBrush = new SolidColorBrush(Color.FromRgb(0xD2, 0x84, 0x2E));
    private static readonly IBrush ConnectorBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x48));
    private static readonly IBrush HoverPathBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xff, 0xc8, 0x4a));
    private static readonly IBrush Poe2NormalFrameBrush = new SolidColorBrush(Color.FromArgb(0xE0, 0x4E, 0x46, 0x35));
    private static readonly IBrush Poe2NotableFrameBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0x8C, 0x75, 0x42));
    private static readonly IBrush Poe2SocketFrameBrush = new SolidColorBrush(Color.FromArgb(0xE0, 0x70, 0x82, 0x28));
    private static readonly IBrush Poe2HoverFrameBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0xD8, 0xC0, 0x78));
    private static readonly IBrush Poe2AllocatedFrameBrush = new SolidColorBrush(Color.FromArgb(0xF0, 0xE0, 0xB8, 0x58));
    private static readonly IBrush Poe2SocketFillBrush = new SolidColorBrush(Color.FromArgb(0xD0, 0x05, 0x06, 0x05));
    private static readonly IBrush DiffAddedBrush = new SolidColorBrush(Color.FromArgb(0xF5, 0x00, 0xF0, 0x5A));
    private static readonly IBrush DiffChangedBrush = new SolidColorBrush(Color.FromArgb(0x95, 0xF0, 0xC8, 0x4A));
    private static readonly IBrush DiffRemovedBrush = new SolidColorBrush(Color.FromArgb(0x95, 0xE5, 0x56, 0x56));
    private static readonly IPen NodeOutlinePen = new Pen(Brushes.Black, 1.5);
    // Cluster background disc layers (programmatic stand-in for the missing art asset).
    // Colors approximate the PoB golden-medallion aesthetic.
    private static readonly IBrush ClusterDiscFillBrush = new SolidColorBrush(Color.FromArgb(0xD8, 0x12, 0x14, 0x1C));
    private static readonly IBrush ClusterDiscBorderBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x68, 0x3E));
    private static readonly IBrush ClusterDiscRing1Brush = new SolidColorBrush(Color.FromRgb(0x5E, 0x52, 0x32));
    private static readonly IBrush ClusterDiscRing2Brush = new SolidColorBrush(Color.FromRgb(0x42, 0x3C, 0x24));
    private static readonly IBrush ClusterOrbitLineBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x90, 0x82, 0x56));

    public PassiveTreeView(PassiveTreeViewModel vm, SpriteMap sprites, ITreeImageAssetResolver assetResolver)
    {
        _vm = vm;
        _sprites = sprites;
        _assetResolver = assetResolver;
        ClipToBounds = true;
        Focusable = true;
        _vm.RedrawRequested += InvalidateVisual;
        _bgTile = _assetResolver.LoadBackground(_vm.Tree.Version);
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
            if (_assetResolver.LoadBitmap(atlas.File) is { } bitmap)
            {
                _atlasBitmaps[atlas.File] = bitmap;
            }
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

    private Rect VisibleTreeRect(double paddingTree = 180)
    {
        var (left, top) = ScreenToTree(new Point(0, 0));
        var (right, bottom) = ScreenToTree(new Point(Bounds.Width, Bounds.Height));
        var x = Math.Min(left, right) - paddingTree;
        var y = Math.Min(top, bottom) - paddingTree;
        return new Rect(
            x,
            y,
            Math.Abs(right - left) + paddingTree * 2,
            Math.Abs(bottom - top) + paddingTree * 2);
    }

    public override void Render(DrawingContext ctx)
    {
        EnsureViewInitialised();
        ctx.FillRectangle(BgBrush, new Rect(Bounds.Size));
        DrawBackgroundTile(ctx);

        var activeClusters = _vm.ActiveClusters;
        var visibleTree = VisibleTreeRect();

        DrawActiveJewelRadii(ctx, visibleTree);

        DrawClusterBackgroundDiscs(ctx, visibleTree);

        // Draw connectors: base tree first, then cluster subgraph connectors.
        var allocated = _vm.AllocatedNodes;
        var connThick = Math.Max(0.5, ConnectorThicknessTree * _scale);
        var connPen = new Pen(ConnectorBrush, connThick);
        var connActivePen = new Pen(AllocatedBrush, connThick);
        var connWeaponSet1Pen = new Pen(WeaponSet1Brush, connThick);
        var connWeaponSet2Pen = new Pen(WeaponSet2Brush, connThick);
        var connHoverPen = new Pen(HoverPathBrush, connThick);
        var pathEdges = _vm.HoverPath.Edges;

        foreach (var c in _vm.Tree.Connectors)
        {
            if (ShouldDrawBaseConnector(c))
            {
                DrawConnector(c);
            }
        }

        foreach (var sub in activeClusters.Values)
        {
            foreach (var c in sub.Connectors)
            {
                DrawConnector(c);
            }
        }

        DrawNodesAndHud(ctx, visibleTree);
        return;

        bool ShouldDrawBaseConnector(Connector c)
        {
            return _vm.Tree.Nodes.TryGetValue(c.FromId, out var from)
                && _vm.Tree.Nodes.TryGetValue(c.ToId, out var to)
                && IsDrawableBaseNode(from)
                && IsDrawableBaseNode(to)
                && ConnectorIntersects(c, visibleTree);
        }

        void DrawConnector(Connector c)
        {
            if (!ConnectorIntersects(c, visibleTree))
            {
                return;
            }
            var key = (Math.Min(c.FromId, c.ToId), Math.Max(c.FromId, c.ToId));
            IPen pen;
            if (allocated.Contains(c.FromId) && allocated.Contains(c.ToId))
            {
                pen = ConnectorAllocationPen(c.FromId, c.ToId);
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

        IPen ConnectorAllocationPen(int fromId, int toId)
        {
            var fromSet = _vm.AllocationSetOf(fromId);
            var toSet = _vm.AllocationSetOf(toId);
            if (fromSet == toSet)
            {
                return fromSet switch
                {
                    PassiveAllocationSet.WeaponSet1 => connWeaponSet1Pen,
                    PassiveAllocationSet.WeaponSet2 => connWeaponSet2Pen,
                    _ => connActivePen,
                };
            }
            if (fromSet == PassiveAllocationSet.Normal)
            {
                return toSet == PassiveAllocationSet.WeaponSet1 ? connWeaponSet1Pen : connWeaponSet2Pen;
            }
            if (toSet == PassiveAllocationSet.Normal)
            {
                return fromSet == PassiveAllocationSet.WeaponSet1 ? connWeaponSet1Pen : connWeaponSet2Pen;
            }
            return connActivePen;
        }
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

    private void DrawActiveJewelRadii(DrawingContext ctx, Rect visibleTree)
    {
        foreach (var visual in _vm.ActiveJewelRadii)
        {
            if (!CircleIntersects(visibleTree, visual.X, visual.Y, visual.OuterRadius))
            {
                continue;
            }

            if (visual.Style == JewelRadiusVisualStyle.Timeless && TryDrawTimelessRadius(ctx, visual))
            {
                continue;
            }

            var centre = TreeToScreen(visual.X, visual.Y);
            var outer = visual.OuterRadius * _scale;
            var inner = visual.InnerRadius * _scale;
            var tint = RadiusBrush(visual);
            var pen = new Pen(tint, Math.Max(1.25, 8 * _scale));

            if (TryGetJewelRadiusBitmap("ShadedOuterRing.png") is { } outerRing)
            {
                DrawBitmapCentered(ctx, outerRing, centre, outer);
            }
            else
            {
                ctx.DrawEllipse(null, pen, centre, outer, outer);
            }

            if (inner > 0)
            {
                if (TryGetJewelRadiusBitmap("ShadedInnerRing.png") is { } innerRing)
                {
                    DrawBitmapCentered(ctx, innerRing, centre, inner);
                }
                else
                {
                    ctx.DrawEllipse(null, pen, centre, inner, inner);
                }
            }
            else
            {
                ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x18, 0x66, 0xFF, 0xCC)), null, centre, outer, outer);
            }
        }
    }

    private bool TryDrawTimelessRadius(DrawingContext ctx, JewelRadiusVisual visual)
    {
        if (visual.Conqueror is not { } conqueror)
        {
            return false;
        }
        var prefix = conqueror switch
        {
            TimelessConqueror.EternalEmpire => "PassiveSkillScreenEternalEmpireJewelCircle",
            TimelessConqueror.Karui => "PassiveSkillScreenKaruiJewelCircle",
            TimelessConqueror.Maraketh => "PassiveSkillScreenMarakethJewelCircle",
            TimelessConqueror.Templar => "PassiveSkillScreenTemplarJewelCircle",
            TimelessConqueror.Vaal => "PassiveSkillScreenVaalJewelCircle",
            TimelessConqueror.Kalguuran => "PassiveSkillScreenKalguuranJewelCircle",
            _ => string.Empty,
        };
        if (prefix.Length == 0
            || TryGetJewelRadiusBitmap($"{prefix}1.png") is not { } circle1
            || TryGetJewelRadiusBitmap($"{prefix}2.png") is not { } circle2)
        {
            return false;
        }

        var centre = TreeToScreen(visual.X, visual.Y);
        var radius = visual.OuterRadius * _scale;
        DrawBitmapCentered(ctx, circle1, centre, radius);
        DrawBitmapCentered(ctx, circle2, centre, radius);
        return true;
    }

    private Bitmap? TryGetJewelRadiusBitmap(string relativePath)
    {
        if (_jewelRadiusBitmaps.TryGetValue(relativePath, out var cached))
        {
            return cached;
        }
        var bitmap = _assetResolver.LoadJewelRadiusBitmap(relativePath);
        _jewelRadiusBitmaps[relativePath] = bitmap;
        return bitmap;
    }

    private static void DrawBitmapCentered(DrawingContext ctx, Bitmap bitmap, Point centre, double radius)
    {
        var dst = new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
        ctx.DrawImage(bitmap, dst);
    }

    private static IBrush RadiusBrush(JewelRadiusVisual visual) =>
        visual.Style switch
        {
            JewelRadiusVisualStyle.Annulus => new SolidColorBrush(Color.FromArgb(0xB0, 0xD3, 0x54, 0x00)),
            JewelRadiusVisualStyle.KeystoneCentered => new SolidColorBrush(Color.FromArgb(0xB0, 0xC1, 0x00, 0xFF)),
            _ => new SolidColorBrush(Color.FromArgb(0xB0, 0x66, 0xFF, 0xCC)),
        };

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

    private static bool ConnectorIntersects(Connector connector, Rect visibleTree) =>
        connector switch
        {
            LineConnector lc => RectFromPoints(lc.X1, lc.Y1, lc.X2, lc.Y2, HitMaxRadius).Intersects(visibleTree),
            ArcConnector ac => CircleRect(ac.Cx, ac.Cy, ac.Radius).Intersects(visibleTree),
            _ => true,
        };

    private static Rect RectFromPoints(double x1, double y1, double x2, double y2, double padding = 0)
    {
        var x = Math.Min(x1, x2) - padding;
        var y = Math.Min(y1, y2) - padding;
        return new Rect(x, y, Math.Abs(x2 - x1) + padding * 2, Math.Abs(y2 - y1) + padding * 2);
    }

    private static Rect CircleRect(double cx, double cy, double radius) =>
        new(cx - radius, cy - radius, radius * 2, radius * 2);

    private static bool CircleIntersects(Rect rect, double cx, double cy, double radius) =>
        CircleRect(cx, cy, radius).Intersects(rect);

    private static bool NodeIntersects(Node node, Rect visibleTree) =>
        visibleTree.Contains(new Point(node.X, node.Y));

    private static bool IsDrawableBaseNode(Node node) =>
        node.Type != NodeType.Proxy
        && node.ExpansionSocket?.ParentSocketId is null;

    private IEnumerable<Node> DrawableBaseNodes()
    {
        foreach (var node in _vm.Tree.Nodes.Values)
        {
            if (IsDrawableBaseNode(node))
            {
                yield return node;
            }
        }
    }

    private IEnumerable<Node> ActiveClusterNodes()
    {
        foreach (var sub in _vm.ActiveClusters.Values)
        {
            foreach (var node in sub.Nodes)
            {
                yield return node;
            }
        }
    }

    private void DrawClusterBackgroundDiscs(DrawingContext ctx, Rect visibleTree)
    {
        // Cluster background discs, drawn under connectors and nodes.
        // We don't have the actual medallion art asset, so we approximate it with
        // concentric golden rings on a dark fill.
        foreach (var sub in _vm.ActiveClusters.Values)
        {
            if (!CircleIntersects(visibleTree, sub.ClusterCenterX, sub.ClusterCenterY, sub.CircleRadius))
            {
                continue;
            }

            var centre = TreeToScreen(sub.ClusterCenterX, sub.ClusterCenterY);
            var radius = sub.CircleRadius * _scale;
            var borderThick = Math.Max(1.5, radius * 0.10);
            var ringThick1 = Math.Max(1.0, radius * 0.04);
            var ringThick2 = Math.Max(0.5, radius * 0.025);

            ctx.DrawEllipse(ClusterDiscFillBrush,
                new Pen(ClusterDiscBorderBrush, borderThick),
                centre, radius * 0.88, radius * 0.88);
            ctx.DrawEllipse(null,
                new Pen(ClusterDiscRing1Brush, ringThick1),
                centre, radius * 0.66, radius * 0.66);
            ctx.DrawEllipse(null,
                new Pen(ClusterDiscRing2Brush, ringThick2),
                centre, radius * 0.46, radius * 0.46);

            var orbitThick = Math.Max(0.5, ConnectorThicknessTree * _scale * 0.5);
            ctx.DrawEllipse(null,
                new Pen(ClusterOrbitLineBrush, orbitThick),
                centre, radius, radius);
        }
    }

    private void DrawNodesAndHud(DrawingContext ctx, Rect visibleTree)
    {
        var allocated = _vm.AllocatedNodes;
        DrawRemovedDiffNodes(ctx, visibleTree);

        // Base-tree nodes (skip proxies; skip jewel sockets that have an active cluster —
        // the cluster ring's slot 0 sits at the socket position and replaces it visually).
        foreach (var n in DrawableBaseNodes())
        {
            if (!NodeIntersects(n, visibleTree))
            {
                continue;
            }
            var isHover = _vm.HoverNodeId == n.Id;
            var onPath = _vm.HoverPathNodes.Contains(n.Id);
            DrawCurrentDiffHighlight(ctx, n);
            DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath, useClusterSocketFrame: true);
        }

        // Cluster subgraph nodes (only rendered when the jewel is active).
        foreach (var n in ActiveClusterNodes())
        {
            if (!NodeIntersects(n, visibleTree))
            {
                continue;
            }
            var isHover = _vm.HoverNodeId == n.Id;
            var onPath = _vm.HoverPathNodes.Contains(n.Id);
            DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath, useClusterSocketFrame: true);
        }

        // HUD: alloc count. The hovered node details are drawn as a tooltip below.
        var diff = _vm.Diff;
        var diffText = diff.HasChanges
            ? $" diff +{diff.AddedCount} ~{diff.ChangedCount} -{diff.RemovedCount}"
            : string.Empty;
        var hud = $"v{_vm.Tree.Version} • allocated: {allocated.Count}{diffText}";
        var ft = new FormattedText(hud, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.White);
        ctx.DrawText(ft, new Point(8, 8));
        DrawNodeTooltip(ctx);
    }

    private void DrawCurrentDiffHighlight(DrawingContext ctx, Node node)
    {
        if (!_vm.Diff.CurrentNodeDiffs.TryGetValue(node.Id, out var diff))
        {
            return;
        }

        var brush = diff.Kind switch
        {
            TreeNodeDiffKind.Added => DiffAddedBrush,
            TreeNodeDiffKind.Changed => DiffChangedBrush,
            _ => null,
        };
        if (brush is null)
        {
            return;
        }

        DrawDiffRing(ctx, node, brush, solidFill: false);
    }

    private void DrawRemovedDiffNodes(DrawingContext ctx, Rect visibleTree)
    {
        foreach (var diff in _vm.Diff.RemovedNodes)
        {
            var node = diff.Node;
            if (!NodeIntersects(node, visibleTree))
            {
                continue;
            }

            DrawDiffRing(ctx, node, DiffRemovedBrush, solidFill: true);
        }
    }

    private void DrawDiffRing(DrawingContext ctx, Node node, IBrush brush, bool solidFill)
    {
        var centre = TreeToScreen(node.X, node.Y);
        var half = node.Visual is not null
            ? Poe2FrameHalfSize(node, string.Empty)
            : new Size(NodeRadius, NodeRadius);
        var rx = Math.Max(NodeRadius * _scale, half.Width * _scale * 0.94);
        var ry = Math.Max(NodeRadius * _scale, half.Height * _scale * 0.94);
        var thickness = Math.Max(2.0, 9.0 * _scale);
        ctx.DrawEllipse(solidFill ? new SolidColorBrush(Color.FromArgb(0x18, 0xE5, 0x56, 0x56)) : null,
            new Pen(brush, thickness),
            centre,
            rx,
            ry);
    }

}
