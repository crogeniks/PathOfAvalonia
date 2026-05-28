using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;

namespace PathOfAvalonia.TreeApp.Controls;

public sealed partial class PassiveTreeView
{
    private void DrawNode(DrawingContext ctx, Node n, bool alloc, bool hover, bool useClusterSocketFrame)
    {
        var screen = TreeToScreen(n.X, n.Y);
        var allocationSet = alloc ? _vm.AllocationSetOf(n.Id) : PassiveAllocationSet.Normal;
        if (TryDrawGameSpecificNode(ctx, n, alloc, hover, screen))
        {
            DrawWeaponSetMarker(ctx, n, allocationSet, screen);
            if (n.Type == NodeType.JewelSocket && _vm.SocketedJewelOverlayAt(n) is { } jewelOverlay)
            {
                DrawSocketedJewelOverlay(ctx, jewelOverlay, screen);
            }
            return;
        }

        var (iconAtlas, iconPath) = IconSprite(n, alloc, hover: false);
        if (iconAtlas is not null && iconPath is not null)
        {
            _ = DrawSprite(ctx, iconAtlas, iconPath, screen);
        }
        if (hover && !alloc && n.Type == NodeType.Mastery && n.InactiveIcon is { } ii)
        {
            _ = DrawSprite(ctx, "masteryConnected", ii, screen);
        }

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

        if (!drewFrame && (frameKey is not null || iconAtlas is null))
        {
            var r = NodeRadius * _scale;
            var outline = allocationSet == PassiveAllocationSet.Normal
                ? NodeOutlinePen
                : new Pen(BrushForAllocationSet(allocationSet), Math.Max(1.5, 5.0 * _scale));
            ctx.DrawEllipse(alloc ? AllocatedBrush : NormalBrush, outline, screen, r, r);
            if (allocationSet != PassiveAllocationSet.Normal)
            {
                ctx.DrawEllipse(null, NodeOutlinePen, screen, r * 0.84, r * 0.84);
            }
        }
    }

    private void DrawWeaponSetMarker(DrawingContext ctx, Node node, PassiveAllocationSet allocationSet, Point centre)
    {
        if (allocationSet == PassiveAllocationSet.Normal)
        {
            return;
        }
        var half = node.Visual is not null
            ? Poe2FrameHalfSize(node, string.Empty)
            : new Size(NodeRadius, NodeRadius);
        var rx = Math.Max(NodeRadius * _scale, half.Width * _scale * 0.82);
        var ry = Math.Max(NodeRadius * _scale, half.Height * _scale * 0.82);
        var thickness = Math.Max(1.5, 6.0 * _scale);
        ctx.DrawEllipse(null, new Pen(BrushForAllocationSet(allocationSet), thickness), centre, rx, ry);
    }

    private static IBrush BrushForAllocationSet(PassiveAllocationSet allocationSet) => allocationSet switch
    {
        PassiveAllocationSet.WeaponSet1 => WeaponSet1Brush,
        PassiveAllocationSet.WeaponSet2 => WeaponSet2Brush,
        _ => AllocatedBrush,
    };

    private bool TryDrawGameSpecificNode(DrawingContext ctx, Node node, bool allocated, bool hover, Point screen)
    {
        if (node.Visual is not { } visual)
        {
            return false;
        }

        var drewAny = false;
        if (!string.IsNullOrWhiteSpace(visual.Icon))
        {
            drewAny |= DrawPoe2NodeIcon(ctx, visual.Icon, screen, node);
        }

        var drewFrame = DrawPoe2FrameSprite(ctx, visual, node, allocated, hover, screen);
        if (!drewFrame)
        {
            drewFrame = DrawPoe2VectorFrame(ctx, node, allocated, hover, screen);
        }
        drewAny |= drewFrame;

        return drewAny;
    }

    private bool DrawPoe2FrameSprite(DrawingContext ctx, NodeVisual visual, Node node, bool allocated, bool hover, Point screen)
    {
        foreach (var frame in SelectPoe2FrameCandidates(visual, node.Type, allocated, hover))
        {
            if (DrawPoe2Sprite(ctx, "poe2Frames", frame, screen, Poe2FrameHalfSize(node, frame)))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> SelectPoe2FrameCandidates(NodeVisual visual, NodeType type, bool allocated, bool hover)
    {
        if (allocated && !string.IsNullOrWhiteSpace(visual.AllocatedFrame))
        {
            yield return visual.AllocatedFrame;
        }
        if (hover && !string.IsNullOrWhiteSpace(visual.HoverFrame))
        {
            yield return visual.HoverFrame;
        }
        if (!string.IsNullOrWhiteSpace(visual.UnallocatedFrame))
        {
            yield return visual.UnallocatedFrame;
        }
        if (Poe2DefaultFrameKey(type, allocated, hover) is { } stateFrame)
        {
            yield return stateFrame;
        }
        if (Poe2DefaultFrameKey(type, allocated: false, hover: false) is { } baseFrame)
        {
            yield return baseFrame;
        }
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
        if (!TryGetSprite(atlasKey, spriteKey, out var bitmap, out var spriteRect))
        {
            return false;
        }

        var halfW = halfSizeTree.Width * _scale;
        var halfH = halfSizeTree.Height * _scale;
        var dst = new Rect(centre.X - halfW, centre.Y - halfH, halfW * 2, halfH * 2);
        DrawSpriteImage(ctx, bitmap, spriteRect, dst);
        return true;
    }

    private bool DrawPoe2NodeIcon(DrawingContext ctx, string spriteKey, Point centre, Node node)
    {
        var halfSizeTree = Poe2IconHalfSize(node);
        var clipRadiusTree = Poe2IconClipRadius(node);
        if (clipRadiusTree <= 0)
        {
            return DrawPoe2Sprite(ctx, "poe2NodeIcons", spriteKey, centre, halfSizeTree);
        }

        var clipRadius = clipRadiusTree * _scale;
        var clip = new EllipseGeometry(new Rect(
            centre.X - clipRadius,
            centre.Y - clipRadius,
            clipRadius * 2,
            clipRadius * 2));
        using (ctx.PushGeometryClip(clip))
        {
            return DrawPoe2Sprite(ctx, "poe2NodeIcons", spriteKey, centre, halfSizeTree);
        }
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

    private static double Poe2IconClipRadius(Node node) => node.Type switch
    {
        NodeType.Keystone => 75,
        NodeType.Notable => 50,
        NodeType.JewelSocket => 58,
        NodeType.Normal => 34,
        _ when node.AscendancyName is not null => 50,
        _ => 0,
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

    private static string? Poe2DefaultFrameKey(NodeType type, bool allocated, bool hover) =>
        BaseFrameKey(type, allocated, hover);

    private bool DrawSprite(DrawingContext ctx, string atlasKey, string spriteKey, Point centre)
    {
        if (!TryGetSprite(atlasKey, spriteKey, out var bitmap, out var spriteRect))
        {
            return false;
        }
        var halfW = spriteRect.W * SpriteDisplayScale * _scale;
        var halfH = spriteRect.H * SpriteDisplayScale * _scale;
        var dst = new Rect(centre.X - halfW, centre.Y - halfH, halfW * 2, halfH * 2);
        DrawSpriteImage(ctx, bitmap, spriteRect, dst);
        return true;
    }

    private bool TryGetSprite(string atlasKey, string spriteKey, out Bitmap bitmap, out SpriteRect spriteRect)
    {
        bitmap = null!;
        spriteRect = default;
        if (!_sprites.Atlases.TryGetValue(atlasKey, out var atlas)
            || !atlas.Coords.TryGetValue(spriteKey, out spriteRect)
            || !_atlasBitmaps.TryGetValue(atlas.File, out bitmap!))
        {
            return false;
        }

        return true;
    }

    private static void DrawSpriteImage(DrawingContext ctx, Bitmap bitmap, SpriteRect spriteRect, Rect dst)
    {
        var src = new Rect(spriteRect.X, spriteRect.Y, spriteRect.W, spriteRect.H);
        ctx.DrawImage(bitmap, src, dst);
    }

    private void DrawSocketedJewelOverlay(DrawingContext ctx, string spriteKey, Point centre)
    {
        if (_sprites.Lookup("poe2Jewels", spriteKey) is not null)
        {
            _ = DrawPoe2Sprite(ctx, "poe2Jewels", spriteKey, centre, new Size(76, 76));
            return;
        }
        if (spriteKey == "JewelSocketActiveLegion" && _sprites.Lookup("poe2Jewels", "Timeless Jewel") is not null)
        {
            _ = DrawPoe2Sprite(ctx, "poe2Jewels", "Timeless Jewel", centre, new Size(76, 76));
            return;
        }
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
        NodeType.Mastery when alloc && n.ActiveIcon is not null => ("masteryActiveSelected", n.ActiveIcon),
        NodeType.Mastery when n.InactiveIcon is not null => ("masteryInactive", n.InactiveIcon),
        NodeType.Mastery => ("mastery", n.Icon),
        _ => (null, null),
    };

    private static string? FrameKey(NodeType type, bool alloc, bool hover,
        ClusterJewelSize? clusterSize = null) =>
        type switch
        {
            NodeType.JewelSocket when clusterSize is { } size =>
                alloc ? "JewelFrameAllocated" :
                hover ? $"JewelSocketClusterAltCanAllocate1{SizeLabel(size)}" :
                        $"JewelSocketClusterAltNormal1{SizeLabel(size)}",
            _ => BaseFrameKey(type, alloc, hover),
        };

    private static string? BaseFrameKey(NodeType type, bool allocated, bool hover) => type switch
    {
        NodeType.Normal => allocated ? "PSSkillFrameActive" : hover ? "PSSkillFrameHighlighted" : "PSSkillFrame",
        NodeType.Notable => allocated ? "NotableFrameAllocated" : hover ? "NotableFrameCanAllocate" : "NotableFrameUnallocated",
        NodeType.Keystone => allocated ? "KeystoneFrameAllocated" : hover ? "KeystoneFrameCanAllocate" : "KeystoneFrameUnallocated",
        NodeType.JewelSocket => allocated ? "JewelFrameAllocated" : hover ? "JewelFrameCanAllocate" : "JewelFrameUnallocated",
        _ => null,
    };

    private static string SizeLabel(ClusterJewelSize size) => size switch
    {
        ClusterJewelSize.Large => "Large",
        ClusterJewelSize.Medium => "Medium",
        _ => "Small",
    };
}
