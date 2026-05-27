using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathOfAvalonia.TreeDomain.Poe2;

public sealed class Poe2LegacyTreeDto
{
    [JsonPropertyName("min_x")] public double MinX { get; set; }
    [JsonPropertyName("min_y")] public double MinY { get; set; }
    [JsonPropertyName("max_x")] public double MaxX { get; set; }
    [JsonPropertyName("max_y")] public double MaxY { get; set; }
    [JsonPropertyName("constants")] public Poe2LegacyConstantsDto Constants { get; set; } = new();
    [JsonPropertyName("groups")] public Poe2LegacyGroupDto?[] Groups { get; set; } = [];
    [JsonPropertyName("nodes")] public Dictionary<string, Poe2LegacyNodeDto> Nodes { get; set; } = new();
    [JsonPropertyName("classes")] public Poe2LegacyClassDto[] Classes { get; set; } = [];
}

public sealed class Poe2LegacyConstantsDto
{
    [JsonPropertyName("orbitRadii")] public double[] OrbitRadii { get; set; } = [];
    [JsonPropertyName("orbitAnglesByOrbit")] public double[][] OrbitAnglesByOrbit { get; set; } = [];
}

public sealed class Poe2LegacyGroupDto
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

public sealed class Poe2LegacyClassDto
{
    [JsonPropertyName("integerId")] public int IntegerId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("ascendancies")] public Poe2LegacyAscendancyDto[] Ascendancies { get; set; } = [];
}

public sealed class Poe2LegacyAscendancyDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("internalId")] public string? InternalId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class Poe2LegacyNodeDto
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
    [JsonPropertyName("connections")] public Poe2LegacyConnectionDto[]? Connections { get; set; }
    [JsonPropertyName("isNotable")] public bool IsNotable { get; set; }
    [JsonPropertyName("isKeystone")] public bool IsKeystone { get; set; }
    [JsonPropertyName("isOnlyImage")] public bool IsOnlyImage { get; set; }
    [JsonPropertyName("isJewelSocket")] public bool IsJewelSocket { get; set; }
    [JsonPropertyName("isAscendancyStart")] public bool IsAscendancyStart { get; set; }
    [JsonPropertyName("isAttribute")] public bool IsAttribute { get; set; }
    [JsonPropertyName("ascendancyName")] public string? AscendancyName { get; set; }
    [JsonPropertyName("stats")] public string[]? Stats { get; set; }
    [JsonPropertyName("options")] public JsonElement? Options { get; set; }
    [JsonPropertyName("nodeOverlay")] public Poe2LegacyNodeOverlayDto? NodeOverlay { get; set; }
    [JsonPropertyName("classesStart")] public string[]? ClassesStart { get; set; }
}

public sealed class Poe2LegacyConnectionDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("orbit")] public int Orbit { get; set; }
}

public sealed class Poe2LegacyNodeOverlayDto
{
    [JsonPropertyName("alloc")] public string? Alloc { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("unalloc")] public string? Unalloc { get; set; }
}

public sealed class Poe2GggTreeDto
{
    [JsonPropertyName("tree")] public string Tree { get; set; } = string.Empty;
    [JsonPropertyName("min_x")] public double MinX { get; set; }
    [JsonPropertyName("min_y")] public double MinY { get; set; }
    [JsonPropertyName("max_x")] public double MaxX { get; set; }
    [JsonPropertyName("max_y")] public double MaxY { get; set; }
    [JsonPropertyName("groups")] public Dictionary<string, Poe2GggGroupDto> Groups { get; set; } = new();
    [JsonPropertyName("nodes")] public Dictionary<string, Poe2GggNodeDto> Nodes { get; set; } = new();
    [JsonPropertyName("edges")] public Poe2GggEdgeDto[] Edges { get; set; } = [];
    [JsonPropertyName("classes")] public Poe2GggClassDto[] Classes { get; set; } = [];
}

public sealed class Poe2GggGroupDto
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("orbits")] public int[] Orbits { get; set; } = [];
    [JsonPropertyName("nodes")] public string[] Nodes { get; set; } = [];
}

public sealed class Poe2GggClassDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("integerId")] public int? IntegerId { get; set; }
    [JsonPropertyName("ascendancies")] public Poe2GggAscendancyDto[] Ascendancies { get; set; } = [];
}

public sealed class Poe2GggAscendancyDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("internalId")] public string? InternalId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class Poe2GggNodeDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("skill")] public int? Skill { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("group")] public int? Group { get; set; }
    [JsonPropertyName("orbit")] public int? Orbit { get; set; }
    [JsonPropertyName("orbitIndex")] public int? OrbitIndex { get; set; }
    [JsonPropertyName("x")] public double? X { get; set; }
    [JsonPropertyName("y")] public double? Y { get; set; }
    [JsonPropertyName("out")] public string[]? Out { get; set; }
    [JsonPropertyName("in")] public string[]? In { get; set; }
    [JsonPropertyName("edges")] public int[]? Edges { get; set; }
    [JsonPropertyName("isNotable")] public bool IsNotable { get; set; }
    [JsonPropertyName("isKeystone")] public bool IsKeystone { get; set; }
    [JsonPropertyName("isOnlyImage")] public bool IsOnlyImage { get; set; }
    [JsonPropertyName("isJewelSocket")] public bool IsJewelSocket { get; set; }
    [JsonPropertyName("isAscendancyStart")] public bool IsAscendancyStart { get; set; }
    [JsonPropertyName("isAttribute")] public bool IsAttribute { get; set; }
    [JsonPropertyName("ascendancyId")] public string? AscendancyId { get; set; }
    [JsonPropertyName("stats")] public string[]? Stats { get; set; }
    [JsonPropertyName("classStartIndex")] public int[]? ClassStartIndex { get; set; }
    [JsonPropertyName("grantedSkill")] public JsonElement? GrantedSkill { get; set; }
}

public sealed class Poe2GggEdgeDto
{
    [JsonPropertyName("from")] public JsonElement From { get; set; }
    [JsonPropertyName("to")] public JsonElement To { get; set; }
    [JsonPropertyName("orbit")] public int? Orbit { get; set; }
    [JsonPropertyName("orbitX")] public double? OrbitX { get; set; }
    [JsonPropertyName("orbitY")] public double? OrbitY { get; set; }
}
