using System;
using System.Collections.Generic;
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

    public string Slot { get; }
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

    private ItemViewModel(ImportedItem item, string? slotOverride = null)
    {
        Slot = slotOverride ?? item.Slot;
        Name = item.Name;
        BaseType = item.BaseType;
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

    public static ItemViewModel FromImported(ImportedItem item, string? slotOverride = null) => new(item, slotOverride);

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
        var rawLines = item.RawText.Split('\n');
        var i = SkipHeaderLines(rawLines, item.Rarity);

        var implicits = new List<BodyLine>();
        var body = new List<BodyLine>();
        var statusFlags = new List<BodyLine>();

        var implicitCount = -1;
        var implicitsSeen = 0;

        for (; i < rawLines.Length; i++)
        {
            var line = rawLines[i].Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("Unique ID:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.Contains("BasePercentile: ", StringComparison.OrdinalIgnoreCase))
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
                body.Add(ParseModLine(line));
            }
            else if (implicitsSeen < implicitCount)
            {
                implicits.Add(ParseModLine(line));
                implicitsSeen++;
            }
            else
            {
                body.Add(ParseModLine(line));
            }
        }

        return (implicits, body, statusFlags);
    }

    private static int SkipHeaderLines(string[] lines, string rarity)
    {
        var i = 0;

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        if (i < lines.Length && lines[i].Trim().StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase))
        {
            i++;
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        if (i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            i++;
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        var ru = rarity.ToUpperInvariant();
        if ((ru == "RARE" || ru == "UNIQUE")
            && i < lines.Length
            && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }

    private static bool IsStatusFlag(string line) =>
        line is "Corrupted" or "Split" or "Mirrored" or "Fractured Item";

    private static BodyLine ParseModLine(string line)
    {
        if (line == "--------")
        {
            return new BodyLine("---", BrushSeparator);
        }

        var (text, brush) = StripPrefixes(line);
        return new BodyLine(text, brush);
    }

    // Strips all leading {tag} tokens. {range:X} is silent metadata; other tags set color.
    private static (string Text, IBrush Brush) StripPrefixes(string line)
    {
        var brush = BrushDefault;
        var span = line.AsSpan();

        while (span.StartsWith("{", StringComparison.Ordinal))
        {
            var close = span.IndexOf('}');
            if (close < 0)
            {
                break;
            }

            var tag = span[1..close].ToString();
            span = span[(close + 1)..];

            if (tag.StartsWith("range:", StringComparison.Ordinal))
            {
                continue;
            }

            brush = tag switch
            {
                "crafted"   => BrushCrafted,
                "fractured" => BrushFractured,
                "scourge"   => BrushScourge,
                "crucible"  => BrushCrucible,
                _           => brush,
            };
        }

        return (span.ToString(), brush);
    }
}
