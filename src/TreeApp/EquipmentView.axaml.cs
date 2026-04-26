using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp;

public partial class EquipmentView : UserControl
{
    private readonly record struct BodyLine(string Text, Color Foreground);

    private static readonly Color ColorDefault   = Color.FromRgb(0xC0, 0xC0, 0xC0);
    private static readonly Color ColorSeparator = Color.FromRgb(0x44, 0x44, 0x55);
    private static readonly Color ColorCrafted   = Color.FromRgb(0x88, 0xBB, 0xFF);
    private static readonly Color ColorFractured = Color.FromRgb(0xA2, 0x91, 0x62);
    private static readonly Color ColorScourge   = Color.FromRgb(0xD0, 0x50, 0x30);
    private static readonly Color ColorCrucible  = Color.FromRgb(0xC8, 0x70, 0x40);
    private static readonly Color ColorStatus    = Color.FromRgb(0xD0, 0x40, 0x40);

    public EquipmentView()
    {
        InitializeComponent();
    }

    public void LoadItems(IReadOnlyList<ImportedItem> items)
    {
        var panel = this.FindControl<WrapPanel>("ItemsPanel")!;
        var scroller = this.FindControl<ScrollViewer>("Scroller")!;
        var empty = this.FindControl<TextBlock>("EmptyMessage")!;

        panel.Children.Clear();

        if (items.Count == 0)
        {
            scroller.IsVisible = false;
            empty.IsVisible = true;
            empty.Text = "No items in this build.";
            return;
        }

        foreach (var item in items)
        {
            panel.Children.Add(BuildItemCard(item));
        }

        scroller.IsVisible = true;
        empty.IsVisible = false;
    }

    public void ClearItems()
    {
        var panel = this.FindControl<WrapPanel>("ItemsPanel")!;
        var scroller = this.FindControl<ScrollViewer>("Scroller")!;
        var empty = this.FindControl<TextBlock>("EmptyMessage")!;

        panel.Children.Clear();
        scroller.IsVisible = false;
        empty.IsVisible = true;
        empty.Text = "Import a build to see equipment.";
    }

    private static Control BuildItemCard(ImportedItem item)
    {
        var (nameColor, borderColor) = item.Rarity.ToUpperInvariant() switch
        {
            "MAGIC"  => (Color.FromRgb(0x88, 0x88, 0xFF), Color.FromRgb(0x44, 0x44, 0xAA)),
            "RARE"   => (Color.FromRgb(0xFF, 0xFF, 0x77), Color.FromRgb(0x88, 0x88, 0x00)),
            "UNIQUE" => (Color.FromRgb(0xAF, 0x60, 0x25), Color.FromRgb(0x7A, 0x42, 0x18)),
            _        => (Color.FromRgb(0xC8, 0xC8, 0xC8), Color.FromRgb(0x55, 0x55, 0x55)),
        };

        var stack = new StackPanel { Spacing = 3 };

        stack.Children.Add(new TextBlock
        {
            Text = item.Slot,
            Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x99)),
            FontSize = 10,
            Margin = new Thickness(0, 0, 0, 1),
        });

        stack.Children.Add(new TextBlock
        {
            Text = item.Name,
            Foreground = new SolidColorBrush(nameColor),
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.Equals(item.Name, item.BaseType, StringComparison.Ordinal))
        {
            stack.Children.Add(new TextBlock
            {
                Text = item.BaseType,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var (implicits, body, statusFlags) = ParseBodySections(item);

        if (implicits.Count > 0)
        {
            stack.Children.Add(BuildLinePanel(implicits));
        }

        stack.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(borderColor),
            Margin = new Thickness(0, 4, 0, 4),
        });

        if (body.Count > 0)
        {
            stack.Children.Add(BuildLinePanel(body));
        }

        if (statusFlags.Count > 0)
        {
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(borderColor),
                Margin = new Thickness(0, 4, 0, 4),
            });
            stack.Children.Add(BuildLinePanel(statusFlags));
        }

        return new Border
        {
            Width = 260,
            Margin = new Thickness(6),
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x20)),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Top,
            Child = stack,
        };
    }

    private static StackPanel BuildLinePanel(IReadOnlyList<BodyLine> lines)
    {
        var panel = new StackPanel { Spacing = 1 };
        foreach (var bl in lines)
        {
            panel.Children.Add(new TextBlock
            {
                Text = bl.Text,
                Foreground = new SolidColorBrush(bl.Foreground),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        return panel;
    }

    // Splits the item body into implicit mods, everything else, and trailing status flags.
    // PoB stores `Implicits: N` followed by N implicit lines, then explicit lines — no separator.
    private static (IReadOnlyList<BodyLine> Implicits, IReadOnlyList<BodyLine> Body, IReadOnlyList<BodyLine> StatusFlags) ParseBodySections(ImportedItem item)
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
                body.Add(new BodyLine("---", ColorSeparator));
                continue;
            }

            if (IsStatusFlag(line))
            {
                statusFlags.Add(new BodyLine(line, ColorStatus));
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
        if ((ru == "RARE" || ru == "UNIQUE") && i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }

    private static bool IsStatusFlag(string line)
    {
        return line == "Corrupted" || line == "Split" || line == "Mirrored" || line == "Fractured Item";
    }

    private static BodyLine ParseModLine(string line)
    {
        if (line == "--------")
        {
            return new BodyLine("---", ColorSeparator);
        }

        var (text, color) = StripPrefixes(line);
        return new BodyLine(text, color);
    }

    // Strips all leading {tag} tokens from a mod line.
    // {range:X} is silent metadata; other tags determine the display color.
    private static (string Text, Color Color) StripPrefixes(string line)
    {
        var color = ColorDefault;
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

            color = tag switch
            {
                "crafted"   => ColorCrafted,
                "fractured" => ColorFractured,
                "scourge"   => ColorScourge,
                "crucible"  => ColorCrucible,
                _           => color,
            };
        }

        return (span.ToString(), color);
    }
}
