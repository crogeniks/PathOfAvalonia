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
        var atlases = new Dictionary<string, SpriteAtlas>(dto.Atlases.Count);
        foreach (var (name, a) in dto.Atlases)
        {
            var coords = new Dictionary<string, SpriteRect>(a.Coords.Count);
            foreach (var (k, c) in a.Coords)
            {
                coords[k] = new SpriteRect(c.X, c.Y, c.W, c.H);
            }
            atlases[name] = new SpriteAtlas
            {
                File = a.File,
                Width = a.W,
                Height = a.H,
                Coords = coords,
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
}
