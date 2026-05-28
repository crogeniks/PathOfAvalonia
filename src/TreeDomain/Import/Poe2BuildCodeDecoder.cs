namespace PathOfAvalonia.TreeDomain.Import;

public static class Poe2BuildCodeDecoder
{
    public static ImportedBuild Decode(string code)
    {
        var xml = PobXmlBuildParser.DecodeBuildCodeToXml(code);
        return Poe2BuildXmlParser.Parse(xml);
    }

    public static bool LooksLikeBuildCode(string text) =>
        PobBuildCodeDecoder.LooksLikeBuildCode(text);
}
