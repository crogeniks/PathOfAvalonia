namespace PathOfAvalonia.TreeDomain.Import;

public static class PobXmlBuildParser
{
    public static string DecodeBuildCodeToXml(string code) =>
        PobBuildCodeDecoder.DecodeToXml(code);

    public static ImportedBuild Parse(string xml, string source) =>
        PobBuildCodeDecoder.ParseXml(xml, source);
}
