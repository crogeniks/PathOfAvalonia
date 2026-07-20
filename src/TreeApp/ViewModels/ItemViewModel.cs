using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed class ItemViewModel
{
    private readonly record struct BodyLine(string Text, IBrush Brush);

    private static readonly IBrush BrushDefault   = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
    private static readonly IBrush BrushSeparator = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x55));
    private static readonly IBrush BrushCrafted   = new SolidColorBrush(Color.FromRgb(0x88, 0xBB, 0xFF));
    private static readonly IBrush BrushFractured = new SolidColorBrush(Color.FromRgb(0xA2, 0x91, 0x62));
    private static readonly IBrush BrushScourge   = new SolidColorBrush(Color.FromRgb(0xD0, 0x50, 0x30));
    private static readonly IBrush BrushCrucible  = new SolidColorBrush(Color.FromRgb(0xC8, 0x70, 0x40));
    private static readonly IBrush BrushStatus    = new SolidColorBrush(Color.FromRgb(0xD0, 0x40, 0x40));
    private static readonly IBrush BrushEnchant   = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xFF));
    private static readonly IBrush BrushRune      = new SolidColorBrush(Color.FromRgb(0x70, 0xD8, 0xC8));
    private static readonly IBrush BrushVariant   = new SolidColorBrush(Color.FromRgb(0xC8, 0xA0, 0xFF));
    private static readonly IBrush BrushCustom    = new SolidColorBrush(Color.FromRgb(0xE0, 0xD0, 0x90));

    public ImportedItem Item { get; }
    public int ItemId { get; }
    public string Slot { get; }
    public string Rarity { get; }
    public string Name { get; }
    public IBrush NameBrush { get; }
    public string BaseType { get; }
    public bool HasSeparateName { get; }
    public IBrush BorderBrush { get; }
    public IReadOnlyList<ModLineViewModel> Implicits { get; }
    public bool HasImplicits { get; }
    public IReadOnlyList<ModLineViewModel> Body { get; }
    public IReadOnlyList<ModLineViewModel> StatusFlags { get; }
    public bool HasStatusFlags { get; }
    public string RawText { get; }
    public string UsageText { get; }
    public bool HasUsageText { get; }

    private ItemViewModel(ImportedItem item, string? slotOverride = null, string? usageText = null)
    {
        Item = item;
        ItemId = item.Id;
        Slot = slotOverride ?? item.Slot;
        Rarity = string.IsNullOrWhiteSpace(item.Rarity) ? "Normal" : item.Rarity;
        Name = item.Name;
        BaseType = item.BaseType;
        RawText = item.RawText;
        UsageText = usageText ?? string.Empty;
        HasUsageText = !string.IsNullOrWhiteSpace(UsageText);
        HasSeparateName = !string.Equals(item.Name, item.BaseType, StringComparison.Ordinal);

        IBrush nameBrush;
        IBrush borderBrush;
        (nameBrush, borderBrush) = item.Rarity.ToUpperInvariant() switch
        {
            "MAGIC"  => ((IBrush)new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xFF)),
                         (IBrush)new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0xAA))),
            "RARE"   => (new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x77)),
                         new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x00))),
            "UNIQUE" => (new SolidColorBrush(Color.FromRgb(0xAF, 0x60, 0x25)),
                         new SolidColorBrush(Color.FromRgb(0x7A, 0x42, 0x18))),
            _        => (new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
                         new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))),
        };
        NameBrush = nameBrush;
        BorderBrush = borderBrush;

        var (implicits, body, statusFlags) = ParseBodySections(item);
        Implicits = ToModLines(implicits);
        HasImplicits = Implicits.Count > 0;
        Body = ToModLines(body);
        StatusFlags = ToModLines(statusFlags);
        HasStatusFlags = StatusFlags.Count > 0;
    }

    public static ItemViewModel FromImported(ImportedItem item, string? slotOverride = null, string? usageText = null) =>
        new(item, slotOverride, usageText);

    private static IReadOnlyList<ModLineViewModel> ToModLines(IReadOnlyList<BodyLine> lines)
    {
        var result = new ModLineViewModel[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            result[i] = new ModLineViewModel { Text = lines[i].Text, Brush = lines[i].Brush };
        }
        return result;
    }

    // Splits item body into implicit mods, everything else, and trailing status flags.
    // PoB stores `Implicits: N` followed by N implicit lines, then explicit lines.
    private static (IReadOnlyList<BodyLine> Implicits, IReadOnlyList<BodyLine> Body, IReadOnlyList<BodyLine> StatusFlags)
        ParseBodySections(ImportedItem item)
    {
        var itemLines = item.Text.BodyLines;

        var implicits = new List<BodyLine>();
        var body = new List<BodyLine>();
        var statusFlags = new List<BodyLine>();

        var implicitCount = -1;
        var implicitsSeen = 0;

        if (item.Sockets.Count > 0)
        {
            body.Add(new BodyLine("Sockets: " + string.Join(" ", item.Sockets.Select(socket => socket.Kind)), BrushDefault));
        }
        foreach (var rune in item.Runes)
        {
            body.Add(new BodyLine("Rune: " + rune, BrushRune));
        }

        foreach (var itemLine in itemLines)
        {
            var line = itemLine.Text;

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("Unique ID:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("Variant:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Selected Variant:", StringComparison.OrdinalIgnoreCase)
                || !ItemVariant.IsActive(itemLine.Raw, item.SelectedVariant))
            {
                continue;
            }

            if (line.Contains("BasePercentile: ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("LevelReq:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Str:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Dex:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Int:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Requires Class ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Source:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Note:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("Sockets:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Rune:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line == "--------")
            {
                body.Add(new BodyLine("---", BrushSeparator));
                continue;
            }

            if (IsStatusFlag(line))
            {
                statusFlags.Add(new BodyLine(line, BrushStatus));
                continue;
            }

            if (implicitCount < 0)
            {
                if (line.StartsWith("Implicits:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line[10..].Trim(), out var n))
                {
                    implicitCount = n;
                    continue;
                }
                body.Add(ParseModLine(itemLine));
            }
            else if (implicitsSeen < implicitCount)
            {
                implicits.Add(ParseModLine(itemLine));
                implicitsSeen++;
            }
            else
            {
                body.Add(ParseModLine(itemLine));
            }
        }

        return (implicits, body, statusFlags);
    }

    private static bool IsStatusFlag(string line) =>
        line is "Corrupted" or "Split" or "Mirrored" or "Fractured Item" or "Desecrated" or "Unreleased";

    private static BodyLine ParseModLine(ItemTextLine line)
    {
        if (line.Text == "--------")
        {
            return new BodyLine("---", BrushSeparator);
        }

        return new BodyLine(line.Text, BrushForTags(line.Tags));
    }

    private static IBrush BrushForTags(IReadOnlyList<string> tags)
    {
        var brush = BrushDefault;
        foreach (var tag in tags)
        {
            if (tag.StartsWith("range:", StringComparison.Ordinal)
                || tag.StartsWith("tags:", StringComparison.Ordinal))
            {
                continue;
            }
            if (tag.StartsWith("variant:", StringComparison.Ordinal))
            {
                brush = BrushVariant;
                continue;
            }

            brush = tag switch
            {
                "crafted"   => BrushCrafted,
                "fractured" => BrushFractured,
                "scourge"   => BrushScourge,
                "crucible"  => BrushCrucible,
                "enchant"   => BrushEnchant,
                "rune"      => BrushRune,
                "custom"    => BrushCustom,
                _           => brush,
            };
        }
        return brush;
    }
}
