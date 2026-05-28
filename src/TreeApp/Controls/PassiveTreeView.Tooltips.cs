using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Controls;

public sealed partial class PassiveTreeView
{
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

        var paddingX = 12.0;
        var paddingY = 9.0;
        var maxWidth = AvailableTooltipWidth();
        var contentWidth = TooltipContentWidth(node.Name, 20, Typeface.Default, paddingX, maxWidth);
        var titleLines = CreateWrappedText(node.Name, contentWidth, 20, TooltipTitleBrush, Typeface.Default);
        var lines = BuildTooltipLines(node, contentWidth);

        DrawTooltip(ctx, titleLines, [], lines, contentWidth, paddingX, paddingY, maxWidth, TooltipBorderBrush);
    }

    private void DrawItemTooltip(DrawingContext ctx, ItemViewModel item)
    {
        var paddingX = 12.0;
        var paddingY = 9.0;
        var maxWidth = AvailableTooltipWidth();
        var contentWidth = TooltipContentWidth(item.Name, 20, Typeface.Default, paddingX, maxWidth);
        if (item.HasSeparateName)
        {
            contentWidth = Math.Max(contentWidth, TooltipContentWidth(item.BaseType, 14, Typeface.Default, paddingX, maxWidth));
        }
        var titleLines = CreateWrappedText(item.Name, contentWidth, 20, item.NameBrush, Typeface.Default);
        var subtitleLines = item.HasSeparateName
            ? CreateWrappedText(item.BaseType, contentWidth, 14, TooltipTitleBrush, Typeface.Default)
            : [];
        var lines = BuildItemTooltipLines(item, contentWidth);

        DrawTooltip(ctx, titleLines, subtitleLines, lines, contentWidth, paddingX, paddingY, maxWidth, item.BorderBrush);
    }

    private void DrawTooltip(
        DrawingContext ctx,
        IReadOnlyList<FormattedText> titleLines,
        IReadOnlyList<FormattedText> subtitleLines,
        List<TooltipLine> bodyLines,
        double contentWidth,
        double paddingX,
        double paddingY,
        double maxWidth,
        IBrush borderBrush)
    {
        var width = TooltipWidth(contentWidth, paddingX, maxWidth, titleLines, subtitleLines, bodyLines);
        var headerHeight = TooltipHeaderHeight(titleLines, subtitleLines);
        var height = TooltipHeight(headerHeight, paddingY, bodyLines);
        var rect = PlaceTooltip(width, height);

        ctx.FillRectangle(TooltipFillBrush, rect);
        ctx.FillRectangle(TooltipHeaderBrush, new Rect(rect.X, rect.Y, rect.Width, headerHeight));
        ctx.DrawRectangle(null, new Pen(borderBrush, 1.5), rect);
        ctx.DrawLine(new Pen(borderBrush, 1), new Point(rect.X, rect.Y + headerHeight), new Point(rect.Right, rect.Y + headerHeight));

        DrawCenteredTextLines(ctx, titleLines, subtitleLines, rect.X, rect.Y, rect.Width, headerHeight);
        DrawTooltipBody(ctx, bodyLines, rect.X + paddingX, rect.Y + headerHeight + paddingY, rect.Width - paddingX * 2);
    }

    private Rect PlaceTooltip(double width, double height)
    {
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
        return new Rect(x, y, width, height);
    }

    private static double TooltipHeight(double headerHeight, double paddingY, IEnumerable<TooltipLine> bodyLines)
    {
        var height = headerHeight + paddingY;
        foreach (var line in bodyLines)
        {
            if (line.IsGap)
            {
                height += 8;
                continue;
            }

            if (line.IsSeparator)
            {
                height += 10;
                continue;
            }

            height += line.Text.Height + 2;
        }
        return height + paddingY;
    }

    private static void DrawTooltipBody(DrawingContext ctx, IEnumerable<TooltipLine> lines, double x, double lineY, double width)
    {
        foreach (var line in lines)
        {
            if (line.IsGap)
            {
                lineY += 8;
                continue;
            }

            if (line.IsSeparator)
            {
                lineY += 4;
                ctx.DrawLine(new Pen(TooltipBorderBrush, 1), new Point(x, lineY), new Point(x + width, lineY));
                lineY += 6;
                continue;
            }

            ctx.DrawText(line.Text, new Point(x, lineY));
            lineY += line.Text.Height + 2;
        }
    }

    private double AvailableTooltipWidth()
    {
        var available = Bounds.Width > 0 ? Bounds.Width - 16 : 560;
        return Math.Max(260, Math.Min(560, available));
    }

    private static double TooltipContentWidth(string text, double size, Typeface typeface, double paddingX, double maxWidth)
    {
        var preferredContentWidth = Math.Max(380, CreateText(text, size, Brushes.Transparent, typeface).Width);
        return Math.Clamp(preferredContentWidth, 260 - paddingX * 2, maxWidth - paddingX * 2);
    }

    private static double TooltipWidth(
        double contentWidth,
        double paddingX,
        double maxWidth,
        IReadOnlyList<FormattedText> titleLines,
        IReadOnlyList<FormattedText> subtitleLines,
        List<TooltipLine> bodyLines)
    {
        var measuredContentWidth = Math.Max(contentWidth, MaxTextWidth(titleLines));
        measuredContentWidth = Math.Max(measuredContentWidth, MaxTextWidth(subtitleLines));
        measuredContentWidth = Math.Max(measuredContentWidth, MaxTextWidth(bodyLines));
        return Math.Clamp(measuredContentWidth + paddingX * 2, 260, maxWidth);
    }

    private static double TooltipHeaderHeight(
        IReadOnlyList<FormattedText> titleLines,
        IReadOnlyList<FormattedText> subtitleLines)
    {
        var separator = titleLines.Count > 0 && subtitleLines.Count > 0 ? 1 : 0;
        return Math.Max(subtitleLines.Count > 0 ? 48 : 32, TextBlockHeight(titleLines) + separator + TextBlockHeight(subtitleLines) + 10);
    }

    private static void DrawCenteredTextLines(
        DrawingContext ctx,
        IReadOnlyList<FormattedText> titleLines,
        IReadOnlyList<FormattedText> subtitleLines,
        double x,
        double y,
        double width,
        double height)
    {
        var textHeight = TextBlockHeight(titleLines) + TextBlockHeight(subtitleLines);
        if (titleLines.Count > 0 && subtitleLines.Count > 0)
        {
            textHeight += 1;
        }

        var lineY = y + (height - textHeight) * 0.5 - 1;
        foreach (var line in titleLines)
        {
            ctx.DrawText(line, new Point(x + (width - line.Width) * 0.5, lineY));
            lineY += line.Height + 2;
        }

        if (titleLines.Count > 0 && subtitleLines.Count > 0)
        {
            lineY -= 1;
        }

        foreach (var line in subtitleLines)
        {
            ctx.DrawText(line, new Point(x + (width - line.Width) * 0.5, lineY));
            lineY += line.Height + 2;
        }
    }

    private static double MaxTextWidth(IReadOnlyList<FormattedText> lines)
    {
        return lines
            .Select(line => line.Width)
            .Prepend(0.0)
            .Max();
    }

    private static double MaxTextWidth(IEnumerable<TooltipLine> lines)
    {
        var max = 0.0;
        foreach (var line in lines)
        {
            if (!line.IsGap)
            {
                max = Math.Max(max, line.Text.Width);
            }
        }
        return max;
    }

    private static double TextBlockHeight(IReadOnlyList<FormattedText> lines)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        var height = 0.0;
        foreach (var line in lines)
        {
            height += line.Height + 2;
        }
        return height - 2;
    }

    private List<TooltipLine> BuildTooltipLines(Node node, double contentWidth)
    {
        var lines = new List<TooltipLine>();

        var passiveLines = PassiveEffectLines(node).ToArray();
        var statLinkSpans = passiveLines.SequenceEqual(node.Stats) && node.StatLinkSpans.Count == node.Stats.Count
            ? node.StatLinkSpans
            : null;
        AddWrappedLines(lines, passiveLines, contentWidth, TooltipStatBrush, 14, Typeface.Default, linkSpans: statLinkSpans);
        AddWeaponSetTooltipLine(lines, node, contentWidth);
        AddAllocationPreviewLines(lines, node, contentWidth);
        AddWrappedLines(lines, node.FlavourText, contentWidth, TooltipFlavourBrush, 14,
            new Typeface(FontFamily.Default, FontStyle.Italic, FontWeight.Normal));
        AddWrappedLines(lines, node.ReminderText, contentWidth, TooltipReminderBrush, 12, Typeface.Default, gapBefore: lines.Count > 0);
        AddDiffTooltipLine(lines, node, contentWidth);

        return lines;
    }

    private void AddWeaponSetTooltipLine(List<TooltipLine> lines, Node node, double contentWidth)
    {
        var (text, brush) = _vm.AllocationSetOf(node.Id) switch
        {
            PassiveAllocationSet.WeaponSet1 => ("Weapon Set 1 allocation", WeaponSet1Brush),
            PassiveAllocationSet.WeaponSet2 => ("Weapon Set 2 allocation", WeaponSet2Brush),
            _ => (null, null),
        };
        if (text is null || brush is null)
        {
            return;
        }
        AddWrappedLines(lines, [text], contentWidth, brush, 13, Typeface.Default, gapBefore: lines.Count > 0);
    }

    private void AddDiffTooltipLine(List<TooltipLine> lines, Node node, double contentWidth)
    {
        if (!_vm.Diff.CurrentNodeDiffs.TryGetValue(node.Id, out var diff))
        {
            return;
        }

        var prefix = string.IsNullOrWhiteSpace(_vm.Diff.BaselineVersion)
            ? "Diff"
            : $"Diff vs {_vm.Diff.BaselineVersion}";
        IReadOnlyList<string> diffLines;
        IBrush brush;
        if (diff.Kind == TreeNodeDiffKind.Added)
        {
            diffLines = ["Added"];
            brush = DiffAddedBrush;
        }
        else if (_vm.Diff.BaselineNodes.TryGetValue(node.Id, out var baselineNode))
        {
            diffLines = BaselineStatLines(baselineNode);
            brush = TooltipStatBrush;
        }
        else
        {
            diffLines = ["Changed"];
            brush = DiffChangedBrush;
        }

        if (lines.Count > 0)
        {
            lines.Add(TooltipLine.Separator);
        }

        AddWrappedLines(lines, [$"{prefix}:"], contentWidth, TooltipReminderBrush, 13, Typeface.Default);
        AddWrappedLines(lines, diffLines, contentWidth, brush, 13, Typeface.Default);
    }

    private static IReadOnlyList<string> BaselineStatLines(Node baselineNode)
    {
        if (baselineNode.Stats.Count > 0)
        {
            return baselineNode.Stats;
        }

        return ["No stats"];
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

        return _vm.PassiveEffectLines(node);
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
        bool gapBefore = false,
        IReadOnlyList<IReadOnlyList<TextSpan>>? linkSpans = null)
    {
        var added = false;
        var sourceIndex = 0;
        foreach (var raw in source)
        {
            var rawLinkSpans = linkSpans is not null && sourceIndex < linkSpans.Count
                ? linkSpans[sourceIndex]
                : null;
            foreach (var line in WrapText(raw, maxWidth, size, typeface, brush, rawLinkSpans))
            {
                if (!added && gapBefore)
                {
                    lines.Add(TooltipLine.Gap);
                }
                lines.Add(new TooltipLine(CreateText(line.Text, size, brush, typeface, line.Underlines)));
                added = true;
            }
            sourceIndex++;
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
                lines.Add(new TooltipLine(CreateText(line.Text, 14, raw.Brush)));
                added = true;
            }
        }
    }

    private static IEnumerable<WrappedTextLine> WrapText(
        string text,
        double maxWidth,
        double size,
        Typeface typeface,
        IBrush brush,
        IReadOnlyList<TextSpan>? underlines = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var current = string.Empty;
        var currentStart = 0;
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && text[index] == ' ')
            {
                index++;
            }
            if (index >= text.Length)
            {
                break;
            }

            var wordStart = index;
            while (index < text.Length && text[index] != ' ')
            {
                index++;
            }

            var word = text[wordStart..index];
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (CreateText(candidate, size, brush, typeface).Width <= maxWidth || current.Length == 0)
            {
                if (current.Length == 0)
                {
                    currentStart = wordStart;
                }
                current = candidate;
                continue;
            }

            yield return new WrappedTextLine(current, ClipTextSpans(underlines, currentStart, current.Length));
            current = word;
            currentStart = wordStart;
        }
        if (current.Length > 0)
        {
            yield return new WrappedTextLine(current, ClipTextSpans(underlines, currentStart, current.Length));
        }
    }

    private static IReadOnlyList<TextSpan> ClipTextSpans(IReadOnlyList<TextSpan>? spans, int lineStart, int lineLength)
    {
        if (spans is null || spans.Count == 0)
        {
            return Array.Empty<TextSpan>();
        }

        var lineEnd = lineStart + lineLength;
        var result = new List<TextSpan>();
        foreach (var span in spans)
        {
            var spanStart = span.Start;
            var spanEnd = span.Start + span.Length;
            var start = Math.Max(spanStart, lineStart);
            var end = Math.Min(spanEnd, lineEnd);
            if (end > start)
            {
                result.Add(new TextSpan(start - lineStart, end - start));
            }
        }

        return result;
    }

    private static List<FormattedText> CreateWrappedText(
        string text,
        double maxWidth,
        double size,
        IBrush brush,
        Typeface typeface)
    {
        var lines = new List<FormattedText>();
        foreach (var line in WrapText(text, maxWidth, size, typeface, brush))
        {
            lines.Add(CreateText(line.Text, size, brush, typeface));
        }
        return lines;
    }

    private static FormattedText CreateText(
        string text,
        double size,
        IBrush brush,
        Typeface? typeface = null,
        IReadOnlyList<TextSpan>? underlines = null)
    {
        var formattedText = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface ?? Typeface.Default, size, brush);
        foreach (var underline in underlines ?? [])
        {
            if (underline.Start >= 0 && underline.Length > 0 && underline.Start + underline.Length <= text.Length)
            {
                formattedText.SetTextDecorations(TextDecorations.Underline, underline.Start, underline.Length);
            }
        }

        return formattedText;
    }

    private readonly record struct TooltipLine(FormattedText Text, bool IsGap = false, bool IsSeparator = false)
    {
        public static TooltipLine Gap { get; } = new(CreateText(string.Empty, 1, Brushes.Transparent), IsGap: true);
        public static TooltipLine Separator { get; } = new(CreateText(string.Empty, 1, Brushes.Transparent), IsSeparator: true);
    }

    private readonly record struct WrappedTextLine(string Text, IReadOnlyList<TextSpan> Underlines);
}
