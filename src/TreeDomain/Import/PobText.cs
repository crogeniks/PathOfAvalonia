namespace PathOfAvalonia.TreeDomain.Import;

internal static class PobText
{
    // Path of Building uses ^ followed by a digit as an inline color control code.
    // Avalonia's text controls do not interpret these codes, so retain the text but
    // remove the non-display formatting markers.
    public static string StripColorCodes(string text)
    {
        var firstCode = text.IndexOf('^');
        if (firstCode < 0 || firstCode == text.Length - 1 || !char.IsDigit(text[firstCode + 1]))
        {
            return text;
        }

        var result = new System.Text.StringBuilder(text.Length - 2);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '^' && index + 1 < text.Length && char.IsDigit(text[index + 1]))
            {
                index++;
                continue;
            }

            result.Append(text[index]);
        }

        return result.ToString();
    }
}
