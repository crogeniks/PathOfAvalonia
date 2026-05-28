using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

internal sealed record ParsedPassiveTrees(
    IReadOnlyList<ImportedPassiveTreeVariant> Variants,
    int ActiveIndex);

internal static class PobPassiveTreeParser
{
    public static ParsedPassiveTrees Parse(string xml)
    {
        var specElements = ExtractSpecElements(xml);
        if (specElements.Count == 0)
        {
            throw new InvalidDataException("PoB XML contains no <Spec> element");
        }

        var variants = specElements
            .Select((spec, index) => ParsePassiveTreeVariant(spec, index))
            .ToArray();
        return new ParsedPassiveTrees(variants, ExtractActiveSpecIndex(xml, variants.Length));
    }

    private static ImportedPassiveTreeVariant ParsePassiveTreeVariant(XElement spec, int index)
    {
        var classId = ParseIntOrZero((string?)spec.Attribute("classId"));
        var ascendClassId = ParseIntOrZero((string?)spec.Attribute("ascendClassId"));
        var secondaryAscendClassId = ParseIntOrZero((string?)spec.Attribute("secondaryAscendClassId"));
        var treeVersion = (string?)spec.Attribute("treeVersion");
        var clusterHashFormatVersion = ParseClusterHashFormatVersion(spec);
        var classInternalId = (string?)spec.Attribute("classInternalId");
        var ascendancyInternalId = (string?)spec.Attribute("ascendancyInternalId");
        var attributeOverrides = ParseAttributeOverrides(spec);
        var socketedJewels = ParseSocketedJewels(spec);
        var displayName = PobXmlHelpers.DisplayName(spec, "Tree", index);
        var allocationSets = ParseWeaponSetAllocations(spec);

        var nodesAttr = (string?)spec.Attribute("nodes");
        if (!string.IsNullOrWhiteSpace(nodesAttr))
        {
            var (main, cluster) = SplitNodes(nodesAttr!);
            return new ImportedPassiveTreeVariant(
                index,
                displayName,
                classId,
                ascendClassId,
                secondaryAscendClassId,
                main,
                cluster,
                ParseMasteryEffects((string?)spec.Attribute("masteryEffects")),
                treeVersion,
                clusterHashFormatVersion,
                classInternalId,
                ascendancyInternalId,
                attributeOverrides,
                socketedJewels)
            {
                AllocationSets = allocationSets,
            };
        }

        var urlElement = spec.Element("URL");
        if (urlElement is null)
        {
            throw new InvalidDataException("Spec has neither 'nodes' attribute nor <URL> child");
        }

        var embedded = PobTreeUrlDecoder.Decode(urlElement.Value);
        return new ImportedPassiveTreeVariant(
            index,
            displayName,
            embedded.ClassId,
            embedded.AscendClassId,
            embedded.SecondaryAscendClassId,
            embedded.NodeHashes,
            embedded.ClusterNodeHashes,
            embedded.MasterySelections,
            treeVersion ?? embedded.TreeVersion,
            clusterHashFormatVersion,
            classInternalId,
            ascendancyInternalId,
            attributeOverrides,
            socketedJewels)
        {
            AllocationSets = allocationSets,
        };
    }

    private static List<XElement> ExtractSpecElements(string xml)
    {
        var result = new List<XElement>();
        var idx = 0;
        while (true)
        {
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

    private static IReadOnlyDictionary<int, PassiveAllocationSet> ParseWeaponSetAllocations(XElement spec)
    {
        var result = new Dictionary<int, PassiveAllocationSet>();
        foreach (var element in spec.Elements())
        {
            var set = element.Name.LocalName switch
            {
                "WeaponSet1" => PassiveAllocationSet.WeaponSet1,
                "WeaponSet2" => PassiveAllocationSet.WeaponSet2,
                _ => PassiveAllocationSet.Normal,
            };
            if (set == PassiveAllocationSet.Normal)
            {
                continue;
            }

            var nodes = (string?)element.Attribute("nodes");
            if (string.IsNullOrWhiteSpace(nodes))
            {
                continue;
            }

            foreach (var part in nodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var id))
                {
                    result[id] = set;
                }
            }
        }
        return result;
    }

    private static Dictionary<int, int> ParseMasteryEffects(string? raw)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

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
}
