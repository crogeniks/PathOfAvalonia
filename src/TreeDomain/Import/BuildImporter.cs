namespace PathOfAvalonia.TreeDomain.Import;

// Sniffs the pasted text and dispatches to the right decoder. Returns an ImportedBuild
// with raw node IDs; the caller applies them to a PassiveSpec via ApplyImport().
public static class BuildImporter
{
    public static ImportedBuild Import(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("Import text is empty");
        }
        var input = text.Trim();

        if (PobTreeUrlDecoder.LooksLikeTreeUrl(input))
        {
            return PobTreeUrlDecoder.Decode(input);
        }

        // PoB build code. Some share sites prefix the base64 with a host URL — strip that.
        var code = input;
        var lastSlash = code.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < code.Length - 1)
        {
            code = code[(lastSlash + 1)..];
        }
        if (PobBuildCodeDecoder.LooksLikeBuildCode(code))
        {
            return PobBuildCodeDecoder.Decode(code);
        }

        throw new InvalidDataException("Unrecognised input — expected a passive tree URL or a PoB build code");
    }
}
