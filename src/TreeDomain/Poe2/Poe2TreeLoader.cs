using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathOfAvalonia.TreeDomain.Poe2;

public sealed class Poe2TreeLoader : ITreeLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public TreeModel Load(Stream stream, string version, GameId gameId)
    {
        var dto = JsonSerializer.Deserialize<Poe2TreeDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("tree JSON was null");
        return Build(dto, version, gameId);
    }

    private static TreeModel Build(Poe2TreeDto dto, string version, GameId gameId)
    {
        var classCatalog = BuildClassCatalog(dto.Classes);
        var classIndexByName = classCatalog.Classes.ToDictionary(c => c.Name, c => c.ClassIndex, StringComparer.Ordinal);

        var orbitRadii = dto.Constants.OrbitRadii;
        var orbitAngles = dto.Constants.OrbitAnglesByOrbit;
        var groupPositions = new Dictionary<int, GroupPosition>(dto.Groups.Length);
        for (var groupIndex = 0; groupIndex < dto.Groups.Length; groupIndex++)
        {
            var group = dto.Groups[groupIndex];
            if (group is null)
            {
                continue;
            }
            groupPositions[groupIndex + 1] = new GroupPosition(group.X, group.Y);
        }

        var nodes = new Dictionary<int, Node>(dto.Nodes.Count);
        var orbitInfo = new Dictionary<int, (int Group, int Orbit, double Cx, double Cy, double Angle, double Radius)>(dto.Nodes.Count);

        foreach (var (key, nd) in dto.Nodes)
        {
            if (!int.TryParse(key, out var id)
                || nd.Group is null
                || !groupPositions.TryGetValue(nd.Group.Value, out var grp)
                || nd.Orbit is null
                || nd.OrbitIndex is null)
            {
                continue;
            }
            nd.IdFromKey = id;
            var orbit = nd.Orbit.Value;
            var orbitIndex = nd.OrbitIndex.Value;
            if (orbit < 0 || orbit >= orbitRadii.Length || orbit >= orbitAngles.Length)
            {
                continue;
            }
            var angles = orbitAngles[orbit];
            if (orbitIndex < 0 || orbitIndex >= angles.Length)
            {
                continue;
            }

            var angle = angles[orbitIndex];
            var radius = orbitRadii[orbit];
            var x = grp.X + Math.Sin(angle) * radius;
            var y = grp.Y - Math.Cos(angle) * radius;
            var classStartIndexes = ResolveClassStartIndexes(nd, classIndexByName);
            var classStartIndex = classStartIndexes.Count > 0 ? classStartIndexes[0] : (int?)null;
            var type = ClassifyNode(nd, classStartIndexes);
            nodes[id] = new Node
            {
                Id = id,
                Name = nd.Name ?? $"Node {id}",
                Type = type,
                X = x,
                Y = y,
                Icon = nd.Icon,
                Stats = NormalizeLines(nd.Stats),
                AscendancyName = nd.AscendancyName,
                ClassStartIndex = classStartIndex,
                ClassStartIndexes = classStartIndexes,
                GroupId = nd.Group.Value,
                Orbit = orbit,
                OrbitIndex = orbitIndex,
                ExpansionSocket = null,
                MasteryEffects = null,
            };
            orbitInfo[id] = (nd.Group.Value, orbit, grp.X, grp.Y, angle, radius);
        }

        var connectorSet = new HashSet<(int, int)>();
        var connectors = new List<Connector>();
        foreach (var (key, nd) in dto.Nodes)
        {
            if (!int.TryParse(key, out var id) || !nodes.TryGetValue(id, out var me))
            {
                continue;
            }
            foreach (var conn in nd.Connections ?? [])
            {
                if (!nodes.TryGetValue(conn.Id, out var other) || ReferenceEquals(me, other))
                {
                    continue;
                }
                if (me.AscendancyName != other.AscendancyName)
                {
                    continue;
                }

                if (!me.LinkedNodes.Contains(other))
                {
                    me.LinkedNodes.Add(other);
                }
                if (!other.LinkedNodes.Contains(me))
                {
                    other.LinkedNodes.Add(me);
                }

                if (me.Type == NodeType.Mastery
                    || other.Type == NodeType.Mastery
                    || (me.Type == NodeType.ClassStart && other.Type == NodeType.AscendancyStart)
                    || (me.Type == NodeType.AscendancyStart && other.Type == NodeType.ClassStart))
                {
                    continue;
                }
                var a = Math.Min(id, conn.Id);
                var b = Math.Max(id, conn.Id);
                if (!connectorSet.Add((a, b)))
                {
                    continue;
                }
                var ia = orbitInfo[a];
                var ib = orbitInfo[b];
                if (TryBuildConnectionArc(me, other, conn.Orbit, orbitRadii, out var connectionArc))
                {
                    connectors.Add(connectionArc);
                }
                else if (conn.Orbit == int.MaxValue)
                {
                    continue;
                }
                else if (ia.Group == ib.Group && ia.Orbit == ib.Orbit && ia.Radius > 0)
                {
                    var sweep = ib.Angle - ia.Angle;
                    while (sweep > Math.PI)
                    {
                        sweep -= Math.Tau;
                    }
                    while (sweep <= -Math.PI)
                    {
                        sweep += Math.Tau;
                    }
                    connectors.Add(new ArcConnector(a, b, ia.Cx, ia.Cy, ia.Radius, ia.Angle, sweep));
                }
                else
                {
                    var na = nodes[a];
                    var nb = nodes[b];
                    connectors.Add(new LineConnector(a, b, na.X, na.Y, nb.X, nb.Y));
                }
            }
        }

        var skillsPerOrbit = orbitAngles.Select(a => Math.Max(0, a.Length)).ToArray();
        return new TreeModel
        {
            GameId = gameId,
            Version = version,
            Classes = classCatalog,
            Nodes = nodes,
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = connectors,
            Bounds = new TreeBounds(dto.MinX, dto.MinY, dto.MaxX, dto.MaxY),
            Groups = groupPositions,
            SkillsPerOrbit = skillsPerOrbit,
            OrbitRadii = orbitRadii,
            OrbitAngles = orbitAngles,
        };
    }

    private static bool TryBuildConnectionArc(
        Node from,
        Node to,
        int connectionOrbit,
        IReadOnlyList<double> orbitRadii,
        out ArcConnector connector)
    {
        connector = null!;
        if (connectionOrbit == 0)
        {
            return false;
        }
        var orbit = Math.Abs(connectionOrbit);
        if (orbit < 0 || orbit >= orbitRadii.Count)
        {
            return false;
        }

        var radius = orbitRadii[orbit];
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= 0 || dist >= radius * 2)
        {
            return false;
        }

        var perp = Math.Sqrt(radius * radius - dist * dist / 4) * (connectionOrbit > 0 ? 1 : -1);
        var cx = from.X + dx / 2 + perp * (dy / dist);
        var cy = from.Y + dy / 2 - perp * (dx / dist);

        var angle1 = Math.Atan2(from.Y - cy, from.X - cx);
        var angle2 = Math.Atan2(to.Y - cy, to.X - cx);
        if (angle1 > angle2)
        {
            (angle1, angle2) = (angle2, angle1);
        }
        var arcAngle = angle2 - angle1;
        if (arcAngle >= Math.PI)
        {
            (angle1, angle2) = (angle2, angle1);
            arcAngle = Math.Tau - arcAngle;
        }
        if (arcAngle > Math.PI)
        {
            return false;
        }

        connector = new ArcConnector(
            from.Id,
            to.Id,
            cx,
            cy,
            radius,
            angle1 + Math.PI / 2,
            arcAngle);
        return true;
    }

    private static ClassCatalog BuildClassCatalog(IReadOnlyList<Poe2ClassDto> classes)
    {
        var result = new List<CharacterClassInfo>(classes.Count);
        for (var i = 0; i < classes.Count; i++)
        {
            var ascendancies = new List<AscendancyInfo> { new(0, "None", string.Empty, null) };
            for (var j = 0; j < classes[i].Ascendancies.Length; j++)
            {
                var a = classes[i].Ascendancies[j];
                ascendancies.Add(new AscendancyInfo(j + 1, a.Name, a.Id ?? a.Name, a.InternalId));
            }
            result.Add(new CharacterClassInfo(i, classes[i].IntegerId, classes[i].Name, ascendancies));
        }
        return new ClassCatalog { Classes = result };
    }

    private static IReadOnlyList<int> ResolveClassStartIndexes(Poe2NodeDto nd, Dictionary<string, int> classIndexByName)
    {
        var result = new List<int>();
        foreach (var className in nd.ClassesStart ?? [])
        {
            if (classIndexByName.TryGetValue(className, out var classIndex)
                && !result.Contains(classIndex))
            {
                result.Add(classIndex);
            }
        }
        return result;
    }

    private static NodeType ClassifyNode(Poe2NodeDto nd, IReadOnlyList<int> classStartIndexes)
    {
        if (classStartIndexes.Count > 0)
        {
            return NodeType.ClassStart;
        }
        if (nd.IsAscendancyStart)
        {
            return NodeType.AscendancyStart;
        }
        if (nd.IsKeystone)
        {
            return NodeType.Keystone;
        }
        if (nd.IsOnlyImage && (nd.Name?.Contains("Mastery", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return NodeType.Mastery;
        }
        if (nd.IsJewelSocket)
        {
            return NodeType.JewelSocket;
        }
        if (nd.IsNotable)
        {
            return NodeType.Notable;
        }
        return NodeType.Normal;
    }

    private static IReadOnlyList<string> NormalizeLines(string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return Array.Empty<string>();
        }
        var lines = new List<string>();
        foreach (var value in values)
        {
            foreach (var line in value.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }
        }
        return lines;
    }
}
