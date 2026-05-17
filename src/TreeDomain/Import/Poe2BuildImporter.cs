namespace PathOfAvalonia.TreeDomain.Import;

public static class Poe2BuildImporter
{
    public static ImportedBuild Import(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("Import text is empty");
        }

        var input = text.Trim();
        if (input.Contains("pathofexile.com", StringComparison.OrdinalIgnoreCase)
            && input.Contains("passive-skill-tree", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Path of Exile 2 does not support PoE1 passive tree URLs.");
        }
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Path of Exile 2 tree URLs are not supported yet; paste a PoB2 build code.");
        }

        var code = input;
        var lastSlash = code.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < code.Length - 1)
        {
            code = code[(lastSlash + 1)..];
        }
        if (Poe2BuildCodeDecoder.LooksLikeBuildCode(code))
        {
            return Poe2BuildCodeDecoder.Decode(code);
        }

        throw new InvalidDataException("Unrecognised input; expected a Path of Building 2 build code");
    }
}
