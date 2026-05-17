using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

var argsMap = ParseArgs(args);
var treePath = Required(argsMap, "tree");
var treeAssets = Required(argsMap, "tree-assets");
var uiAssets = Required(argsMap, "ui-assets");
var outDir = Required(argsMap, "out");
var version = Required(argsMap, "version");
var treeLuaPath = Path.Combine(Path.GetDirectoryName(treePath)!, "tree.lua");

Directory.CreateDirectory(outDir);
Directory.CreateDirectory(Path.Combine(outDir, "icons"));
Directory.CreateDirectory(Path.Combine(outDir, "frames"));
Directory.CreateDirectory(Path.Combine(outDir, "connections"));

var tree = JsonDocument.Parse(File.ReadAllText(treePath));
var iconKeys = new SortedSet<string>(StringComparer.Ordinal);
var frameKeys = new SortedSet<string>(StringComparer.Ordinal);
var connectionKeys = new SortedSet<string>(StringComparer.Ordinal);

if (tree.RootElement.TryGetProperty("nodes", out var nodes))
{
    foreach (var node in nodes.EnumerateObject())
    {
        AddString(node.Value, "icon", iconKeys);
        AddString(node.Value, "activeEffectImage", iconKeys);
        AddString(node.Value, "connectionArt", connectionKeys);
        frameKeys.Add(Poe2DefaultFrameKey(node.Value));
        if (node.Value.TryGetProperty("nodeOverlay", out var overlay))
        {
            AddString(overlay, "alloc", frameKeys);
            AddString(overlay, "path", frameKeys);
            AddString(overlay, "unalloc", frameKeys);
        }
    }
}
if (tree.RootElement.TryGetProperty("connectionArt", out var connectionArt))
{
    foreach (var prop in connectionArt.EnumerateObject())
    {
        if (prop.Value.ValueKind == JsonValueKind.String)
        {
            connectionKeys.Add(prop.Value.GetString()!);
        }
    }
}
if (tree.RootElement.TryGetProperty("assets", out var assets))
{
    foreach (var prop in assets.EnumerateObject())
    {
        foreach (var file in prop.Value.EnumerateArray())
        {
            if (file.ValueKind == JsonValueKind.String)
            {
                connectionKeys.Add(file.GetString()!);
            }
        }
    }
}

var spriteSources = ParseSpriteSources(File.ReadAllText(treeLuaPath));
var iconCoords = BuildCombinedAtlas(
    spriteSources,
    iconKeys,
    texture => texture.StartsWith("skills_", StringComparison.OrdinalIgnoreCase)
        || texture.StartsWith("legion_", StringComparison.OrdinalIgnoreCase),
    treeAssets,
    outDir,
    Path.Combine("icons", "poe2NodeIcons.png"),
    "icons");
var frameCoords = BuildCombinedAtlas(
    spriteSources,
    frameKeys,
    texture => texture.StartsWith("group-background_", StringComparison.OrdinalIgnoreCase),
    treeAssets,
    outDir,
    Path.Combine("frames", "poe2Frames.png"),
    "frames");
var jewelCoords = BuildCombinedAtlas(
    spriteSources,
    new SortedSet<string>(StringComparer.Ordinal) { "Ruby", "Emerald", "Sapphire", "Diamond", "Time-Lost Diamond", "Timeless Jewel" },
    texture => texture.StartsWith("jewel-sockets_", StringComparison.OrdinalIgnoreCase),
    treeAssets,
    outDir,
    Path.Combine("frames", "poe2Jewels.png"),
    "frames");

var connectionSource = FirstExisting(treeAssets, "Character_orbit_normal0.png");
var connectionAtlasFile = Path.Combine("connections", "Character_orbit_normal0.png");
if (connectionSource is not null)
{
    File.Copy(connectionSource, Path.Combine(outDir, connectionAtlasFile), overwrite: true);
}

var iconSize = PngSize(Path.Combine(outDir, "icons", "poe2NodeIcons.png"));
var frameSize = PngSize(Path.Combine(outDir, "frames", "poe2Frames.png"));
var jewelSize = File.Exists(Path.Combine(outDir, "frames", "poe2Jewels.png"))
    ? PngSize(Path.Combine(outDir, "frames", "poe2Jewels.png"))
    : (Width: 1, Height: 1);
var connectionSize = connectionSource is not null ? PngSize(Path.Combine(outDir, connectionAtlasFile)) : (Width: 1, Height: 1);

var spriteMap = new SpriteMapDto
{
    Atlases = new SortedDictionary<string, AtlasDto>(StringComparer.Ordinal)
    {
        ["poe2NodeIcons"] = new()
        {
            File = "icons/poe2NodeIcons.png",
            W = iconSize.Width,
            H = iconSize.Height,
            Coords = iconCoords,
        },
        ["poe2Frames"] = new()
        {
            File = "frames/poe2Frames.png",
            W = frameSize.Width,
            H = frameSize.Height,
            Coords = frameCoords,
        },
        ["poe2Connections"] = new()
        {
            File = connectionAtlasFile.Replace('\\', '/'),
            W = connectionSize.Width,
            H = connectionSize.Height,
            Coords = connectionKeys.ToDictionary(k => k, _ => new RectDto(0, 0, connectionSize.Width, connectionSize.Height), StringComparer.Ordinal),
        },
        ["poe2Background"] = new()
        {
            File = "background_0_4.png",
            W = 98,
            H = 98,
            Coords = new Dictionary<string, RectDto>(StringComparer.Ordinal),
        },
        ["poe2Jewels"] = new()
        {
            File = "frames/poe2Jewels.png",
            W = jewelSize.Width,
            H = jewelSize.Height,
            Coords = jewelCoords,
        },
    },
};

var json = JsonSerializer.Serialize(spriteMap, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(outDir, $"sprites_{version}.json"), json + Environment.NewLine);
Console.WriteLine($"Wrote {iconKeys.Count} icon keys, {frameKeys.Count} frame keys, {connectionKeys.Count} connection keys.");

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {args[i]}");
        }
        result[args[i][2..]] = args[++i];
    }
    return result;
}

static string Required(Dictionary<string, string> args, string key) =>
    args.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Missing --{key}");

static void AddString(JsonElement element, string propertyName, ISet<string> target)
{
    if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
    {
        var value = prop.GetString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }
}

static string Poe2DefaultFrameKey(JsonElement node)
{
    if (node.TryGetProperty("isKeystone", out var isKeystone) && isKeystone.GetBoolean())
    {
        return "KeystoneFrameUnallocated";
    }
    if (node.TryGetProperty("isJewelSocket", out var isJewelSocket) && isJewelSocket.GetBoolean())
    {
        return "JewelFrameUnallocated";
    }
    if (node.TryGetProperty("isNotable", out var isNotable) && isNotable.GetBoolean())
    {
        return "NotableFrameUnallocated";
    }
    return "PSSkillFrame";
}

static IReadOnlyList<SpriteSource> ParseSpriteSources(string lua)
{
    var result = new List<SpriteSource>();
    var spriteStart = lua.IndexOf("ddsCoords={", StringComparison.Ordinal);
    if (spriteStart < 0)
    {
        return result;
    }
    var nodeStart = lua.IndexOf("\n\tnodes={", spriteStart, StringComparison.Ordinal);
    var spriteBlock = nodeStart > spriteStart ? lua[spriteStart..nodeStart] : lua[spriteStart..];
    var textureRegex = new Regex("\\[\"(?<texture>[^\"]+)\"\\]=\\{(?<body>.*?)\\n\\t\\t\\}", RegexOptions.Singleline);
    var quotedRegex = new Regex("\\[\"(?<key>[^\"]+)\"\\]=(?<index>\\d+)");
    var bareRegex = new Regex("(?m)^\\s*(?<key>[A-Za-z0-9_ ]+)=(?<index>\\d+),?");
    foreach (Match textureMatch in textureRegex.Matches(spriteBlock))
    {
        var texture = textureMatch.Groups["texture"].Value;
        if (!TryParseTileSize(texture, out var tileWidth, out var tileHeight))
        {
            continue;
        }

        var entries = new Dictionary<string, int>(StringComparer.Ordinal);
        var body = textureMatch.Groups["body"].Value;
        foreach (Match m in quotedRegex.Matches(body))
        {
            entries[m.Groups["key"].Value] = int.Parse(m.Groups["index"].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        foreach (Match m in bareRegex.Matches(body))
        {
            entries[m.Groups["key"].Value.Trim()] = int.Parse(m.Groups["index"].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        result.Add(new SpriteSource(texture, tileWidth, tileHeight, entries));
    }
    return result;
}

static bool TryParseTileSize(string texture, out int width, out int height)
{
    var match = Regex.Match(texture, @"_(\d+)_(\d+)_");
    if (match.Success
        && int.TryParse(match.Groups[1].Value, out width)
        && int.TryParse(match.Groups[2].Value, out height))
    {
        return true;
    }
    width = 0;
    height = 0;
    return false;
}

static Dictionary<string, RectDto> BuildCombinedAtlas(
    IReadOnlyList<SpriteSource> sources,
    ISet<string> wantedKeys,
    Func<string, bool> includeTexture,
    string treeAssets,
    string outDir,
    string outputRelativePath,
    string tempSubdir)
{
    var matchingSources = sources
        .Where(source => includeTexture(source.Texture) && source.Entries.Keys.Any(wantedKeys.Contains))
        .OrderBy(source => source.Texture, StringComparer.Ordinal)
        .ToArray();
    var coords = new Dictionary<string, RectDto>(StringComparer.Ordinal);
    if (matchingSources.Length == 0)
    {
        CreateTransparentPng(Path.Combine(outDir, outputRelativePath), 1, 1);
        return coords;
    }

    if (matchingSources.All(source => source.Texture.Contains("_BC1.", StringComparison.OrdinalIgnoreCase)))
    {
        return BuildBc1CombinedAtlas(matchingSources, wantedKeys, treeAssets, outDir, outputRelativePath);
    }

    var tempFiles = new List<string>();
    var yOffset = 0;
    foreach (var source in matchingSources)
    {
        var sourcePath = Path.Combine(treeAssets, source.Texture);
        var converted = Path.Combine(outDir, tempSubdir, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(source.Texture)) + ".png");
        ConvertZstdDds(sourcePath, converted);
        tempFiles.Add(converted);
        var size = PngSize(converted);
        foreach (var (key, index) in source.Entries)
        {
            if (!wantedKeys.Contains(key))
            {
                continue;
            }
            coords[key] = new RectDto(
                0,
                yOffset,
                Math.Min(source.TileWidth, size.Width),
                Math.Min(source.TileHeight, size.Height));
        }
        yOffset += size.Height;
    }

    AppendImages(tempFiles, Path.Combine(outDir, outputRelativePath));
    return coords;
}

static Dictionary<string, RectDto> BuildBc1CombinedAtlas(
    IReadOnlyList<SpriteSource> sources,
    ISet<string> wantedKeys,
    string treeAssets,
    string outDir,
    string outputRelativePath)
{
    var entries = new List<(string Key, byte[] Rgba, int Width, int Height)>();
    foreach (var source in sources)
    {
        var ddsPath = Path.Combine(treeAssets, source.Texture);
        var dds = ReadZstd(ddsPath);
        foreach (var (key, index) in source.Entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!wantedKeys.Contains(key))
            {
                continue;
            }
            entries.Add((key, DecodeBc1DdsArraySlice(dds, index - 1), source.TileWidth, source.TileHeight));
        }
    }

    const int atlasWidth = 2048;
    var x = 0;
    var y = 0;
    var rowHeight = 0;
    var coords = new Dictionary<string, RectDto>(StringComparer.Ordinal);
    var placements = new List<(byte[] Rgba, int Width, int Height, int X, int Y)>();
    foreach (var entry in entries)
    {
        if (x + entry.Width > atlasWidth)
        {
            x = 0;
            y += rowHeight;
            rowHeight = 0;
        }
        coords[entry.Key] = new RectDto(x, y, entry.Width, entry.Height);
        placements.Add((entry.Rgba, entry.Width, entry.Height, x, y));
        x += entry.Width;
        rowHeight = Math.Max(rowHeight, entry.Height);
    }
    var atlasHeight = Math.Max(1, y + rowHeight);
    var atlas = new byte[atlasWidth * atlasHeight * 4];
    foreach (var placement in placements)
    {
        for (var row = 0; row < placement.Height; row++)
        {
            Buffer.BlockCopy(
                placement.Rgba,
                row * placement.Width * 4,
                atlas,
                ((placement.Y + row) * atlasWidth + placement.X) * 4,
                placement.Width * 4);
        }
    }
    WritePng(Path.Combine(outDir, outputRelativePath), atlasWidth, atlasHeight, atlas);
    return coords;
}

static byte[] ReadZstd(string source)
{
    var psi = new ProcessStartInfo("zstd")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    psi.ArgumentList.Add("-dc");
    psi.ArgumentList.Add(source);
    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start zstd.");
    using var output = new MemoryStream();
    process.StandardOutput.BaseStream.CopyTo(output);
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"zstd failed: {stderr}");
    }
    return output.ToArray();
}

static byte[] DecodeBc1DdsArraySlice(byte[] dds, int sliceIndex)
{
    if (dds.Length < 148 || dds[0] != 'D' || dds[1] != 'D' || dds[2] != 'S' || dds[3] != ' ')
    {
        throw new InvalidDataException("Invalid DDS data.");
    }
    var height = ReadInt32LE(dds, 12);
    var width = ReadInt32LE(dds, 16);
    var mipCount = Math.Max(1, ReadInt32LE(dds, 28));
    var format = ReadInt32LE(dds, 128);
    if (format != 71)
    {
        throw new InvalidDataException($"Expected BC1 DDS, got DXGI format {format}.");
    }
    var arraySize = ReadInt32LE(dds, 140);
    if (sliceIndex < 0 || sliceIndex >= arraySize)
    {
        throw new InvalidDataException($"DDS slice {sliceIndex} is outside array size {arraySize}.");
    }

    var dataOffset = 148;
    var bytesPerSlice = 0;
    var mw = width;
    var mh = height;
    for (var mip = 0; mip < mipCount; mip++)
    {
        bytesPerSlice += Bc1MipSize(mw, mh);
        mw = Math.Max(1, mw / 2);
        mh = Math.Max(1, mh / 2);
    }
    var sliceOffset = dataOffset + sliceIndex * bytesPerSlice;
    var rgba = new byte[width * height * 4];
    var blockOffset = sliceOffset;
    var blocksWide = Math.Max(1, (width + 3) / 4);
    var blocksHigh = Math.Max(1, (height + 3) / 4);
    for (var by = 0; by < blocksHigh; by++)
    {
        for (var bx = 0; bx < blocksWide; bx++)
        {
            DecodeBc1Block(dds, blockOffset, rgba, width, height, bx * 4, by * 4);
            blockOffset += 8;
        }
    }
    return rgba;
}

static int Bc1MipSize(int width, int height) =>
    Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8;

static void DecodeBc1Block(byte[] data, int offset, byte[] rgba, int width, int height, int px, int py)
{
    var c0 = ReadUInt16LE(data, offset);
    var c1 = ReadUInt16LE(data, offset + 2);
    Span<byte> colors = stackalloc byte[16];
    DecodeRgb565(c0, colors, 0);
    DecodeRgb565(c1, colors, 4);
    colors[3] = 255;
    colors[7] = 255;
    if (c0 > c1)
    {
        for (var i = 0; i < 3; i++)
        {
            colors[8 + i] = (byte)((2 * colors[i] + colors[4 + i]) / 3);
            colors[12 + i] = (byte)((colors[i] + 2 * colors[4 + i]) / 3);
        }
        colors[11] = 255;
        colors[15] = 255;
    }
    else
    {
        for (var i = 0; i < 3; i++)
        {
            colors[8 + i] = (byte)((colors[i] + colors[4 + i]) / 2);
            colors[12 + i] = 0;
        }
        colors[11] = 255;
        colors[15] = 0;
    }

    var bits = (uint)ReadInt32LE(data, offset + 4);
    for (var y = 0; y < 4; y++)
    {
        for (var x = 0; x < 4; x++)
        {
            var tx = px + x;
            var ty = py + y;
            if (tx >= width || ty >= height)
            {
                continue;
            }
            var code = (int)((bits >> (2 * (4 * y + x))) & 0x3);
            var src = code * 4;
            var dst = (ty * width + tx) * 4;
            rgba[dst] = colors[src];
            rgba[dst + 1] = colors[src + 1];
            rgba[dst + 2] = colors[src + 2];
            rgba[dst + 3] = colors[src + 3];
        }
    }
}

static void DecodeRgb565(ushort value, Span<byte> colors, int offset)
{
    var r = (value >> 11) & 0x1F;
    var g = (value >> 5) & 0x3F;
    var b = value & 0x1F;
    colors[offset] = (byte)((r << 3) | (r >> 2));
    colors[offset + 1] = (byte)((g << 2) | (g >> 4));
    colors[offset + 2] = (byte)((b << 3) | (b >> 2));
}

static int ReadInt32LE(byte[] data, int offset) =>
    data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);

static ushort ReadUInt16LE(byte[] data, int offset) =>
    (ushort)(data[offset] | (data[offset + 1] << 8));

static void WritePng(string path, int width, int height, byte[] rgba)
{
    using var file = File.Create(path);
    file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
    WriteChunk(file, "IHDR", BuildIhdr(width, height));
    using var raw = new MemoryStream();
    for (var y = 0; y < height; y++)
    {
        raw.WriteByte(0);
        raw.Write(rgba, y * width * 4, width * 4);
    }
    using var compressed = new MemoryStream();
    using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
    {
        raw.Position = 0;
        raw.CopyTo(zlib);
    }
    WriteChunk(file, "IDAT", compressed.ToArray());
    WriteChunk(file, "IEND", []);
}

static byte[] BuildIhdr(int width, int height)
{
    var ihdr = new byte[13];
    WriteBigEndian(ihdr, 0, width);
    WriteBigEndian(ihdr, 4, height);
    ihdr[8] = 8;
    ihdr[9] = 6;
    return ihdr;
}

static void WriteChunk(Stream stream, string type, byte[] data)
{
    Span<byte> len = stackalloc byte[4];
    WriteBigEndian(len, 0, data.Length);
    stream.Write(len);
    var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
    stream.Write(typeBytes);
    stream.Write(data);
    var crc = Crc32(typeBytes, data);
    Span<byte> crcBytes = stackalloc byte[4];
    WriteBigEndian(crcBytes, 0, unchecked((int)crc));
    stream.Write(crcBytes);
}

static void WriteBigEndian(Span<byte> data, int offset, int value)
{
    data[offset] = (byte)((value >> 24) & 0xFF);
    data[offset + 1] = (byte)((value >> 16) & 0xFF);
    data[offset + 2] = (byte)((value >> 8) & 0xFF);
    data[offset + 3] = (byte)(value & 0xFF);
}

static uint Crc32(byte[] type, byte[] data)
{
    var crc = 0xFFFFFFFFu;
    foreach (var b in type)
    {
        crc = UpdateCrc(crc, b);
    }
    foreach (var b in data)
    {
        crc = UpdateCrc(crc, b);
    }
    return crc ^ 0xFFFFFFFFu;
}

static uint UpdateCrc(uint crc, byte b)
{
    crc ^= b;
    for (var i = 0; i < 8; i++)
    {
        crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
    }
    return crc;
}

static string? FirstExisting(string dir, params string[] files)
{
    foreach (var file in files)
    {
        var path = Path.Combine(dir, file);
        if (File.Exists(path))
        {
            return path;
        }
    }
    return null;
}

static void ConvertZstdDds(string source, string target)
{
    if (!File.Exists(source))
    {
        throw new FileNotFoundException("Required DDS/ZST source was not found.", source);
    }
    var tempDds = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dds");
    try
    {
        Run("zstd", ["-dc", source], tempDds);
        Run("magick", [tempDds, target], null);
    }
    finally
    {
        File.Delete(tempDds);
    }
}

static void AppendImages(IReadOnlyList<string> inputs, string target)
{
    var args = new List<string>();
    args.AddRange(inputs);
    args.Add("-background");
    args.Add("none");
    args.Add("-append");
    args.Add(target);
    Run("magick", args, null);
}

static void CreateTransparentPng(string target, int width, int height) =>
    Run("magick", ["-size", $"{width}x{height}", "xc:none", target], null);

static void Run(string fileName, IReadOnlyList<string> arguments, string? redirectStdout)
{
    var psi = new ProcessStartInfo(fileName)
    {
        UseShellExecute = false,
        RedirectStandardError = true,
        RedirectStandardOutput = redirectStdout is not null,
    };
    foreach (var arg in arguments)
    {
        psi.ArgumentList.Add(arg);
    }

    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}.");
    if (redirectStdout is not null)
    {
        using var output = File.Create(redirectStdout);
        process.StandardOutput.BaseStream.CopyTo(output);
    }
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{fileName} failed: {stderr}");
    }
}

static (int Width, int Height) PngSize(string path)
{
    using var stream = File.OpenRead(path);
    Span<byte> header = stackalloc byte[24];
    if (stream.Read(header) != header.Length)
    {
        throw new InvalidDataException($"PNG is too small: {path}");
    }
    var width = ReadBigEndian(header[16..20]);
    var height = ReadBigEndian(header[20..24]);
    return (width, height);
}

static int ReadBigEndian(ReadOnlySpan<byte> bytes) =>
    (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

public sealed class SpriteMapDto
{
    [JsonPropertyName("atlases")] public SortedDictionary<string, AtlasDto> Atlases { get; set; } = new(StringComparer.Ordinal);
}

public sealed class AtlasDto
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
    [JsonPropertyName("coords")] public Dictionary<string, RectDto> Coords { get; set; } = new(StringComparer.Ordinal);
}

public sealed record RectDto(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("w")] int W,
    [property: JsonPropertyName("h")] int H);

public sealed record SpriteSource(
    string Texture,
    int TileWidth,
    int TileHeight,
    IReadOnlyDictionary<string, int> Entries);
