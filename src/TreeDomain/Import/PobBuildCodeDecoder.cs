using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

// Decodes a PoB share string: URL-safe base64 → zlib → XML.
// The build XML carries a <Build><Spec nodes="..." classId="..." ascendClassId="..."/> block;
// specs in the modern schema use the `nodes` attribute, older specs fall back to <URL>.
public static class PobBuildCodeDecoder
{
    public static ImportedBuild Decode(string code)
    {
        var xml = DecodeToXml(code);
        return ParseXml(xml);
    }

    public static bool LooksLikeBuildCode(string text)
    {
        var t = text.Trim();
        if (t.Length < 40)
        {
            return false;
        }
        if (t.Contains("pathofexile.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        foreach (var c in t)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '=' || c == '+' || c == '/'))
            {
                return false;
            }
        }
        return true;
    }

    internal static string DecodeToXml(string code)
    {
        var bytes = PobTreeUrlDecoder.Base64UrlDecode(code.Trim());
        // Try strict zlib first; some builds in the wild have a corrupt Adler32 trailer
        // (ZLibStream throws "unsupported compression method" then). Fall back to a raw
        // DeflateStream with the 2-byte zlib header skipped — that's what PoB's Lua
        // inflater effectively does since it doesn't verify the checksum.
        try
        {
            using var compressed = new MemoryStream(bytes);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            if (bytes.Length < 3)
            {
                throw;
            }
            using var compressed = new MemoryStream(bytes, 2, bytes.Length - 2);
            using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(deflate, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    internal static ImportedBuild ParseXml(string xml)
        => ParseXml(xml, "pob-code");

    internal static ImportedBuild ParseXml(string xml, string source)
    {
        var items = ParseItemsSection(xml);

        // PoB builds in the wild can have corrupt or unescaped content in later sections
        // (Notes, Skills names), so XDocument on the whole document may fail. We only need
        // the <Spec> block for tree import — pull each Spec substring out and parse it
        // as its own fragment, then pick the one Tree.activeSpec points at if present.
        var specElements = ExtractSpecElements(xml);
        if (specElements.Count == 0)
        {
            throw new InvalidDataException("PoB XML contains no <Spec> element");
        }
        var activeIndex = ExtractActiveSpecIndex(xml, specElements.Count);
        var spec = specElements[activeIndex];

        var classId = ParseIntOrZero((string?)spec.Attribute("classId"));
        var ascendClassId = ParseIntOrZero((string?)spec.Attribute("ascendClassId"));
        var secondaryAscendClassId = ParseIntOrZero((string?)spec.Attribute("secondaryAscendClassId"));
        var treeVersion = (string?)spec.Attribute("treeVersion");
        var clusterHashFormatVersion = ParseClusterHashFormatVersion(spec);
        var classInternalId = (string?)spec.Attribute("classInternalId");
        var ascendancyInternalId = (string?)spec.Attribute("ascendancyInternalId");
        var attributeOverrides = ParseAttributeOverrides(spec);

        var nodesAttr = (string?)spec.Attribute("nodes");
        if (!string.IsNullOrWhiteSpace(nodesAttr))
        {
            var (main, cluster) = SplitNodes(nodesAttr!);
            var masteries = ParseMasteryEffects((string?)spec.Attribute("masteryEffects"));
            return new ImportedBuild(
                ClassId: classId,
                AscendClassId: ascendClassId,
                SecondaryAscendClassId: secondaryAscendClassId,
                NodeHashes: main,
                ClusterNodeHashes: cluster,
                MasterySelections: masteries,
                TreeVersion: treeVersion,
                Source: source)
            {
                ClusterHashFormatVersion = clusterHashFormatVersion,
                ClassInternalId = classInternalId,
                AscendancyInternalId = ascendancyInternalId,
                AttributeOverrides = attributeOverrides,
                Items = items.ActiveItems,
                ItemsById = items.ItemsById,
                SocketedJewels = ParseSocketedJewels(spec),
            };
        }

        var urlElement = spec.Element("URL");
        if (urlElement is null)
        {
            throw new InvalidDataException("Spec has neither 'nodes' attribute nor <URL> child");
        }
        var embedded = PobTreeUrlDecoder.Decode(urlElement.Value);
        return embedded with
        {
            TreeVersion = treeVersion,
            Source = source,
            ClusterHashFormatVersion = clusterHashFormatVersion,
            ClassInternalId = classInternalId,
            AscendancyInternalId = ascendancyInternalId,
            AttributeOverrides = attributeOverrides,
            Items = items.ActiveItems,
            ItemsById = items.ItemsById,
            SocketedJewels = ParseSocketedJewels(spec),
        };
    }

    private static List<XElement> ExtractSpecElements(string xml)
    {
        var result = new List<XElement>();
        var idx = 0;
        while (true)
        {
            // Match `<Spec` followed by a space, '>', or '/'; skip false positives like "<Specs".
            var found = -1;
            while ((found = xml.IndexOf("<Spec", idx, StringComparison.Ordinal)) >= 0)
            {
                var after = found + 5;
                if (after >= xml.Length)
                {
                    return result;
                }
                var c = xml[after];
                if (c == ' ' || c == '\t' || c == '>' || c == '/' || c == '\r' || c == '\n')
                {
                    break;
                }
                idx = after;
            }
            if (found < 0)
            {
                return result;
            }
            var tagEnd = xml.IndexOf('>', found);
            if (tagEnd < 0)
            {
                return result;
            }
            // Self-closing <Spec ... />
            if (xml[tagEnd - 1] == '/')
            {
                var fragment = xml.Substring(found, tagEnd - found + 1);
                if (TryParseSpec(fragment, out var el))
                {
                    result.Add(el);
                }
                idx = tagEnd + 1;
                continue;
            }
            var close = xml.IndexOf("</Spec>", tagEnd, StringComparison.Ordinal);
            if (close < 0)
            {
                // Unterminated — parse the open tag as if it were self-closing so we still
                // capture attributes.
                var openOnly = xml.Substring(found, tagEnd - found) + "/>";
                if (TryParseSpec(openOnly, out var el))
                {
                    result.Add(el);
                }
                return result;
            }
            var end = close + "</Spec>".Length;
            var block = xml.Substring(found, end - found);
            if (!TryParseSpec(block, out var parsed))
            {
                // Body failed to parse (probably corrupt Sockets/Overrides content);
                // fall back to the attributes on the open tag, which is all we need.
                var openOnly = xml.Substring(found, tagEnd - found) + "/>";
                if (TryParseSpec(openOnly, out var el))
                {
                    result.Add(el);
                }
            }
            else
            {
                result.Add(parsed);
            }
            idx = end;
        }
    }

    private static bool TryParseSpec(string fragment, out XElement element)
    {
        try
        {
            element = XElement.Parse(fragment);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            element = null!;
            return false;
        }
    }

    private static int ExtractActiveSpecIndex(string xml, int specCount)
    {
        // <Tree activeSpec="N">. 1-indexed in PoB.
        var m = Regex.Match(xml, "<Tree\\b[^>]*\\bactiveSpec=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var i) && i >= 1 && i <= specCount)
        {
            return i - 1;
        }
        return 0;
    }

    private static int ParseIntOrZero(string? s) =>
        int.TryParse(s, out var v) ? v : 0;

    private static int ParseClusterHashFormatVersion(XElement spec)
    {
        if (int.TryParse((string?)spec.Attribute("clusterHashFormatVersion"), out var version))
        {
            return version;
        }

        return spec.Attribute("nodes") is null ? 2 : 1;
    }

    private static (int[] Main, int[] Cluster) SplitNodes(string csv)
    {
        var main = new List<int>();
        var cluster = new List<int>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id))
            {
                continue;
            }
            (id >= 65536 ? cluster : main).Add(id);
        }
        return (main.ToArray(), cluster.ToArray());
    }

    private static Dictionary<int, int> ParseMasteryEffects(string? raw)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }
        // Format: "{nodeId,effectId}{nodeId,effectId}..."
        var span = raw.AsSpan();
        while (!span.IsEmpty)
        {
            var openBrace = span.IndexOf('{');
            if (openBrace < 0)
            {
                break;
            }
            var closeBrace = span.IndexOf('}');
            if (closeBrace < 0 || closeBrace <= openBrace)
            {
                break;
            }
            var inner = span.Slice(openBrace + 1, closeBrace - openBrace - 1);
            var comma = inner.IndexOf(',');
            if (comma > 0
                && int.TryParse(inner[..comma], out var nodeId)
                && int.TryParse(inner[(comma + 1)..], out var effectId))
            {
                result[nodeId] = effectId;
            }
            span = span[(closeBrace + 1)..];
        }
        return result;
    }

    private static IReadOnlyDictionary<int, AttributeNodeOverride> ParseAttributeOverrides(XElement spec)
    {
        var overrides = spec.Element("Overrides");
        if (overrides is null)
        {
            return new Dictionary<int, AttributeNodeOverride>();
        }

        var result = new Dictionary<int, AttributeNodeOverride>();
        foreach (var el in overrides.Elements("AttributeOverride"))
        {
            var nodeId = (int?)el.Attribute("nodeId") ?? (int?)el.Attribute("id") ?? 0;
            var raw = ((string?)el.Attribute("attribute") ?? (string?)el.Attribute("value") ?? el.Value).Trim();
            if (nodeId <= 0 || raw.Length == 0)
            {
                continue;
            }

            if (int.TryParse(raw, out var numeric) && Enum.IsDefined(typeof(AttributeNodeOverride), numeric))
            {
                result[nodeId] = (AttributeNodeOverride)numeric;
                continue;
            }

            result[nodeId] = raw.ToLowerInvariant() switch
            {
                "str" or "strength" => AttributeNodeOverride.Strength,
                "dex" or "dexterity" => AttributeNodeOverride.Dexterity,
                "int" or "intelligence" => AttributeNodeOverride.Intelligence,
                _ => result.TryGetValue(nodeId, out var existing) ? existing : 0,
            };
            if (result[nodeId] == 0)
            {
                result.Remove(nodeId);
            }
        }
        return result;
    }

    private sealed record ParsedItems(
        IReadOnlyList<ImportedItem> ActiveItems,
        IReadOnlyDictionary<int, ImportedItem> ItemsById);

    private static ParsedItems ParseItemsSection(string xml)
    {
        var start = FindTagBoundary(xml, "Items", 0);
        if (start < 0)
        {
            return new ParsedItems([], new Dictionary<int, ImportedItem>());
        }

        var end = xml.IndexOf("</Items>", start, StringComparison.Ordinal);
        if (end < 0)
        {
            return new ParsedItems([], new Dictionary<int, ImportedItem>());
        }

        var block = xml[start..(end + 8)];
        XElement itemsEl;
        try
        {
            itemsEl = XElement.Parse(block);
        }
        catch
        {
            return new ParsedItems([], new Dictionary<int, ImportedItem>());
        }

        // Build id → raw text map
        var texts = new Dictionary<int, string>();
        var itemsById = new Dictionary<int, ImportedItem>();
        foreach (var el in itemsEl.Elements("Item"))
        {
            if ((int?)el.Attribute("id") is int id)
            {
                texts[id] = el.Value;
                itemsById[id] = RawItemParser.Parse(string.Empty, rawText: el.Value.Trim()) with { Id = id };
            }
        }

        // Find the active ItemSet
        var activeId = (int?)itemsEl.Attribute("activeItemSet")
            ?? (int?)itemsEl.Attribute("active")
            ?? 1;
        XElement? activeSet = null;
        foreach (var setEl in itemsEl.Elements("ItemSet"))
        {
            var isActive = string.Equals((string?)setEl.Attribute("active"), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals((string?)setEl.Attribute("active"), "1", StringComparison.OrdinalIgnoreCase);
            if (isActive || ((int?)setEl.Attribute("id") ?? 1) == activeId)
            {
                activeSet = setEl;
                break;
            }
        }
        if (activeSet is null)
        {
            foreach (var setEl in itemsEl.Elements("ItemSet"))
            {
                activeSet = setEl;
                break;
            }
        }
        if (activeSet is null)
        {
            return new ParsedItems([], itemsById);
        }

        var result = new List<ImportedItem>();
        foreach (var slotEl in activeSet.Elements("Slot"))
        {
            var slotName = (string?)slotEl.Attribute("name") ?? string.Empty;
            var itemId = (int?)slotEl.Attribute("itemId") ?? 0;
            if (itemId > 0 && texts.TryGetValue(itemId, out var raw))
            {
                result.Add(RawItemParser.Parse(slotName, raw.Trim()) with { Id = itemId });
            }
        }

        result.Sort((a, b) => SlotIndex(a.Slot).CompareTo(SlotIndex(b.Slot)));
        return new ParsedItems(result, itemsById);
    }

    private static IReadOnlyList<ImportedSocketedJewel> ParseSocketedJewels(XElement spec)
    {
        var sockets = spec.Element("Sockets");
        if (sockets is null)
        {
            return [];
        }

        var result = new List<ImportedSocketedJewel>();
        foreach (var socket in sockets.Elements("Socket"))
        {
            var socketId = (int?)socket.Attribute("nodeId") ?? 0;
            var itemId = (int?)socket.Attribute("itemId") ?? 0;
            if (socketId > 0 && itemId > 0)
            {
                result.Add(new ImportedSocketedJewel(socketId, itemId));
            }
        }
        return result;
    }

    // Finds the start index of <tagName followed by whitespace, '>', or '/'.
    // Prevents false positives like <ItemSet when searching for <Item.
    private static int FindTagBoundary(string xml, string tag, int start)
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

    private static ImportedItem ParseRawItem(string slot, string rawText)
    {
        var lines = rawText.Split('\n');
        var rarity = "Normal";
        var name = string.Empty;
        var baseType = string.Empty;
        var i = 0;

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        if (i < lines.Length)
        {
            var first = lines[i].Trim();
            if (first.StartsWith("Rarity:", StringComparison.OrdinalIgnoreCase))
            {
                rarity = first[7..].Trim();
                i++;
            }
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        if (i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            name = lines[i++].Trim();
        }

        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
        {
            i++;
        }

        var ru = rarity.ToUpperInvariant();
        if ((ru == "RARE" || ru == "UNIQUE") && i < lines.Length && !lines[i].Trim().StartsWith("---", StringComparison.Ordinal))
        {
            baseType = lines[i].Trim();
        }
        else
        {
            baseType = name;
        }

        return new ImportedItem(slot, rarity, name, baseType, rawText);
    }

    private static int SlotIndex(string slot) => slot switch
    {
        "Weapon 1" => 0,
        "Weapon 2" => 1,
        "Weapon 1 Swap" => 2,
        "Weapon 2 Swap" => 3,
        "Helmet" => 4,
        "Body Armour" => 5,
        "Gloves" => 6,
        "Boots" => 7,
        "Amulet" => 8,
        "Ring 1" => 9,
        "Ring 2" => 10,
        "Belt" => 11,
        "Flask 1" => 12,
        "Flask 2" => 13,
        "Flask 3" => 14,
        "Flask 4" => 15,
        "Flask 5" => 16,
        _ => 100,
    };
}
