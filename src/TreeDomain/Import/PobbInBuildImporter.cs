using System.Net;
using System.Net.Http.Headers;

namespace PathOfAvalonia.TreeDomain.Import;

public static class PobbInBuildImporter
{
    private static readonly HttpClient Client = CreateClient();

    public static bool LooksLikeUrl(string text) =>
        TryCreateRawUrl(text, out _);

    public static async Task<string> FetchBuildCodeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!TryCreateRawUrl(text, out var rawUrl))
        {
            throw new InvalidDataException("Expected a pobb.in build URL.");
        }

        using var response = await Client.GetAsync(rawUrl, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidDataException($"pobb.in returned {(int)response.StatusCode} {response.StatusCode}");
        }

        var code = body.Trim();
        if (!PobBuildCodeDecoder.LooksLikeBuildCode(code))
        {
            throw new InvalidDataException("pobb.in response did not contain a valid PoB build code.");
        }
        return code;
    }

    public static bool TryCreateRawUrl(string text, out Uri rawUrl)
    {
        rawUrl = null!;
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !IsPobbInHost(uri.Host))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => WebUtility.UrlDecode(segment) ?? string.Empty)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
        if (segments.Length == 0)
        {
            return false;
        }
        if (string.Equals(segments[^1], "raw", StringComparison.OrdinalIgnoreCase))
        {
            segments = segments[..^1];
        }

        string rawPath;
        if (segments.Length == 1)
        {
            rawPath = "/" + Uri.EscapeDataString(segments[0]) + "/raw";
        }
        else if (segments.Length == 3 && string.Equals(segments[0], "u", StringComparison.OrdinalIgnoreCase))
        {
            rawPath = "/u/" + Uri.EscapeDataString(segments[1]) + "/" + Uri.EscapeDataString(segments[2]) + "/raw";
        }
        else
        {
            return false;
        }

        rawUrl = new Uri("https://pobb.in" + rawPath);
        return true;
    }

    private static bool IsPobbInHost(string host) =>
        string.Equals(host, "pobb.in", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "www.pobb.in", StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PathOfAvalonia", "0.1"));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/crogeniks/PathOfAvalonia)"));
        return client;
    }
}
