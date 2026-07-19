namespace PathOfAvalonia.TreeDomain.Import;

/// <summary>Shared transport handling for PoB build-code importers.</summary>
internal static class BuildImportDispatcher
{
    public static async Task<ImportedBuild> ImportAsync(
        string text,
        Func<string, ImportedBuild> decodePobbInCode,
        Func<ImportInput, ImportedBuild> decodeInput,
        CancellationToken cancellationToken = default)
    {
        var input = ImportInput.From(text);
        if (!PobbInBuildImporter.LooksLikeUrl(input.Text))
        {
            return decodeInput(input);
        }

        var code = await PobbInBuildImporter.FetchBuildCodeAsync(input.Text, cancellationToken).ConfigureAwait(false);
        return decodePobbInCode(code);
    }

    public static ImportedBuild Import(
        string text,
        Func<string, ImportedBuild> decodePobbInCode,
        Func<ImportInput, ImportedBuild> decodeInput)
    {
        var input = ImportInput.From(text);
        if (!PobbInBuildImporter.LooksLikeUrl(input.Text))
        {
            return decodeInput(input);
        }

        var code = PobbInBuildImporter.FetchBuildCodeAsync(input.Text).GetAwaiter().GetResult();
        return decodePobbInCode(code);
    }
}
