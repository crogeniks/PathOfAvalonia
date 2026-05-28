using System.IO.Compression;
using System.Text;

namespace PathOfAvalonia.TreeDomain.Import;

// Decodes a PoB share string: URL-safe base64 → zlib → XML.
// The build XML carries a <Build><Spec nodes="..." classId="..." ascendClassId="..."/> block;
// specs in the modern schema use the `nodes` attribute, older specs fall back to <URL>.
public static class PobBuildCodeDecoder
{
    public static ImportedBuild Decode(string code)
    {
        var xml = DecodeToXml(code);
        return PobXmlBuildParser.Parse(xml, "pob-code");
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

}
