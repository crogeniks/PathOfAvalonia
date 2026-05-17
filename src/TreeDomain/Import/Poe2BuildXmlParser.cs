using System.IO;
using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

public static class Poe2BuildXmlParser
{
    public static ImportedBuild Parse(string xml)
    {
        if (!HasPoe2Root(xml))
        {
            throw new InvalidDataException("PoE2 build code must contain a PathOfBuilding2 XML document");
        }

        return PobXmlBuildParser.Parse(xml, "pob2-code");
    }

    private static bool HasPoe2Root(string xml)
    {
        try
        {
            using var reader = new StringReader(xml);
            var doc = XDocument.Load(reader, LoadOptions.None);
            return string.Equals(doc.Root?.Name.LocalName, "PathOfBuilding2", StringComparison.Ordinal);
        }
        catch
        {
            var trimmed = xml.TrimStart();
            return trimmed.StartsWith("<PathOfBuilding2", StringComparison.Ordinal);
        }
    }
}
