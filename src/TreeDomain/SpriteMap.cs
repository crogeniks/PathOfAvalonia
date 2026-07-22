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

    public SpriteMap Merge(SpriteMap additional)
    {
        var atlases = new Dictionary<string, SpriteAtlas>(Atlases, StringComparer.Ordinal);
        foreach (var (name, atlas) in additional.Atlases)
        {
            atlases[name] = atlas;
        }
        return new SpriteMap { Atlases = atlases };
    }

    public static SpriteMap LoadFromJson(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<SpriteMapDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("sprite JSON was null");
        return new SpriteMap { Atlases = ConvertAtlases(dto.Atlases) };
    }

    public static SpriteMap LoadPoe2FromGggAssets(
        Stream skillsStream,
        Stream framesStream,
        Stream jewelsStream,
        Stream? masteryEffectDisabledStream = null,
        Stream? masteryEffectActiveStream = null)
    {
        var atlases = new Dictionary<string, SpriteAtlas>(StringComparer.Ordinal)
        {
            ["poe2NodeIcons"] = LoadGggAtlas(skillsStream, "assets/skills.webp"),
            ["poe2Frames"] = LoadGggAtlas(framesStream, "assets/frame.webp"),
            ["poe2Jewels"] = LoadGggAtlas(jewelsStream, "assets/jewel.webp"),
        };
        if (masteryEffectDisabledStream is not null)
        {
            atlases["poe2MasteryEffectDisabled"] = LoadGggAtlas(masteryEffectDisabledStream, "assets/mastery-effect-disabled.webp");
        }
        if (masteryEffectActiveStream is not null)
        {
            atlases["poe2MasteryEffectActive"] = LoadGggAtlas(masteryEffectActiveStream, "assets/mastery-effect-active.webp");
        }
        return new SpriteMap { Atlases = atlases };
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
                    pair => pair.Value.ToSpriteRect(),
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
        [JsonPropertyName("coords")] public Dictionary<string, Poe1GggRectDto> Coords { get; set; } = new();
    }

    private sealed class Poe1GggRectDto
    {
        [JsonPropertyName("x")] public IntOrArrayDto X { get; set; }
        [JsonPropertyName("y")] public IntOrArrayDto Y { get; set; }
        [JsonPropertyName("w")] public IntOrArrayDto W { get; set; }
        [JsonPropertyName("h")] public IntOrArrayDto H { get; set; }

        public SpriteRect ToSpriteRect()
        {
            var count = X.Count;
            if (count == 0 || Y.Count != count || W.Count != count || H.Count != count)
            {
                throw new JsonException("PoE1 sprite coordinate arrays must be non-empty and have matching lengths.");
            }

            // GGG 3.29 can provide multiple size-specific rectangles for one logical
            // sprite. SpriteMap currently exposes one rectangle, and the first tuple
            // is the legacy-sized variant used by existing rendering.
            return new SpriteRect(X[0], Y[0], W[0], H[0]);
        }
    }

    [JsonConverter(typeof(IntOrArrayDtoJsonConverter))]
    private readonly record struct IntOrArrayDto(IReadOnlyList<int>? Values)
    {
        public int Count => Values?.Count ?? 0;
        public int this[int index] => Values![index];
    }

    private sealed class IntOrArrayDtoJsonConverter : JsonConverter<IntOrArrayDto>
    {
        public override IntOrArrayDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return new IntOrArrayDto([reader.GetInt32()]);
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("PoE1 sprite coordinate must be an integer or an integer array.");
            }

            var values = new List<int>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.Number)
                {
                    throw new JsonException("PoE1 sprite coordinate array values must be integers.");
                }
                values.Add(reader.GetInt32());
            }

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("PoE1 sprite coordinate array was not terminated.");
            }

            return new IntOrArrayDto(values);
        }

        public override void Write(Utf8JsonWriter writer, IntOrArrayDto value, JsonSerializerOptions options)
        {
            if (value.Count == 1)
            {
                writer.WriteNumberValue(value[0]);
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value.Values ?? [])
            {
                writer.WriteNumberValue(item);
            }
            writer.WriteEndArray();
        }
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
