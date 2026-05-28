namespace PathOfAvalonia.TreeDomain.Import;

internal static class ItemText
{
    public static string StripTags(string text)
    {
        var span = text.AsSpan();
        while (span.StartsWith("{", StringComparison.Ordinal))
        {
            var close = span.IndexOf('}');
            if (close < 0)
            {
                break;
            }
            span = span[(close + 1)..];
            while (!span.IsEmpty && char.IsWhiteSpace(span[0]))
            {
                span = span[1..];
            }
        }
        return span.ToString();
    }
}
