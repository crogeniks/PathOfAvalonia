using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathOfAvalonia.TreeDomain;

public readonly record struct SpriteRect(int X, int Y, int W, int H);

public sealed class SpriteAtlas
{
    public required string File { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required IReadOnlyDictionary<string, SpriteRect> Coords { get; init; }
}

public sealed class SpriteMap
{
    public required IReadOnlyDictionary<string, SpriteAtlas> Atlases { get; init; }

    public SpriteRect? Lookup(string atlas, string key)
    {
        if (!Atlases.TryGetValue(atlas, out var a))
        {
            return null;
        }
        return a.Coords.TryGetValue(key, out var r) ? r : null;
    }

    public static SpriteMap LoadFromJson(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<SpriteMapDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("sprite JSON was null");
        return new SpriteMap { Atlases = ConvertAtlases(dto.Atlases) };
    }

    public static SpriteMap LoadPoe2FromGggAssets(Stream skillsStream, Stream framesStream, Stream jewelsStream)
    {
        return new SpriteMap
        {
            Atlases = new Dictionary<string, SpriteAtlas>(StringComparer.Ordinal)
            {
                ["poe2NodeIcons"] = LoadGggAtlas(skillsStream, "assets/skills.webp"),
                ["poe2Frames"] = LoadGggAtlas(framesStream, "assets/frame.webp"),
                ["poe2Jewels"] = LoadGggAtlas(jewelsStream, "assets/jewel.webp"),
            },
        };
    }

    public static SpriteMap LoadPoe1FromGggTree(Stream stream, string assetPrefix, string zoom = "0.3835")
    {
        var dto = JsonSerializer.Deserialize<Poe1GggTreeDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("PoE1 tree JSON was null");
        var atlases = new Dictionary<string, SpriteAtlas>(StringComparer.Ordinal);
        foreach (var (atlasName, zoomAtlases) in dto.Sprites)
        {
            if (!zoomAtlases.TryGetValue(zoom, out var atlas))
            {
                atlas = zoomAtlases
                    .OrderByDescending(pair => double.TryParse(pair.Key, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
                    .FirstOrDefault().Value;
            }

            if (atlas is null)
            {
                continue;
            }

            atlases[atlasName] = new SpriteAtlas
            {
                File = $"{assetPrefix.TrimEnd('/')}/{LocalFileName(atlas.Filename)}",
                Width = atlas.W,
                Height = atlas.H,
                Coords = atlas.Coords.ToDictionary(
                    pair => pair.Key,
                    pair => new SpriteRect(pair.Value.X, pair.Value.Y, pair.Value.W, pair.Value.H),
                    StringComparer.Ordinal),
            };
        }

        return new SpriteMap { Atlases = atlases };
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class SpriteMapDto
    {
        [JsonPropertyName("atlases")] public Dictionary<string, AtlasDto> Atlases { get; set; } = new();
    }

    private sealed class GggAtlasDto
    {
        [JsonPropertyName("frames")] public Dictionary<string, GggFrameDto> Frames { get; set; } = new();
    }

    private sealed class GggFrameDto
    {
        [JsonPropertyName("frame")] public RectDto Frame { get; set; } = new();
    }

    private sealed class Poe1GggTreeDto
    {
        [JsonPropertyName("sprites")] public Dictionary<string, Dictionary<string, Poe1GggAtlasDto>> Sprites { get; set; } = new();
    }

    private sealed class Poe1GggAtlasDto
    {
        [JsonPropertyName("filename")] public string Filename { get; set; } = string.Empty;
        [JsonPropertyName("w")] public int W { get; set; }
        [JsonPropertyName("h")] public int H { get; set; }
        [JsonPropertyName("coords")] public Dictionary<string, RectDto> Coords { get; set; } = new();
    }

    private sealed class AtlasDto
    {
        [JsonPropertyName("file")] public string File { get; set; } = "";
        [JsonPropertyName("w")] public int W { get; set; }
        [JsonPropertyName("h")] public int H { get; set; }
        [JsonPropertyName("coords")] public Dictionary<string, RectDto> Coords { get; set; } = new();
    }

    private sealed class RectDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("w")] public int W { get; set; }
        [JsonPropertyName("h")] public int H { get; set; }
    }

    private static IReadOnlyDictionary<string, SpriteAtlas> ConvertAtlases(Dictionary<string, AtlasDto> atlases)
    {
        var result = new Dictionary<string, SpriteAtlas>(atlases.Count, StringComparer.Ordinal);
        foreach (var (name, atlas) in atlases)
        {
            result[name] = new SpriteAtlas
            {
                File = atlas.File,
                Width = atlas.W,
                Height = atlas.H,
                Coords = atlas.Coords.ToDictionary(
                    pair => pair.Key,
                    pair => new SpriteRect(pair.Value.X, pair.Value.Y, pair.Value.W, pair.Value.H),
                    StringComparer.Ordinal),
            };
        }

        return result;
    }

    private static SpriteAtlas LoadGggAtlas(Stream stream, string file)
    {
        var dto = JsonSerializer.Deserialize<GggAtlasDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("GGG atlas JSON was null");
        var coords = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
        var maxX = 0;
        var maxY = 0;

        foreach (var (rawKey, frame) in dto.Frames)
        {
            var key = NormalizeGggFrameKey(rawKey);
            if (key.Length == 0 || coords.ContainsKey(key))
            {
                continue;
            }

            coords[key] = new SpriteRect(frame.Frame.X, frame.Frame.Y, frame.Frame.W, frame.Frame.H);
            maxX = Math.Max(maxX, frame.Frame.X + frame.Frame.W);
            maxY = Math.Max(maxY, frame.Frame.Y + frame.Frame.H);
        }

        return new SpriteAtlas
        {
            File = file,
            Width = maxX,
            Height = maxY,
            Coords = coords,
        };
    }

    private static string NormalizeGggFrameKey(string rawKey)
    {
        var separator = rawKey.IndexOf(':');
        return separator >= 0 && separator + 1 < rawKey.Length
            ? rawKey[(separator + 1)..]
            : rawKey;
    }

    private static string LocalFileName(string filename)
    {
        var withoutQuery = filename.Split('?', 2)[0];
        var slash = withoutQuery.LastIndexOf('/');
        return slash >= 0 ? withoutQuery[(slash + 1)..] : withoutQuery;
    }
}
