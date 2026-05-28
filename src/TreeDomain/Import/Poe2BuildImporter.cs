namespace PathOfAvalonia.TreeDomain.Import;

public static class Poe2BuildImporter
{
    public static ImportedBuild Import(string text)
    {
        var input = ImportInput.From(text);
        if (input.Text.Contains("pathofexile.com", StringComparison.OrdinalIgnoreCase)
            && input.Text.Contains("passive-skill-tree", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Path of Exile 2 does not support PoE1 passive tree URLs.");
        }
        if (input.Text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || input.Text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Path of Exile 2 tree URLs are not supported yet; paste a PoB2 build code.");
        }

        if (Poe2BuildCodeDecoder.LooksLikeBuildCode(input.LastPathSegment))
        {
            return Poe2BuildCodeDecoder.Decode(input.LastPathSegment);
        }

        throw new InvalidDataException("Unrecognised input; expected a Path of Building 2 build code");
    }
}
