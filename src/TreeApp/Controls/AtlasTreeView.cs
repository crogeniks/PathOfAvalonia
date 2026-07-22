using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Atlas;
using SkiaSharp;

namespace PathOfAvalonia.TreeApp.Controls;

/// <summary>
/// Atlas-specific tree canvas. It shares the low-level sprite and connector
/// formats with the character tree, while keeping Atlas allocation and category
/// interactions out of PassiveTreeView and PassiveTreeViewModel.
/// </summary>
public sealed class AtlasTreeView : Control
{
    private const double SpriteDisplayScale = 1.33;
    private const double HitRadiusNormal = 40 * SpriteDisplayScale;
    private const double HitRadiusNotable = 58 * SpriteDisplayScale;
    private const double HitRadiusKeystone = 84 * SpriteDisplayScale;
    private const double HitRadiusClusterIcon = 65 * SpriteDisplayScale;
    private const double HitMaxRadius = HitRadiusKeystone;
    private const double ConnectorThicknessTree = 18;
    private const double MinZoomFactor = 0.9;
    private const double MaxZoomFactor = 10;
    private const double BackgroundTileSize = 98;

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x18));
    private static readonly IBrush FallbackNodeBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x60));
    private static readonly IBrush AllocatedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x4A));
    private static readonly IBrush SearchBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0x59, 0x4F));
    private static readonly IBrush CategoryBrush = new SolidColorBrush(Color.FromRgb(0x55, 0xE8, 0xFF));
    private static readonly IBrush DiffAddedBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0x5A));
    private static readonly IBrush DiffChangedBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x5C, 0xFF));
    private static readonly IBrush DiffRemovedBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x56, 0x56));
    private static readonly IBrush TooltipBackgroundBrush = new SolidColorBrush(Color.FromArgb(0xF2, 0x06, 0x08, 0x0B));
    private static readonly IBrush TooltipTitleBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xDF, 0xC4));
    private static readonly IBrush TooltipStatBrush = new SolidColorBrush(Color.FromRgb(0x8D, 0x98, 0xFF));
    private static readonly IBrush TooltipReminderBrush = new SolidColorBrush(Color.FromRgb(0xA6, 0xB1, 0xA4));
    private static readonly IBrush TooltipFlavourBrush = new SolidColorBrush(Color.FromRgb(0xD2, 0x84, 0x2E));
    private static readonly IPen TooltipBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0x76, 0x22)), 1.5);

    private readonly AtlasTreeViewModel _viewModel;
    private readonly SpriteMap _sprites;
    private readonly ITreeImageAssetResolver _assetResolver;
    private readonly Dictionary<string, Bitmap> _atlasBitmaps = [];
    private readonly HashSet<string> _missingAtlasFiles = new(StringComparer.Ordinal);
    private Bitmap? _backgroundTile;
    private ImageBrush? _backgroundTileBrush;
    private DispatcherTimer? _categoryHighlightTimer;
    private bool _subscribed;
    private double _scale = 0.05;
    private double _fitScale = 0.05;
    private double _offsetX;
    private double _offsetY;
    private bool _viewInitialised;
    private bool _panning;
    private bool _panMoved;
    private Point _panStart;
    private Point _tooltipAnchor;
    private double _panStartOffsetX;
    private double _panStartOffsetY;

    public AtlasTreeView(
        AtlasTreeViewModel viewModel,
        SpriteMap sprites,
        ITreeImageAssetResolver assetResolver)
    {
        _viewModel = viewModel;
        _sprites = sprites;
        _assetResolver = assetResolver;
        ClipToBounds = true;
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_subscribed)
        {
            _viewModel.RedrawRequested += InvalidateVisual;
            _subscribed = true;
        }
        if (_backgroundTile is null && _assetResolver.LoadBackground(_viewModel.Tree.Version) is { } tile)
        {
            _backgroundTile = tile;
            _backgroundTileBrush = new ImageBrush(tile)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.Tile,
            };
        }
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _categoryHighlightTimer?.Stop();
        _viewModel.ClearClusterHighlights();
        if (_subscribed)
        {
            _viewModel.RedrawRequested -= InvalidateVisual;
            _subscribed = false;
        }
        foreach (var bitmap in _atlasBitmaps.Values)
        {
            bitmap.Dispose();
        }
        _atlasBitmaps.Clear();
        _missingAtlasFiles.Clear();
        _backgroundTile?.Dispose();
        _backgroundTile = null;
        _backgroundTileBrush = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    public override void Render(DrawingContext context)
    {
        EnsureViewInitialised();
        context.FillRectangle(BackgroundBrush, new Rect(Bounds.Size));
        DrawBackgroundTile(context);
        DrawAtlasBackground(context);
        var visibleTree = VisibleTreeRect();
        DrawGroupVisuals(context, visibleTree);
        context.Custom(new AtlasConnectorDrawOperation(
            new Rect(Bounds.Size),
            _viewModel.Tree.Connectors,
            visibleTree,
            _viewModel.AllocatedNodes.ToHashSet(),
            _viewModel.HoverPath.Edges,
            _scale,
            _offsetX,
            _offsetY));
        DrawRemovedDiffNodes(context, visibleTree);
        foreach (var node in _viewModel.Tree.Nodes.Values)
        {
            if (!visibleTree.Contains(new Point(node.X, node.Y)))
            {
                continue;
            }
            DrawNode(context, node);
            DrawDiff(context, node);
            DrawSearchHighlight(context, node);
            DrawCategoryHighlight(context, node);
        }
        DrawTooltip(context);
    }

    private void EnsureViewInitialised()
    {
        if (_viewInitialised || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        var bounds = _viewModel.Tree.Bounds;
        _fitScale = Math.Min(Bounds.Width / bounds.Width, Bounds.Height / bounds.Height) * 0.95;
        _scale = _fitScale;
        _offsetX = Bounds.Width * 0.5 - bounds.CenterX * _scale;
        _offsetY = Bounds.Height * 0.5 - bounds.CenterY * _scale;
        _viewInitialised = true;
    }

    private Point TreeToScreen(double x, double y) => new(x * _scale + _offsetX, y * _scale + _offsetY);

    private (double X, double Y) ScreenToTree(Point point) =>
        ((point.X - _offsetX) / _scale, (point.Y - _offsetY) / _scale);

    private Rect VisibleTreeRect(double padding = 700)
    {
        var topLeft = ScreenToTree(new Point(0, 0));
        var bottomRight = ScreenToTree(new Point(Bounds.Width, Bounds.Height));
        var x = Math.Min(topLeft.X, bottomRight.X) - padding;
        var y = Math.Min(topLeft.Y, bottomRight.Y) - padding;
        return new Rect(
            x,
            y,
            Math.Abs(bottomRight.X - topLeft.X) + padding * 2,
            Math.Abs(bottomRight.Y - topLeft.Y) + padding * 2);
    }

    private void DrawBackgroundTile(DrawingContext context)
    {
        if (_backgroundTileBrush is null)
        {
            return;
        }
        var x = ((_offsetX % BackgroundTileSize) + BackgroundTileSize) % BackgroundTileSize - BackgroundTileSize;
        var y = ((_offsetY % BackgroundTileSize) + BackgroundTileSize) % BackgroundTileSize - BackgroundTileSize;
        _backgroundTileBrush.DestinationRect = new RelativeRect(x, y, BackgroundTileSize, BackgroundTileSize, RelativeUnit.Absolute);
        context.FillRectangle(_backgroundTileBrush, new Rect(Bounds.Size));
    }

    private void DrawAtlasBackground(DrawingContext context)
    {
        var bounds = _viewModel.Tree.Bounds;
        DrawSprite(context, "atlasBackground", "AtlasPassiveBackground", TreeToScreen(bounds.CenterX, bounds.CenterY));
    }

    private void DrawGroupVisuals(DrawingContext context, Rect visibleTree)
    {
        foreach (var visual in _viewModel.Tree.GroupVisuals)
        {
            if (!CircleIntersects(visibleTree, visual.X, visual.Y, 700))
            {
                continue;
            }
            DrawSprite(context, visual.AtlasKey, visual.SpriteKey, TreeToScreen(visual.X, visual.Y));
        }
    }

    private void DrawNode(DrawingContext context, AtlasNode node)
    {
        var allocated = _viewModel.IsAllocated(node.Id);
        var highlighted = _viewModel.HoverNodeId == node.Id || _viewModel.HoverPathNodes.Contains(node.Id);
        var centre = TreeToScreen(node.X, node.Y);
        var (atlas, icon) = node.Type switch
        {
            AtlasNodeType.Normal => (allocated ? "normalActive" : "normalInactive", node.Icon),
            AtlasNodeType.Notable => (allocated ? "notableActive" : "notableInactive", node.Icon),
            AtlasNodeType.Keystone => (allocated ? "keystoneActive" : "keystoneInactive", node.Icon),
            AtlasNodeType.ClusterIcon => ("mastery", node.Icon),
            _ => (null, null),
        };
        var iconDrawn = atlas is not null && icon is not null && DrawSprite(context, atlas, icon, centre);
        var frame = node.Type switch
        {
            AtlasNodeType.Normal => allocated ? "PSSkillFrameActive" : highlighted ? "PSSkillFrameHighlighted" : "PSSkillFrame",
            AtlasNodeType.Notable => allocated ? "NotableFrameAllocated" : highlighted ? "NotableFrameCanAllocate" : "NotableFrameUnallocated",
            AtlasNodeType.Keystone => allocated ? "KeystoneFrameAllocated" : highlighted ? "KeystoneFrameCanAllocate" : "KeystoneFrameUnallocated",
            _ => null,
        };
        var frameDrawn = frame is not null && DrawSprite(context, "frame", frame, centre);
        if (!iconDrawn && !frameDrawn && node.Type is not (AtlasNodeType.Start or AtlasNodeType.ClusterIcon))
        {
            var radius = 45 * _scale;
            context.DrawEllipse(allocated ? AllocatedBrush : FallbackNodeBrush, new Pen(Brushes.Black, 1.5), centre, radius, radius);
        }
    }

    private void DrawSearchHighlight(DrawingContext context, AtlasNode node)
    {
        if (!_viewModel.SearchResultNodeIds.Contains(node.Id))
        {
            return;
        }
        DrawHighlightRing(context, node, SearchBrush, 6);
    }

    private void DrawCategoryHighlight(DrawingContext context, AtlasNode node)
    {
        if (!_viewModel.HighlightedClusterIconNodeIds.Contains(node.Id))
        {
            return;
        }
        DrawHighlightRing(context, node, CategoryBrush, 9);
    }

    private void DrawDiff(DrawingContext context, AtlasNode node)
    {
        if (!_viewModel.Diff.CurrentNodeDiffs.TryGetValue(node.Id, out var diff))
        {
            return;
        }
        DrawHighlightRing(
            context,
            node,
            diff.Kind == AtlasNodeDiffKind.Added ? DiffAddedBrush : DiffChangedBrush,
            10);
    }

    private void DrawRemovedDiffNodes(DrawingContext context, Rect visibleTree)
    {
        foreach (var diff in _viewModel.Diff.RemovedNodes)
        {
            if (visibleTree.Contains(new Point(diff.Node.X, diff.Node.Y)))
            {
                DrawHighlightRing(context, diff.Node, DiffRemovedBrush, 10);
            }
        }
    }

    private void DrawHighlightRing(DrawingContext context, AtlasNode node, IBrush brush, double thickness)
    {
        var centre = TreeToScreen(node.X, node.Y);
        var radius = NodeRadius(node.Type) * _scale + Math.Max(4, 12 * _scale);
        context.DrawEllipse(null, new Pen(brush, Math.Max(2, thickness * _scale)), centre, radius, radius);
    }

    private static double NodeRadius(AtlasNodeType type) => type switch
    {
        AtlasNodeType.Keystone => HitRadiusKeystone,
        AtlasNodeType.Notable => HitRadiusNotable,
        AtlasNodeType.ClusterIcon => HitRadiusClusterIcon,
        _ => HitRadiusNormal,
    };

    private bool DrawSprite(DrawingContext context, string atlasKey, string spriteKey, Point centre)
    {
        if (!_sprites.Atlases.TryGetValue(atlasKey, out var atlas)
            || !atlas.Coords.TryGetValue(spriteKey, out var sprite)
            || !TryGetBitmap(atlas.File, out var bitmap))
        {
            return false;
        }
        var halfWidth = sprite.W * SpriteDisplayScale * _scale;
        var halfHeight = sprite.H * SpriteDisplayScale * _scale;
        context.DrawImage(
            bitmap,
            new Rect(sprite.X, sprite.Y, sprite.W, sprite.H),
            new Rect(centre.X - halfWidth, centre.Y - halfHeight, halfWidth * 2, halfHeight * 2));
        return true;
    }

    private bool TryGetBitmap(string file, out Bitmap bitmap)
    {
        if (_atlasBitmaps.TryGetValue(file, out bitmap!))
        {
            return true;
        }
        if (_missingAtlasFiles.Contains(file) || _assetResolver.LoadBitmap(file) is not { } loaded)
        {
            _missingAtlasFiles.Add(file);
            bitmap = null!;
            return false;
        }
        _atlasBitmaps[file] = loaded;
        bitmap = loaded;
        return true;
    }

    private int? HitTestNode(Point screen)
    {
        var tree = ScreenToTree(screen);
        var bestDistance = double.MaxValue;
        int? bestNodeId = null;
        foreach (var node in _viewModel.Tree.Nodes.Values)
        {
            var dx = tree.X - node.X;
            var dy = tree.Y - node.Y;
            if (Math.Abs(dx) > HitMaxRadius || Math.Abs(dy) > HitMaxRadius)
            {
                continue;
            }
            var distance = dx * dx + dy * dy;
            var radius = NodeRadius(node.Type);
            if (distance < radius * radius && distance < bestDistance)
            {
                bestDistance = distance;
                bestNodeId = node.Id;
            }
        }
        return bestNodeId;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        if (_panning)
        {
            _offsetX = _panStartOffsetX + point.X - _panStart.X;
            _offsetY = _panStartOffsetY + point.Y - _panStart.Y;
            var dx = point.X - _panStart.X;
            var dy = point.Y - _panStart.Y;
            _panMoved |= dx * dx + dy * dy > 16;
            InvalidateVisual();
            return;
        }

        var hit = HitTestNode(point);
        if (hit != _viewModel.HoverNodeId)
        {
            _tooltipAnchor = point;
            _viewModel.SetHover(hit);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_panning)
        {
            _viewModel.SetHover(null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _panning = true;
        _panMoved = false;
        _panStart = e.GetPosition(this);
        _panStartOffsetX = _offsetX;
        _panStartOffsetY = _offsetY;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_panning)
        {
            return;
        }
        _panning = false;
        e.Pointer.Capture(null);
        if (_panMoved)
        {
            return;
        }

        var point = e.GetPosition(this);
        var nodeId = HitTestNode(point);
        if (nodeId is not { } id || !_viewModel.Tree.Nodes.TryGetValue(id, out var node))
        {
            return;
        }
        _tooltipAnchor = point;
        _viewModel.SetHover(id);
        if (node.Type == AtlasNodeType.ClusterIcon)
        {
            ShowSimilarClusters(id);
        }
        else if (!_viewModel.IsAllocated(id) && !_viewModel.HoverPath.IsEmpty)
        {
            _viewModel.AllocateHoverPath();
        }
        else
        {
            _viewModel.ToggleNode(id);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var point = e.GetPosition(this);
        var before = ScreenToTree(point);
        _scale = Math.Clamp(
            _scale * Math.Pow(1.2, e.Delta.Y),
            _fitScale * MinZoomFactor,
            _fitScale * MaxZoomFactor);
        _offsetX = point.X - before.X * _scale;
        _offsetY = point.Y - before.Y * _scale;
        InvalidateVisual();
        e.Handled = true;
    }

    private void ShowSimilarClusters(int nodeId)
    {
        if (!_viewModel.HighlightSimilarClusters(nodeId))
        {
            return;
        }
        _categoryHighlightTimer ??= CreateCategoryHighlightTimer();
        _categoryHighlightTimer.Stop();
        _categoryHighlightTimer.Start();
    }

    private DispatcherTimer CreateCategoryHighlightTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1250) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _viewModel.ClearClusterHighlights();
        };
        return timer;
    }

    private void DrawTooltip(DrawingContext context)
    {
        if (_viewModel.HoverNode is not { } node || node.Type == AtlasNodeType.Start)
        {
            return;
        }

        const double contentWidth = 440;
        var lines = new List<(FormattedText Text, IBrush Brush)>();
        AddWrapped(lines, string.IsNullOrWhiteSpace(node.Name) ? "Atlas Start" : node.Name, 19, TooltipTitleBrush, contentWidth);
        foreach (var stat in node.Stats)
        {
            AddWrapped(lines, stat, 14, TooltipStatBrush, contentWidth);
        }
        foreach (var reminder in node.ReminderText)
        {
            AddWrapped(lines, reminder, 12, TooltipReminderBrush, contentWidth);
        }
        foreach (var flavour in node.FlavourText)
        {
            AddWrapped(lines, flavour, 13, TooltipFlavourBrush, contentWidth);
        }
        if (lines.Count == 0)
        {
            return;
        }

        var width = Math.Min(contentWidth + 24, Math.Max(260, lines.Max(line => line.Text.Width) + 24));
        var height = lines.Sum(line => line.Text.Height + 3) + 18;
        var x = _tooltipAnchor.X + 18;
        var y = _tooltipAnchor.Y + 18;
        if (x + width > Bounds.Width - 8)
        {
            x = _tooltipAnchor.X - width - 18;
        }
        if (y + height > Bounds.Height - 8)
        {
            y = Bounds.Height - height - 8;
        }
        x = Math.Clamp(x, 8, Math.Max(8, Bounds.Width - width - 8));
        y = Math.Clamp(y, 8, Math.Max(8, Bounds.Height - height - 8));
        var rect = new Rect(x, y, width, height);
        context.FillRectangle(TooltipBackgroundBrush, rect);
        context.DrawRectangle(null, TooltipBorderPen, rect);
        var lineY = y + 9;
        foreach (var line in lines)
        {
            context.DrawText(line.Text, new Point(x + 12, lineY));
            lineY += line.Text.Height + 3;
        }
    }

    private static void AddWrapped(
        ICollection<(FormattedText Text, IBrush Brush)> destination,
        string text,
        double fontSize,
        IBrush brush,
        double maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = string.Empty;
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            var formatted = Format(candidate, fontSize, brush);
            if (line.Length > 0 && formatted.Width > maxWidth)
            {
                destination.Add((Format(line, fontSize, brush), brush));
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
        {
            destination.Add((Format(line, fontSize, brush), brush));
        }
    }

    private static FormattedText Format(string text, double size, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

    private static bool CircleIntersects(Rect rect, double x, double y, double radius) =>
        new Rect(x - radius, y - radius, radius * 2, radius * 2).Intersects(rect);

    private sealed class AtlasConnectorDrawOperation(
        Rect bounds,
        IReadOnlyList<Connector> connectors,
        Rect visibleTree,
        IReadOnlySet<int> allocated,
        IReadOnlySet<(int Min, int Max)> hoverEdges,
        double scale,
        double offsetX,
        double offsetY) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point point) => bounds.Contains(point);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var saved = canvas.Save();
            canvas.Translate((float)offsetX, (float)offsetY);
            canvas.Scale((float)scale);
            using var normal = Paint(new SKColor(0x40, 0x40, 0x48, 0x70), (float)Math.Max(0.5 / scale, ConnectorThicknessTree));
            using var active = Paint(new SKColor(0xFF, 0xC8, 0x4A, 0xFF), normal.StrokeWidth);
            using var hover = Paint(new SKColor(0xFF, 0xC8, 0x4A, 0x90), normal.StrokeWidth);
            foreach (var connector in connectors)
            {
                if (!ConnectorIntersects(connector, visibleTree))
                {
                    continue;
                }
                var edge = (Math.Min(connector.FromId, connector.ToId), Math.Max(connector.FromId, connector.ToId));
                var paint = allocated.Contains(connector.FromId) && allocated.Contains(connector.ToId)
                    ? active
                    : hoverEdges.Contains(edge) ? hover : normal;
                switch (connector)
                {
                    case LineConnector line:
                        canvas.DrawLine((float)line.X1, (float)line.Y1, (float)line.X2, (float)line.Y2, paint);
                        break;
                    case ArcConnector arc:
                        var radius = (float)arc.Radius;
                        canvas.DrawArc(
                            new SKRect((float)arc.Cx - radius, (float)arc.Cy - radius, (float)arc.Cx + radius, (float)arc.Cy + radius),
                            (float)(arc.StartAngle * 180 / Math.PI - 90),
                            (float)(arc.SweepAngle * 180 / Math.PI),
                            false,
                            paint);
                        break;
                }
            }
            canvas.RestoreToCount(saved);
        }

        private static bool ConnectorIntersects(Connector connector, Rect visibleTree) => connector switch
        {
            LineConnector line => new Rect(
                Math.Min(line.X1, line.X2) - HitMaxRadius,
                Math.Min(line.Y1, line.Y2) - HitMaxRadius,
                Math.Abs(line.X2 - line.X1) + HitMaxRadius * 2,
                Math.Abs(line.Y2 - line.Y1) + HitMaxRadius * 2).Intersects(visibleTree),
            ArcConnector arc => new Rect(
                arc.Cx - arc.Radius,
                arc.Cy - arc.Radius,
                arc.Radius * 2,
                arc.Radius * 2).Intersects(visibleTree),
            _ => true,
        };

        private static SKPaint Paint(SKColor color, float width) => new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            IsAntialias = true,
            Color = color,
        };
    }
}
