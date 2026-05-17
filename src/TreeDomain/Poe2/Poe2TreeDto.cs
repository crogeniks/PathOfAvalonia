using System.Text.Json.Serialization;
using System.Text.Json;

namespace PathOfAvalonia.TreeDomain.Poe2;

public sealed class Poe2TreeDto
{
    [JsonPropertyName("min_x")] public double MinX { get; set; }
    [JsonPropertyName("min_y")] public double MinY { get; set; }
    [JsonPropertyName("max_x")] public double MaxX { get; set; }
    [JsonPropertyName("max_y")] public double MaxY { get; set; }
    [JsonPropertyName("constants")] public Poe2ConstantsDto Constants { get; set; } = new();
    [JsonPropertyName("groups")] public Poe2GroupDto?[] Groups { get; set; } = [];
    [JsonPropertyName("nodes")] public Dictionary<string, Poe2NodeDto> Nodes { get; set; } = new();
    [JsonPropertyName("classes")] public Poe2ClassDto[] Classes { get; set; } = [];
}

public sealed class Poe2ConstantsDto
{
    [JsonPropertyName("orbitRadii")] public double[] OrbitRadii { get; set; } = [];
    [JsonPropertyName("orbitAnglesByOrbit")] public double[][] OrbitAnglesByOrbit { get; set; } = [];
}

public sealed class Poe2GroupDto
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

public sealed class Poe2ClassDto
{
    [JsonPropertyName("integerId")] public int IntegerId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("ascendancies")] public Poe2AscendancyDto[] Ascendancies { get; set; } = [];
}

public sealed class Poe2AscendancyDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("internalId")] public string? InternalId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class Poe2NodeDto
{
    public int IdFromKey { get; set; }
    [JsonPropertyName("skill")] public int? Skill { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("connectionArt")] public string? ConnectionArt { get; set; }
    [JsonPropertyName("activeEffectImage")] public string? ActiveEffectImage { get; set; }
    [JsonPropertyName("group")] public int? Group { get; set; }
    [JsonPropertyName("orbit")] public int? Orbit { get; set; }
    [JsonPropertyName("orbitIndex")] public int? OrbitIndex { get; set; }
    [JsonPropertyName("connections")] public Poe2ConnectionDto[]? Connections { get; set; }
    [JsonPropertyName("isNotable")] public bool IsNotable { get; set; }
    [JsonPropertyName("isKeystone")] public bool IsKeystone { get; set; }
    [JsonPropertyName("isOnlyImage")] public bool IsOnlyImage { get; set; }
    [JsonPropertyName("isJewelSocket")] public bool IsJewelSocket { get; set; }
    [JsonPropertyName("isAscendancyStart")] public bool IsAscendancyStart { get; set; }
    [JsonPropertyName("isAttribute")] public bool IsAttribute { get; set; }
    [JsonPropertyName("ascendancyName")] public string? AscendancyName { get; set; }
    [JsonPropertyName("stats")] public string[]? Stats { get; set; }
    [JsonPropertyName("options")] public JsonElement? Options { get; set; }
    [JsonPropertyName("nodeOverlay")] public Poe2NodeOverlayDto? NodeOverlay { get; set; }
    [JsonPropertyName("classesStart")] public string[]? ClassesStart { get; set; }
}

public sealed class Poe2ConnectionDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("orbit")] public int Orbit { get; set; }
}

public sealed class Poe2NodeOverlayDto
{
    [JsonPropertyName("alloc")] public string? Alloc { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("unalloc")] public string? Unalloc { get; set; }
}
