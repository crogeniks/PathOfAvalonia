namespace PathOfAvalonia.TreeDomain.Import;

internal readonly record struct ImportInput(string Text, string LastPathSegment)
{
    public static ImportInput From(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("Import text is empty");
        }

        var input = text.Trim();
        var code = input;
        var lastSlash = code.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < code.Length - 1)
        {
            code = code[(lastSlash + 1)..];
        }

        return new ImportInput(input, code);
    }
}
