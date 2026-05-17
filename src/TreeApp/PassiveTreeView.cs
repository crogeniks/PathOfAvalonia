using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;

namespace PathOfAvalonia.TreeApp;

public sealed class PassiveTreeView : Control
{
    private readonly PassiveTreeViewModel _vm;
    private readonly SpriteMap _sprites;
    private readonly ITreeImageAssetResolver _assetResolver;
    private ContextMenu? _clusterMenu;
    // One Bitmap per unique atlas filename (multiple atlas keys share a file).
    private readonly Dictionary<string, Bitmap> _atlasBitmaps = new();

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
    private static readonly IPen NodeOutlinePen = new Pen(Brushes.Black, 1.5);
    // Cluster background disc layers (programmatic stand-in for the missing art asset).
    // Colors approximate the PoB golden-medallion aesthetic.
    private static readonly IBrush ClusterDiscFillBrush   = new SolidColorBrush(Color.FromArgb(0xD8, 0x12, 0x14, 0x1C));
    private static readonly IBrush ClusterDiscBorderBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x68, 0x3E));
    private static readonly IBrush ClusterDiscRing1Brush  = new SolidColorBrush(Color.FromRgb(0x5E, 0x52, 0x32));
    private static readonly IBrush ClusterDiscRing2Brush  = new SolidColorBrush(Color.FromRgb(0x42, 0x3C, 0x24));
    private static readonly IBrush ClusterOrbitLineBrush  = new SolidColorBrush(Color.FromArgb(0x60, 0x90, 0x82, 0x56));

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

        DrawNodesAndHud(ctx);
        return;

        bool ShouldDrawBaseConnector(Connector c)
        {
            return _vm.Tree.Nodes.TryGetValue(c.FromId, out var from)
                && _vm.Tree.Nodes.TryGetValue(c.ToId, out var to)
                && IsVisibleBaseTreeNode(from)
                && IsVisibleBaseTreeNode(to);
        }

        static bool IsVisibleBaseTreeNode(Node n)
        {
            return n.Type != NodeType.Proxy
                && n.ExpansionSocket?.ParentSocketId is null;
        }

        void DrawConnector(Connector c)
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
            if (n.ExpansionSocket?.ParentSocketId is not null)
            {
                continue;
            }
            var isHover = _vm.HoverNodeId == n.Id;
            var onPath = _vm.HoverPathNodes.Contains(n.Id);
            DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath, useClusterSocketFrame: true);
        }

        // Cluster subgraph nodes (only rendered when the jewel is active).
        foreach (var sub in _vm.ActiveClusters.Values)
        {
            foreach (var n in sub.Nodes)
            {
                var isHover = _vm.HoverNodeId == n.Id;
                var onPath = _vm.HoverPathNodes.Contains(n.Id);
                DrawNode(ctx, n, allocated.Contains(n.Id), isHover || onPath, useClusterSocketFrame: true);
            }
        }

        // HUD: alloc count. The hovered node details are drawn as a tooltip below.
        var hud = $"v{_vm.Tree.Version} • allocated: {allocated.Count}";
        var ft = new FormattedText(hud, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, 14, Brushes.White);
        ctx.DrawText(ft, new Point(8, 8));
        DrawNodeTooltip(ctx);
    }

    private void DrawNodeTooltip(DrawingContext ctx)
    {
        if (_vm.HoverNode is not { } node)
        {
            return;
        }

        if (node.Type == NodeType.JewelSocket && _vm.SocketedJewelAt(node.Id) is { } socketedJewel)
        {
            DrawItemTooltip(ctx, ItemViewModel.FromImported(socketedJewel));
            return;
        }

        var contentWidth = 380.0;
        var paddingX = 12.0;
        var paddingY = 9.0;
        var headerHeight = 32.0;
        var title = CreateText(node.Name, 20, TooltipTitleBrush);
        var lines = BuildTooltipLines(node, contentWidth);

        var width = Math.Max(260, Math.Min(430, Math.Max(title.Width + paddingX * 2, contentWidth + paddingX * 2)));
        var height = headerHeight + paddingY;
        foreach (var line in lines)
        {
            height += line.IsGap ? 8 : line.Text.Height + 2;
        }
        height += paddingY;

        var x = _lastPointerPosition.X + 18;
        var y = _lastPointerPosition.Y + 18;
        if (x + width > Bounds.Width - 8)
        {
            x = _lastPointerPosition.X - width - 18;
        }
        if (y + height > Bounds.Height - 8)
        {
            y = Bounds.Height - height - 8;
        }
        x = Math.Clamp(x, 8, Math.Max(8, Bounds.Width - width - 8));
        y = Math.Clamp(y, 8, Math.Max(8, Bounds.Height - height - 8));

        var rect = new Rect(x, y, width, height);
        ctx.FillRectangle(TooltipFillBrush, rect);
        ctx.FillRectangle(TooltipHeaderBrush, new Rect(x, y, width, headerHeight));
        ctx.DrawRectangle(null, new Pen(TooltipBorderBrush, 1.5), rect);
        ctx.DrawLine(new Pen(TooltipBorderBrush, 1), new Point(x, y + headerHeight), new Point(x + width, y + headerHeight));

        ctx.DrawText(title, new Point(x + (width - title.Width) * 0.5, y + (headerHeight - title.Height) * 0.5 - 1));

        var cy = y + headerHeight + paddingY;
        foreach (var line in lines)
        {
            if (line.IsGap)
            {
                cy += 8;
                continue;
            }
            ctx.DrawText(line.Text, new Point(x + paddingX, cy));
            cy += line.Text.Height + 2;
        }
    }

    private void DrawItemTooltip(DrawingContext ctx, ItemViewModel item)
    {
        var contentWidth = 380.0;
        var paddingX = 12.0;
        var paddingY = 9.0;
        var headerHeight = item.HasSeparateName ? 48.0 : 32.0;
        var title = CreateText(item.Name, 20, item.NameBrush);
        var subtitle = item.HasSeparateName ? CreateText(item.BaseType, 14, TooltipTitleBrush) : null;
        var lines = BuildItemTooltipLines(item, contentWidth);

        var headerWidth = title.Width;
        if (subtitle is not null)
        {
            headerWidth = Math.Max(headerWidth, subtitle.Width);
        }
        var width = Math.Max(260, Math.Min(430, Math.Max(headerWidth + paddingX * 2, contentWidth + paddingX * 2)));
        var height = headerHeight + paddingY;
        foreach (var line in lines)
        {
            height += line.IsGap ? 8 : line.Text.Height + 2;
        }
        height += paddingY;

        var x = _lastPointerPosition.X + 18;
        var y = _lastPointerPosition.Y + 18;
        if (x + width > Bounds.Width - 8)
        {
            x = _lastPointerPosition.X - width - 18;
        }
        if (y + height > Bounds.Height - 8)
        {
            y = Bounds.Height - height - 8;
        }
        x = Math.Clamp(x, 8, Math.Max(8, Bounds.Width - width - 8));
        y = Math.Clamp(y, 8, Math.Max(8, Bounds.Height - height - 8));

        var rect = new Rect(x, y, width, height);
        ctx.FillRectangle(TooltipFillBrush, rect);
        ctx.FillRectangle(TooltipHeaderBrush, new Rect(x, y, width, headerHeight));
        ctx.DrawRectangle(null, new Pen(item.BorderBrush, 1.5), rect);
        ctx.DrawLine(new Pen(item.BorderBrush, 1), new Point(x, y + headerHeight), new Point(x + width, y + headerHeight));

        if (subtitle is null)
        {
            ctx.DrawText(title, new Point(x + (width - title.Width) * 0.5, y + (headerHeight - title.Height) * 0.5 - 1));
        }
        else
        {
            var textHeight = title.Height + subtitle.Height + 1;
            var ty = y + (headerHeight - textHeight) * 0.5 - 1;
            ctx.DrawText(title, new Point(x + (width - title.Width) * 0.5, ty));
            ctx.DrawText(subtitle, new Point(x + (width - subtitle.Width) * 0.5, ty + title.Height + 1));
        }

        var cy = y + headerHeight + paddingY;
        foreach (var line in lines)
        {
            if (line.IsGap)
            {
                cy += 8;
                continue;
            }
            ctx.DrawText(line.Text, new Point(x + paddingX, cy));
            cy += line.Text.Height + 2;
        }
    }

    private List<TooltipLine> BuildTooltipLines(Node node, double contentWidth)
    {
        var lines = new List<TooltipLine>();

        AddWrappedLines(lines, PassiveEffectLines(node), contentWidth, TooltipStatBrush, 14, Typeface.Default);
        AddAllocationPreviewLines(lines, node, contentWidth);
        AddWrappedLines(lines, node.FlavourText, contentWidth, TooltipFlavourBrush, 14,
            new Typeface(FontFamily.Default, FontStyle.Italic, FontWeight.Normal));
        AddWrappedLines(lines, node.ReminderText, contentWidth, TooltipReminderBrush, 12, Typeface.Default, gapBefore: lines.Count > 0);

        return lines;
    }

    private List<TooltipLine> BuildItemTooltipLines(ItemViewModel item, double contentWidth)
    {
        var lines = new List<TooltipLine>();
        AddModLines(lines, item.Implicits, contentWidth, gapBefore: false);
        AddModLines(lines, item.Body, contentWidth, gapBefore: lines.Count > 0);
        AddModLines(lines, item.StatusFlags, contentWidth, gapBefore: lines.Count > 0);
        return lines;
    }

    private IEnumerable<string> PassiveEffectLines(Node node)
    {
        if (node.Type == NodeType.Mastery)
        {
            if (_vm.SelectedMasteryEffect(node.Id) is { Stats.Count: > 0 } selected)
            {
                return selected.Stats;
            }
            if (node.MasteryEffects is { Count: > 0 } effects)
            {
                var result = new List<string>();
                foreach (var effect in effects)
                {
                    result.AddRange(effect.Stats);
                }
                return result;
            }
        }

        return node.Stats;
    }

    private void AddAllocationPreviewLines(List<TooltipLine> lines, Node node, double contentWidth)
    {
        _ = node;
        _ = contentWidth;
        // Future home for "Allocating this node will give you" and DPS/stat deltas.
        // The app does not yet have the calculation skeleton needed for this section.
    }

    private static void AddWrappedLines(
        List<TooltipLine> lines,
        IEnumerable<string> source,
        double maxWidth,
        IBrush brush,
        double size,
        Typeface typeface,
        bool gapBefore = false)
    {
        var added = false;
        foreach (var raw in source)
        {
            foreach (var line in WrapText(raw, maxWidth, size, typeface, brush))
            {
                if (!added && gapBefore)
                {
                    lines.Add(TooltipLine.Gap);
                }
                lines.Add(new TooltipLine(CreateText(line, size, brush, typeface)));
                added = true;
            }
        }
    }

    private static void AddModLines(
        List<TooltipLine> lines,
        IEnumerable<ModLineViewModel> source,
        double maxWidth,
        bool gapBefore)
    {
        var added = false;
        foreach (var raw in source)
        {
            foreach (var line in WrapText(raw.Text, maxWidth, 14, Typeface.Default, raw.Brush))
            {
                if (!added && gapBefore)
                {
                    lines.Add(TooltipLine.Gap);
                }
                lines.Add(new TooltipLine(CreateText(line, 14, raw.Brush)));
                added = true;
            }
        }
    }

    private static IEnumerable<string> WrapText(string text, double maxWidth, double size, Typeface typeface, IBrush brush)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (CreateText(candidate, size, brush, typeface).Width <= maxWidth || current.Length == 0)
            {
                current = candidate;
                continue;
            }

            yield return current;
            current = word;
        }
        if (current.Length > 0)
        {
            yield return current;
        }
    }

    private static FormattedText CreateText(string text, double size, IBrush brush, Typeface? typeface = null) =>
        new(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface ?? Typeface.Default, size, brush);

    private void DrawNode(DrawingContext ctx, Node n, bool alloc, bool hover, bool useClusterSocketFrame)
    {
        var screen = TreeToScreen(n.X, n.Y);
        if (TryDrawGameSpecificNode(ctx, n, alloc, hover, screen))
        {
            if (n.Type == NodeType.JewelSocket && _vm.SocketedJewelOverlayAt(n) is { } jewelOverlay)
            {
                DrawSocketedJewelOverlay(ctx, jewelOverlay, screen);
            }
            return;
        }

        // Icon: skills atlas (normal/notable/keystone) or mastery atlas.
        var (iconAtlas, iconPath) = IconSprite(n, alloc, hover: false);
        if (iconAtlas is not null && iconPath is not null)
        {
            _ = DrawSprite(ctx, iconAtlas, iconPath, screen);
        }
        // Hover overlay: for masteries, the masteryConnected sprite glows on top of
        // the base icon rather than replacing it.
        if (hover && !alloc && n.Type == NodeType.Mastery && n.InactiveIcon is { } ii)
        {
            _ = DrawSprite(ctx, "masteryConnected", ii, screen);
        }
        // Frame: ornate border. Mastery has no separate frame (baked in).
        // Only active cluster subgraphs alter the socket frame. A socketed jewel by
        // itself must not make the node look allocated.
        var clusterSize = useClusterSocketFrame && n.Type == NodeType.JewelSocket ? _vm.ClusterSizeAt(n.Id) : null;
        var frameKey = FrameKey(n.Type, alloc, hover, clusterSize);
        var drewFrame = false;
        if (frameKey is not null)
        {
            drewFrame = DrawSprite(ctx, "frame", frameKey, screen);
        }
        if (n.Type == NodeType.JewelSocket && _vm.SocketedJewelOverlayAt(n) is { } socketOverlay)
        {
            DrawSocketedJewelOverlay(ctx, socketOverlay, screen);
        }
        // Fallback for node types we haven't wired art for yet (e.g. class start),
        // so they don't disappear.
        if (!drewFrame && (frameKey is not null || iconAtlas is null))
        {
            var r = NodeRadius * _scale;
            ctx.DrawEllipse(alloc ? AllocatedBrush : NormalBrush, NodeOutlinePen, screen, r, r);
        }
    }

    private bool TryDrawGameSpecificNode(DrawingContext ctx, Node node, bool allocated, bool hover, Point screen)
    {
        if (node.Visual is not { } visual)
        {
            return false;
        }

        var drewAny = false;
        if (!string.IsNullOrWhiteSpace(visual.Icon))
        {
            drewAny |= DrawPoe2Sprite(ctx, "poe2NodeIcons", visual.Icon, screen, Poe2IconHalfSize(node));
        }

        drewAny |= DrawPoe2VectorFrame(ctx, node, allocated, hover, screen);

        return drewAny;
    }

    private bool DrawPoe2VectorFrame(DrawingContext ctx, Node node, bool allocated, bool hover, Point centre)
    {
        if (node.Type is NodeType.ClassStart or NodeType.AscendancyStart or NodeType.Proxy or NodeType.Mastery)
        {
            return false;
        }

        var half = Poe2FrameHalfSize(node, string.Empty);
        var rx = half.Width * _scale;
        var ry = half.Height * _scale;
        if (rx <= 0 || ry <= 0)
        {
            return false;
        }

        var baseBrush = node.Type switch
        {
            NodeType.Notable or NodeType.Keystone => Poe2NotableFrameBrush,
            NodeType.JewelSocket => Poe2SocketFrameBrush,
            _ => Poe2NormalFrameBrush,
        };
        var brush = allocated ? Poe2AllocatedFrameBrush : hover ? Poe2HoverFrameBrush : baseBrush;
        var thickness = Math.Max(1.0, (node.Type switch
        {
            NodeType.Keystone => 5.0,
            NodeType.Notable => 3.5,
            NodeType.JewelSocket => 3.0,
            _ => 2.2,
        }) * _scale);

        if (node.Type == NodeType.JewelSocket)
        {
            ctx.DrawEllipse(Poe2SocketFillBrush, new Pen(brush, thickness), centre, rx * 0.72, ry * 0.72);
            ctx.DrawEllipse(null, new Pen(baseBrush, Math.Max(0.75, thickness * 0.45)), centre, rx, ry);
            return true;
        }

        ctx.DrawEllipse(null, new Pen(brush, thickness), centre, rx, ry);
        if (node.Type is NodeType.Notable or NodeType.Keystone)
        {
            ctx.DrawEllipse(null, new Pen(Poe2NormalFrameBrush, Math.Max(0.75, thickness * 0.45)), centre, rx * 0.83, ry * 0.83);
        }
        return true;
    }

    private bool DrawPoe2Sprite(DrawingContext ctx, string atlasKey, string spriteKey, Point centre, Size halfSizeTree)
    {
        if (!_sprites.Atlases.TryGetValue(atlasKey, out var atlas))
        {
            return false;
        }
        if (!atlas.Coords.TryGetValue(spriteKey, out var rect))
        {
            return false;
        }
        if (!_atlasBitmaps.TryGetValue(atlas.File, out var bmp))
        {
            return false;
        }

        var halfW = halfSizeTree.Width * _scale;
        var halfH = halfSizeTree.Height * _scale;
        var dst = new Rect(centre.X - halfW, centre.Y - halfH, halfW * 2, halfH * 2);
        var src = new Rect(rect.X, rect.Y, rect.W, rect.H);
        ctx.DrawImage(bmp, src, dst);
        return true;
    }

    private static Size Poe2IconHalfSize(Node node) => node.Type switch
    {
        NodeType.Keystone => new Size(82, 82),
        NodeType.Notable => new Size(54, 54),
        NodeType.JewelSocket => new Size(76, 76),
        NodeType.AscendancyStart => new Size(16, 16),
        NodeType.Mastery => new Size(37, 37),
        _ when node.AscendancyName is not null => new Size(54, 54),
        _ => new Size(37, 37),
    };

    private static Size Poe2FrameHalfSize(Node node, string frameKey)
    {
        if (frameKey.Contains("FrameLarge", StringComparison.Ordinal))
        {
            return new Size(100, 100);
        }
        if (frameKey.Contains("FrameSmall", StringComparison.Ordinal))
        {
            return new Size(80, 80);
        }
        return node.Type switch
        {
            NodeType.Keystone => new Size(120, 120),
            NodeType.Notable => new Size(80, 80),
            NodeType.JewelSocket => new Size(76, 76),
            NodeType.AscendancyStart => new Size(24, 24),
            _ => new Size(54, 54),
        };
    }

    private static string? Poe2DefaultFrameKey(NodeType type, bool allocated, bool hover) => type switch
    {
        NodeType.Normal => allocated ? "PSSkillFrameActive" : hover ? "PSSkillFrameHighlighted" : "PSSkillFrame",
        NodeType.Notable => allocated ? "NotableFrameAllocated" : hover ? "NotableFrameCanAllocate" : "NotableFrameUnallocated",
        NodeType.Keystone => allocated ? "KeystoneFrameAllocated" : hover ? "KeystoneFrameCanAllocate" : "KeystoneFrameUnallocated",
        NodeType.JewelSocket => allocated ? "JewelFrameAllocated" : hover ? "JewelFrameCanAllocate" : "JewelFrameUnallocated",
        _ => null,
    };

    private bool DrawSprite(DrawingContext ctx, string atlasKey, string spriteKey, Point centre)
    {
        if (!_sprites.Atlases.TryGetValue(atlasKey, out var atlas))
        {
            return false;
        }
        if (!atlas.Coords.TryGetValue(spriteKey, out var rect))
        {
            return false;
        }
        if (!_atlasBitmaps.TryGetValue(atlas.File, out var bmp))
        {
            return false;
        }
        var halfW = rect.W * SpriteDisplayScale * _scale;
        var halfH = rect.H * SpriteDisplayScale * _scale;
        var dst = new Rect(centre.X - halfW, centre.Y - halfH, halfW * 2, halfH * 2);
        var src = new Rect(rect.X, rect.Y, rect.W, rect.H);
        ctx.DrawImage(bmp, src, dst);
        return true;
    }

    private void DrawSocketedJewelOverlay(DrawingContext ctx, string spriteKey, Point centre)
    {
        if (_sprites.Lookup("jewel", spriteKey) is not null)
        {
            _ = DrawSprite(ctx, "jewel", spriteKey, centre);
            return;
        }
        if (_sprites.Lookup("azmeriBloodline", spriteKey) is not null)
        {
            _ = DrawSprite(ctx, "azmeriBloodline", spriteKey, centre);
        }
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
            if (n.ExpansionSocket?.ParentSocketId is not null)
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
        _lastPointerPosition = p;
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
        else if (hit is not null)
        {
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
        // Keep cursor anchored.
        _offsetX = p.X - txBefore * _scale;
        _offsetY = p.Y - tyBefore * _scale;
        InvalidateVisual();
        e.Handled = true;
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

    private readonly record struct TooltipLine(FormattedText Text, bool IsGap = false)
    {
        public static TooltipLine Gap { get; } = new(CreateText(string.Empty, 1, Brushes.Transparent), IsGap: true);
    }
}
