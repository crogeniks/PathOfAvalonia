namespace PathOfAvalonia.TreeDomain.Import;

using System.Globalization;
using System.Xml.Linq;

public static class PobXmlBuildParser
{
    public static string DecodeBuildCodeToXml(string code) =>
        PobBuildCodeDecoder.DecodeToXml(code);

    public static ImportedBuild Parse(string xml, string source)
    {
        xml = PobText.StripColorCodes(xml);
        var items = PobItemSetParser.Parse(xml);
        var skills = PobSkillsParser.Parse(xml);
        var metrics = PobMetricsParser.Parse(xml);
        var passiveTrees = PobPassiveTreeParser.Parse(xml);
        var active = passiveTrees.Variants[passiveTrees.ActiveIndex];
        var buildElement = TryFindElement(xml, "Build");

        return new ImportedBuild(
            ClassId: active.ClassId,
            AscendClassId: active.AscendClassId,
            SecondaryAscendClassId: active.SecondaryAscendClassId,
            NodeHashes: active.NodeHashes,
            ClusterNodeHashes: active.ClusterNodeHashes,
            MasterySelections: active.MasterySelections,
            TreeVersion: active.TreeVersion,
            Source: source)
        {
            ClusterHashFormatVersion = active.ClusterHashFormatVersion,
            ClassInternalId = active.ClassInternalId,
            AscendancyInternalId = active.AscendancyInternalId,
            AttributeOverrides = active.AttributeOverrides,
            AllocationSets = active.AllocationSets,
            Items = items.ActiveItems,
            ItemsById = items.ItemsById,
            SocketedJewels = active.SocketedJewels,
            PassiveTreeVariants = passiveTrees.Variants,
            ItemSetVariants = items.ItemSetVariants,
            ActivePassiveTreeVariantIndex = passiveTrees.ActiveIndex,
            ActiveItemSetVariantIndex = items.ActiveItemSetVariantIndex,
            CharacterLevel = Math.Clamp(PobXmlHelpers.AttrInt(buildElement ?? new XElement("Build"), "level") ?? 1, 1, 100),
            ResistancePenalty = ParseResistancePenalty(xml) ?? -60,
            RawXml = xml,
            Skills = skills,
            Metrics = metrics,
        };
    }

    private static int? ParseResistancePenalty(string xml)
    {
        try
        {
            var input = XDocument.Parse(xml)
                .Descendants("Input")
                .FirstOrDefault(element => string.Equals(
                    (string?)element.Attribute("name"),
                    "resistancePenalty",
                    StringComparison.Ordinal));
            return int.TryParse(
                (string?)input?.Attribute("number"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                    ? Math.Clamp(value, -60, 0)
                    : null;
        }
        catch
        {
            return null;
        }
    }

    private static XElement? TryFindElement(string xml, string name)
    {
        try
        {
            return XDocument.Parse(xml).Descendants(name).FirstOrDefault();
        }
        catch
        {
            return PobXmlHelpers.TryExtractElement(xml, name);
        }
    }
}
