namespace PathOfAvalonia.TreeDomain.Import;

// Sniffs the pasted text and dispatches to the right decoder. Returns an ImportedBuild
// with raw node IDs; the caller applies them to a PassiveSpec via ApplyImport().
public static class BuildImporter
{
    public static async Task<ImportedBuild> ImportAsync(string text, CancellationToken cancellationToken = default)
    {
        var input = ImportInput.From(text);
        if (PobbInBuildImporter.LooksLikeUrl(input.Text))
        {
            var code = await PobbInBuildImporter.FetchBuildCodeAsync(input.Text, cancellationToken).ConfigureAwait(false);
            return PobBuildCodeDecoder.Decode(code, "pobb.in");
        }

        return DecodeInput(input);
    }

    public static ImportedBuild Import(string text)
    {
        var input = ImportInput.From(text);
        if (PobbInBuildImporter.LooksLikeUrl(input.Text))
        {
            var code = PobbInBuildImporter.FetchBuildCodeAsync(input.Text).GetAwaiter().GetResult();
            return PobBuildCodeDecoder.Decode(code, "pobb.in");
        }

        return DecodeInput(input);
    }

    private static ImportedBuild DecodeInput(ImportInput input)
    {
        if (PobTreeUrlDecoder.LooksLikeTreeUrl(input.Text))
        {
            return PobTreeUrlDecoder.Decode(input.Text);
        }

        if (PobBuildCodeDecoder.LooksLikeBuildCode(input.LastPathSegment))
        {
            return PobBuildCodeDecoder.Decode(input.LastPathSegment);
        }

        throw new InvalidDataException("Unrecognised input — expected a passive tree URL or a PoB build code");
    }
}
