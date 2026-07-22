using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PathOfAvalonia.TreeDomain.Atlas;

public sealed class Poe1AtlasTreeLoader
{
    private static readonly Regex StatLinkRegex = new(
        @"\[[^|\]]+\|([^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public AtlasTreeModel Load(Stream stream, string version, GameId gameId)
    {
        var dto = JsonSerializer.Deserialize<TreeDto>(stream, JsonOptions)
            ?? throw new InvalidDataException("Atlas tree JSON was null.");
        if (!string.Equals(dto.Tree, "Atlas", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The supplied JSON is not an Atlas passive tree.");
        }
        if (!dto.Nodes.TryGetValue("root", out var root)
            || root.Out?.FirstOrDefault() is not { } startNodeId
            || startNodeId <= 0)
        {
            throw new InvalidDataException("The Atlas tree does not contain its single root connection.");
        }

        var orbitAngles = BuildOrbitAngles(dto.Constants.SkillsPerOrbit);
        var groups = dto.Groups
            .Where(pair => int.TryParse(pair.Key, out _))
            .ToDictionary(
                pair => int.Parse(pair.Key),
                pair => new GroupPosition(pair.Value.X, pair.Value.Y));
        var nodes = new Dictionary<int, AtlasNode>();
        var orbitInfo = new Dictionary<int, OrbitInfo>();
        foreach (var (key, source) in dto.Nodes)
        {
            if (!TryNodeId(key, source, out var nodeId)
                || source.Group is not { } groupId
                || !dto.Groups.TryGetValue(groupId.ToString(), out var group)
                || source.Orbit is not { } orbit
                || source.OrbitIndex is not { } orbitIndex
                || orbit < 0
                || orbit >= dto.Constants.OrbitRadii.Length
                || orbit >= orbitAngles.Length
                || orbitIndex < 0
                || orbitIndex >= orbitAngles[orbit].Length)
            {
                continue;
            }

            var angle = orbitAngles[orbit][orbitIndex];
            var radius = dto.Constants.OrbitRadii[orbit];
            var x = group.X + Math.Sin(angle) * radius;
            var y = group.Y - Math.Cos(angle) * radius;
            nodes[nodeId] = new AtlasNode
            {
                Id = nodeId,
                Name = source.Name ?? string.Empty,
                Type = Classify(source, nodeId == startNodeId),
                X = x,
                Y = y,
                Icon = source.Icon,
                IsGateway = source.IsWormhole,
                Stats = NormalizeStatLines(source.Stats),
                ReminderText = NormalizeLines(source.ReminderText),
                FlavourText = NormalizeLines(source.FlavourText),
                GroupId = groupId,
                Orbit = orbit,
                OrbitIndex = orbitIndex,
            };
            orbitInfo[nodeId] = new OrbitInfo(groupId, orbit, group.X, group.Y, angle, radius);
        }

        if (!nodes.ContainsKey(startNodeId))
        {
            throw new InvalidDataException($"The Atlas start node {startNodeId} could not be resolved.");
        }

        var connectorIds = new HashSet<(int Min, int Max)>();
        var connectors = new List<Connector>();
        foreach (var (key, source) in dto.Nodes)
        {
            if (!TryNodeId(key, source, out var nodeId) || !nodes.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            foreach (var linkedId in Enumerable.Concat(source.Out ?? [], source.In ?? []))
            {
                if (!nodes.TryGetValue(linkedId, out var linked) || linkedId == nodeId)
                {
                    continue;
                }
                if (!node.LinkedNodes.Any(candidate => candidate.Id == linkedId))
                {
                    node.LinkedNodes.Add(linked);
                }
                if (node.Type == AtlasNodeType.ClusterIcon
                    || linked.Type == AtlasNodeType.ClusterIcon
                    || (node.IsGateway && linked.IsGateway))
                {
                    continue;
                }

                var edge = (Math.Min(nodeId, linkedId), Math.Max(nodeId, linkedId));
                if (!connectorIds.Add(edge))
                {
                    continue;
                }

                var first = nodes[edge.Item1];
                var second = nodes[edge.Item2];
                var firstOrbit = orbitInfo[edge.Item1];
                var secondOrbit = orbitInfo[edge.Item2];
                if (firstOrbit.GroupId == secondOrbit.GroupId
                    && firstOrbit.Orbit == secondOrbit.Orbit
                    && firstOrbit.Radius > 0)
                {
                    var sweep = secondOrbit.Angle - firstOrbit.Angle;
                    while (sweep > Math.PI)
                    {
                        sweep -= Math.Tau;
                    }
                    while (sweep <= -Math.PI)
                    {
                        sweep += Math.Tau;
                    }
                    connectors.Add(new ArcConnector(
                        edge.Item1,
                        edge.Item2,
                        firstOrbit.CenterX,
                        firstOrbit.CenterY,
                        firstOrbit.Radius,
                        firstOrbit.Angle,
                        sweep));
                }
                else
                {
                    connectors.Add(new LineConnector(
                        edge.Item1,
                        edge.Item2,
                        first.X,
                        first.Y,
                        second.X,
                        second.Y));
                }
            }
        }

        var visuals = dto.Groups
            .Where(pair => pair.Value.Background?.Image is { Length: > 0 } && int.TryParse(pair.Key, out _))
            .Select(pair => new AtlasGroupVisual(
                int.Parse(pair.Key),
                pair.Value.X,
                pair.Value.Y,
                "groupBackground",
                pair.Value.Background!.Image!))
            .ToList();
        var start = nodes[startNodeId];
        visuals.Add(new AtlasGroupVisual(
            start.GroupId,
            start.X,
            start.Y,
            "startNode",
            "AtlasPassiveSkillScreenStart"));

        return new AtlasTreeModel
        {
            GameId = gameId,
            Version = version,
            StartNodeId = startNodeId,
            PointLimit = dto.Points.TotalPoints > 0 ? dto.Points.TotalPoints : 138,
            Nodes = nodes,
            Connectors = connectors,
            Bounds = new TreeBounds(dto.MinX, dto.MinY, dto.MaxX, dto.MaxY),
            Groups = groups,
            SkillsPerOrbit = dto.Constants.SkillsPerOrbit,
            OrbitRadii = dto.Constants.OrbitRadii,
            OrbitAngles = orbitAngles,
            GroupVisuals = visuals,
        };
    }

    private static AtlasNodeType Classify(NodeDto node, bool isStart) =>
        isStart ? AtlasNodeType.Start
        : node.IsMastery ? AtlasNodeType.ClusterIcon
        : node.IsKeystone ? AtlasNodeType.Keystone
        : node.IsNotable ? AtlasNodeType.Notable
        : AtlasNodeType.Normal;

    private static bool TryNodeId(string key, NodeDto node, out int nodeId)
    {
        if (node.Skill is > 0 and var skill)
        {
            nodeId = skill;
            return true;
        }
        return int.TryParse(key, out nodeId);
    }

    private static IReadOnlyList<string> NormalizeLines(string[]? source)
    {
        if (source is null)
        {
            return [];
        }
        return source
            .SelectMany(line => line.Replace("\r\n", "\n").Split('\n'))
            .Select(line => StatLinkRegex.Replace(line.Trim(), match => match.Groups[1].Value))
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeStatLines(string[]? source)
    {
        if (source is null)
        {
            return [];
        }
        return source
            .Select(line => string.Join(
                ' ',
                line.Replace("\r\n", "\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Select(line => StatLinkRegex.Replace(line.Trim(), match => match.Groups[1].Value))
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static double[][] BuildOrbitAngles(int[] skillsPerOrbit)
    {
        var result = new double[skillsPerOrbit.Length][];
        for (var orbit = 0; orbit < skillsPerOrbit.Length; orbit++)
        {
            var count = skillsPerOrbit[orbit];
            var angles = new double[Math.Max(count, 1)];
            if (count == 16)
            {
                var degrees = new[] { 0, 30, 45, 60, 90, 120, 135, 150, 180, 210, 225, 240, 270, 300, 315, 330 };
                for (var index = 0; index < count; index++)
                {
                    angles[index] = degrees[index] * Math.PI / 180;
                }
            }
            else if (count == 40)
            {
                var degrees = new[]
                {
                    0, 10, 20, 30, 40, 45, 50, 60, 70, 80,
                    90, 100, 110, 120, 130, 135, 140, 150, 160, 170,
                    180, 190, 200, 210, 220, 225, 230, 240, 250, 260,
                    270, 280, 290, 300, 310, 315, 320, 330, 340, 350,
                };
                for (var index = 0; index < count; index++)
                {
                    angles[index] = degrees[index] * Math.PI / 180;
                }
            }
            else if (count > 0)
            {
                for (var index = 0; index < count; index++)
                {
                    angles[index] = index * Math.Tau / count;
                }
            }
            result[orbit] = angles;
        }
        return result;
    }

    private sealed record OrbitInfo(
        int GroupId,
        int Orbit,
        double CenterX,
        double CenterY,
        double Angle,
        double Radius);

    private sealed class TreeDto
    {
        [JsonPropertyName("tree")] public string? Tree { get; set; }
        [JsonPropertyName("min_x")] public double MinX { get; set; }
        [JsonPropertyName("min_y")] public double MinY { get; set; }
        [JsonPropertyName("max_x")] public double MaxX { get; set; }
        [JsonPropertyName("max_y")] public double MaxY { get; set; }
        [JsonPropertyName("constants")] public ConstantsDto Constants { get; set; } = new();
        [JsonPropertyName("points")] public PointsDto Points { get; set; } = new();
        [JsonPropertyName("groups")] public Dictionary<string, GroupDto> Groups { get; set; } = [];
        [JsonPropertyName("nodes")] public Dictionary<string, NodeDto> Nodes { get; set; } = [];
    }

    private sealed class ConstantsDto
    {
        [JsonPropertyName("skillsPerOrbit")] public int[] SkillsPerOrbit { get; set; } = [];
        [JsonPropertyName("orbitRadii")] public double[] OrbitRadii { get; set; } = [];
    }

    private sealed class PointsDto
    {
        [JsonPropertyName("totalPoints")] public int TotalPoints { get; set; }
    }

    private sealed class GroupDto
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
        [JsonPropertyName("background")] public GroupBackgroundDto? Background { get; set; }
    }

    private sealed class GroupBackgroundDto
    {
        [JsonPropertyName("image")] public string? Image { get; set; }
    }

    private sealed class NodeDto
    {
        [JsonPropertyName("skill")] public int? Skill { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("icon")] public string? Icon { get; set; }
        [JsonPropertyName("stats")] public string[]? Stats { get; set; }
        [JsonPropertyName("reminderText")] public string[]? ReminderText { get; set; }
        [JsonPropertyName("flavourText")] public string[]? FlavourText { get; set; }
        [JsonPropertyName("group")] public int? Group { get; set; }
        [JsonPropertyName("orbit")] public int? Orbit { get; set; }
        [JsonPropertyName("orbitIndex")] public int? OrbitIndex { get; set; }
        [JsonPropertyName("out")] public int[]? Out { get; set; }
        [JsonPropertyName("in")] public int[]? In { get; set; }
        [JsonPropertyName("isNotable")] public bool IsNotable { get; set; }
        [JsonPropertyName("isKeystone")] public bool IsKeystone { get; set; }
        [JsonPropertyName("isMastery")] public bool IsMastery { get; set; }
        [JsonPropertyName("isWormhole")] public bool IsWormhole { get; set; }
    }
}
