namespace PathOfAvalonia.TreeDomain.Import;

public static class Poe2BuildCodeDecoder
{
    public static ImportedBuild Decode(string code) => Decode(code, "pob2-code");

    public static ImportedBuild Decode(string code, string source)
    {
        var xml = PobXmlBuildParser.DecodeBuildCodeToXml(code);
        return Poe2BuildXmlParser.Parse(xml) with { Source = source };
    }

    public static bool LooksLikeBuildCode(string text) =>
        PobBuildCodeDecoder.LooksLikeBuildCode(text);
}
