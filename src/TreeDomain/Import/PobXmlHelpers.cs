using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

internal static class PobXmlHelpers
{
    public static XElement? TryExtractElement(string xml, string tagName)
    {
        var start = FindTagBoundary(xml, tagName, 0);
        if (start < 0)
        {
            return null;
        }

        var closeTag = "</" + tagName + ">";
        var end = xml.IndexOf(closeTag, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        try
        {
            return XElement.Parse(xml[start..(end + closeTag.Length)]);
        }
        catch
        {
            return null;
        }
    }

    public static int FindTagBoundary(string xml, string tag, int start)
    {
        var search = "<" + tag;
        var pos = start;
        while (true)
        {
            var found = xml.IndexOf(search, pos, StringComparison.Ordinal);
            if (found < 0)
            {
                return -1;
            }

            var after = found + search.Length;
            if (after >= xml.Length)
            {
                return -1;
            }

            var c = xml[after];
            if (c == ' ' || c == '\t' || c == '>' || c == '/' || c == '\r' || c == '\n')
            {
                return found;
            }

            pos = after;
        }
    }

    public static string DisplayName(XElement element, string prefix, int index)
    {
        var title = ((string?)element.Attribute("title"))?.Trim();
        if (!string.IsNullOrEmpty(title))
        {
            return title;
        }

        var name = ((string?)element.Attribute("name"))?.Trim();
        return !string.IsNullOrEmpty(name) ? name : $"{prefix} {index + 1}";
    }

    public static int OneBasedToZero(int value) => value > 0 ? value - 1 : 0;

    public static string? AttrString(XElement element, string name)
    {
        var value = ((string?)element.Attribute(name))?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static int? AttrInt(XElement element, string name) =>
        int.TryParse((string?)element.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    public static bool AttrBool(XElement element, string name, bool defaultValue)
    {
        var raw = ((string?)element.Attribute(name))?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return defaultValue;
        }

        return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    public static string StatLabel(string stat)
    {
        if (string.IsNullOrWhiteSpace(stat))
        {
            return string.Empty;
        }

        var spaced = Regex.Replace(stat, "([a-z])([A-Z])", "$1 $2");
        return spaced.Replace('_', ' ');
    }
}
