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

    private static readonly string[] PreferredClassOrder =
    [
        "Ranger",
        "Huntress",
        "Warrior",
        "Mercenary",
        "Druid",
        "Witch",
        "Sorceress",
        "Monk",
    ];

    public TreeModel Load(Stream stream, string version, GameId gameId)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        if (root.TryGetProperty("edges", out _))
        {
            var dto = root.Deserialize<Poe2GggTreeDto>(JsonOpts)
                      ?? throw new InvalidDataException("tree JSON was null");
            return BuildGgg(dto, version, gameId);
        }

        var legacy = root.Deserialize<Poe2LegacyTreeDto>(JsonOpts)
                     ?? throw new InvalidDataException("tree JSON was null");
        return BuildLegacy(legacy, version, gameId);
    }

    private static TreeModel BuildLegacy(Poe2LegacyTreeDto dto, string version, GameId gameId)
    {
        var classCatalog = BuildLegacyClassCatalog(dto.Classes);
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
        var orbitInfo = new Dictionary<int, OrbitInfo>(dto.Nodes.Count);

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
            var classStartIndexes = ResolveLegacyClassStartIndexes(nd, classIndexByName);
            var classStartIndex = classStartIndexes.Count > 0 ? classStartIndexes[0] : (int?)null;
            var type = ClassifyLegacyNode(nd, classStartIndexes);
            nodes[id] = new Node
            {
                Id = id,
                Name = nd.Name ?? $"Node {id}",
                Type = type,
                X = x,
                Y = y,
                Icon = nd.Icon,
                Visual = new NodeVisual(
                    nd.Icon,
                    nd.NodeOverlay?.Alloc,
                    nd.NodeOverlay?.Path,
                    nd.NodeOverlay?.Unalloc,
                    nd.ConnectionArt,
                    IconPathIsAssetPath: true),
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
            orbitInfo[id] = new OrbitInfo(nd.Group.Value, orbit, grp.X, grp.Y, angle, radius);
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

                LinkNodes(me, other);
                if (!NeedsDrawableConnector(me, other))
                {
                    continue;
                }

                var pair = OrderedPair(id, conn.Id);
                if (!connectorSet.Add(pair))
                {
                    continue;
                }

                if (TryBuildLegacyArc(me, other, conn.Orbit, orbitRadii, out var connectionArc))
                {
                    connectors.Add(connectionArc);
                    continue;
                }

                var ia = orbitInfo[pair.Item1];
                var ib = orbitInfo[pair.Item2];
                if (ia.Group == ib.Group && ia.Orbit == ib.Orbit && ia.Radius > 0)
                {
                    var sweep = NormalizeSweep(ib.Angle - ia.Angle);
                    connectors.Add(new ArcConnector(pair.Item1, pair.Item2, ia.Cx, ia.Cy, ia.Radius, ia.Angle, sweep));
                    continue;
                }

                var na = nodes[pair.Item1];
                var nb = nodes[pair.Item2];
                connectors.Add(new LineConnector(pair.Item1, pair.Item2, na.X, na.Y, nb.X, nb.Y));
            }
        }

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
            SkillsPerOrbit = orbitAngles.Select(a => Math.Max(0, a.Length)).ToArray(),
            OrbitRadii = orbitRadii,
            OrbitAngles = orbitAngles,
        };
    }

    private static TreeModel BuildGgg(Poe2GggTreeDto dto, string version, GameId gameId)
    {
        var classInfo = BuildGggClassCatalog(dto.Classes);
        var ascendancyNameById = BuildAscendancyNameById(dto.Classes);
        var groupPositions = dto.Groups
            .Where(pair => int.TryParse(pair.Key, out _))
            .ToDictionary(
                pair => int.Parse(pair.Key),
                pair => new GroupPosition(pair.Value.X, pair.Value.Y));

        var nodes = new Dictionary<int, Node>(dto.Nodes.Count);
        foreach (var (key, nd) in dto.Nodes)
        {
            if (!int.TryParse(key, out var id)
                || nd.Group is null
                || nd.Orbit is null
                || nd.OrbitIndex is null
                || nd.X is null
                || nd.Y is null)
            {
                continue;
            }

            var classStartIndexes = ResolveGggClassStartIndexes(nd.ClassStartIndex, classInfo.SourceToCatalogIndex);
            var classStartIndex = classStartIndexes.Count > 0 ? classStartIndexes[0] : (int?)null;
            var ascendancyName = ResolveAscendancyName(nd.AscendancyId, ascendancyNameById);

            nodes[id] = new Node
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(nd.Name) ? $"Node {id}" : nd.Name,
                Type = ClassifyGggNode(nd, classStartIndexes),
                X = nd.X.Value,
                Y = nd.Y.Value,
                Icon = string.IsNullOrWhiteSpace(nd.Icon) ? null : nd.Icon,
                Visual = new NodeVisual(
                    string.IsNullOrWhiteSpace(nd.Icon) ? null : nd.Icon,
                    null,
                    null,
                    null,
                    null,
                    IconPathIsAssetPath: true),
                Stats = NormalizeLines(nd.Stats),
                AscendancyName = ascendancyName,
                ClassStartIndex = classStartIndex,
                ClassStartIndexes = classStartIndexes,
                GroupId = nd.Group.Value,
                Orbit = nd.Orbit.Value,
                OrbitIndex = nd.OrbitIndex.Value,
                ExpansionSocket = null,
                MasteryEffects = null,
            };
        }

        var connectors = new List<Connector>();
        var connectorSet = new HashSet<(int, int)>();
        foreach (var edge in dto.Edges)
        {
            if (!TryReadNodeId(edge.From, out var fromId)
                || !TryReadNodeId(edge.To, out var toId)
                || !nodes.TryGetValue(fromId, out var from)
                || !nodes.TryGetValue(toId, out var to)
                || ReferenceEquals(from, to))
            {
                continue;
            }

            if (from.AscendancyName != to.AscendancyName)
            {
                continue;
            }

            LinkNodes(from, to);
            if (!NeedsDrawableConnector(from, to))
            {
                continue;
            }

            var pair = OrderedPair(fromId, toId);
            if (!connectorSet.Add(pair))
            {
                continue;
            }

            if (TryBuildGggArc(pair, from, to, edge, out var arc))
            {
                connectors.Add(arc);
            }
            else
            {
                connectors.Add(new LineConnector(pair.Item1, pair.Item2, from.X, from.Y, to.X, to.Y));
            }
        }

        var orbitData = BuildOrbitData(dto, groupPositions, nodes.Values);
        return new TreeModel
        {
            GameId = gameId,
            Version = version,
            Classes = classInfo.Catalog,
            Nodes = nodes,
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = connectors,
            Bounds = new TreeBounds(dto.MinX, dto.MinY, dto.MaxX, dto.MaxY),
            Groups = groupPositions,
            SkillsPerOrbit = orbitData.SkillsPerOrbit,
            OrbitRadii = orbitData.OrbitRadii,
            OrbitAngles = orbitData.OrbitAngles,
        };
    }

    private static (ClassCatalog Catalog, Dictionary<int, int> SourceToCatalogIndex) BuildGggClassCatalog(IReadOnlyList<Poe2GggClassDto> classes)
    {
        var sourceByName = classes
            .Select((cls, index) => (Class: cls, Index: index))
            .ToDictionary(pair => pair.Class.Name, pair => pair, StringComparer.Ordinal);

        var result = new List<CharacterClassInfo>(PreferredClassOrder.Length);
        var sourceToCatalogIndex = new Dictionary<int, int>();
        for (var catalogIndex = 0; catalogIndex < PreferredClassOrder.Length; catalogIndex++)
        {
            var className = PreferredClassOrder[catalogIndex];
            if (!sourceByName.TryGetValue(className, out var source))
            {
                continue;
            }

            sourceToCatalogIndex[source.Index] = catalogIndex;
            var ascendancies = new List<AscendancyInfo> { new(0, "None", string.Empty, null) };
            foreach (var ascendancy in source.Class.Ascendancies)
            {
                if (string.IsNullOrWhiteSpace(ascendancy.Name))
                {
                    continue;
                }

                ascendancies.Add(new AscendancyInfo(
                    ascendancies.Count,
                    ascendancy.Name,
                    ascendancy.Name,
                    ascendancy.InternalId ?? ascendancy.Id));
            }

            result.Add(new CharacterClassInfo(catalogIndex, source.Index, source.Class.Name, ascendancies));
        }

        return (new ClassCatalog { Classes = result }, sourceToCatalogIndex);
    }

    private static Dictionary<string, string> BuildAscendancyNameById(IEnumerable<Poe2GggClassDto> classes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cls in classes)
        {
            foreach (var ascendancy in cls.Ascendancies)
            {
                if (string.IsNullOrWhiteSpace(ascendancy.Id) || string.IsNullOrWhiteSpace(ascendancy.Name))
                {
                    continue;
                }

                result[ascendancy.Id] = ascendancy.Name;
            }
        }

        return result;
    }

    private static string? ResolveAscendancyName(string? ascendancyId, IReadOnlyDictionary<string, string> ascendancyNameById)
    {
        if (string.IsNullOrWhiteSpace(ascendancyId))
        {
            return null;
        }

        return ascendancyNameById.TryGetValue(ascendancyId, out var name)
            ? name
            : ascendancyId;
    }

    private static OrbitData BuildOrbitData(
        Poe2GggTreeDto dto,
        IReadOnlyDictionary<int, GroupPosition> groupPositions,
        IEnumerable<Node> nodes)
    {
        var maxOrbit = nodes.Any() ? nodes.Max(node => node.Orbit) : 0;
        var skillsPerOrbit = new int[maxOrbit + 1];
        var orbitRadii = new double[maxOrbit + 1];
        var orbitAngles = new IReadOnlyList<double>[maxOrbit + 1];

        for (var orbit = 0; orbit <= maxOrbit; orbit++)
        {
            var orbitNodes = nodes.Where(node => node.Orbit == orbit).ToArray();
            var count = orbitNodes.Length == 0 ? 1 : orbitNodes.Max(node => node.OrbitIndex) + 1;
            skillsPerOrbit[orbit] = count;

            var observedAngles = new double?[count];
            var radii = new List<double>();
            foreach (var node in orbitNodes)
            {
                if (!groupPositions.TryGetValue(node.GroupId, out var group))
                {
                    continue;
                }

                var dx = node.X - group.X;
                var dy = group.Y - node.Y;
                radii.Add(Math.Sqrt(dx * dx + dy * dy));
                if (node.OrbitIndex >= 0 && node.OrbitIndex < observedAngles.Length)
                {
                    observedAngles[node.OrbitIndex] ??= Math.Atan2(dx, dy);
                }
            }

            orbitRadii[orbit] = radii.Count == 0 ? 0 : radii.Average();
            orbitAngles[orbit] = Enumerable.Range(0, count)
                .Select(index => observedAngles[index] ?? index * Math.Tau / count)
                .ToArray();
        }

        return new OrbitData(skillsPerOrbit, orbitRadii, orbitAngles);
    }

    private static ClassCatalog BuildLegacyClassCatalog(IReadOnlyList<Poe2LegacyClassDto> classes)
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

    private static IReadOnlyList<int> ResolveLegacyClassStartIndexes(Poe2LegacyNodeDto nd, Dictionary<string, int> classIndexByName)
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

    private static IReadOnlyList<int> ResolveGggClassStartIndexes(int[]? indexes, IReadOnlyDictionary<int, int> sourceToCatalogIndex)
    {
        var result = new List<int>();
        foreach (var sourceIndex in indexes ?? [])
        {
            if (sourceToCatalogIndex.TryGetValue(sourceIndex, out var catalogIndex)
                && !result.Contains(catalogIndex))
            {
                result.Add(catalogIndex);
            }
        }

        return result;
    }

    private static NodeType ClassifyLegacyNode(Poe2LegacyNodeDto nd, IReadOnlyList<int> classStartIndexes)
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

        return nd.IsNotable ? NodeType.Notable : NodeType.Normal;
    }

    private static NodeType ClassifyGggNode(Poe2GggNodeDto nd, IReadOnlyList<int> classStartIndexes)
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

        if (IsGggMasteryMarker(nd))
        {
            return NodeType.Proxy;
        }

        if (nd.IsJewelSocket)
        {
            return NodeType.JewelSocket;
        }

        return nd.IsNotable ? NodeType.Notable : NodeType.Normal;
    }

    private static bool IsGggMasteryMarker(Poe2GggNodeDto nd) =>
        nd.Name?.EndsWith("Mastery", StringComparison.OrdinalIgnoreCase) == true
        && (nd.Stats is null || nd.Stats.Length == 0);

    private static bool TryBuildLegacyArc(
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
        var startAngle = TreeAngle(cx, cy, from.X, from.Y);
        var endAngle = TreeAngle(cx, cy, to.X, to.Y);
        var sweep = NormalizeSweep(endAngle - startAngle);

        connector = new ArcConnector(from.Id, to.Id, cx, cy, radius, startAngle, sweep);
        return true;
    }

    private static bool TryBuildGggArc((int, int) pair, Node from, Node to, Poe2GggEdgeDto edge, out ArcConnector connector)
    {
        connector = null!;
        if (edge.OrbitX is null || edge.OrbitY is null)
        {
            return false;
        }

        var radius = Radius(edge.OrbitX.Value, edge.OrbitY.Value, from.X, from.Y);
        if (radius <= 0)
        {
            return false;
        }

        var startAngle = TreeAngle(edge.OrbitX.Value, edge.OrbitY.Value, from.X, from.Y);
        var endAngle = TreeAngle(edge.OrbitX.Value, edge.OrbitY.Value, to.X, to.Y);
        var sweep = NormalizeSweep(endAngle - startAngle);
        if (Math.Abs(sweep) < 0.0001)
        {
            return false;
        }

        connector = new ArcConnector(pair.Item1, pair.Item2, edge.OrbitX.Value, edge.OrbitY.Value, radius, startAngle, sweep);
        return true;
    }

    private static bool NeedsDrawableConnector(Node from, Node to) =>
        from.Type is not NodeType.Proxy and not NodeType.Mastery
        && to.Type is not NodeType.Proxy and not NodeType.Mastery
        && !((from.Type == NodeType.ClassStart && to.Type == NodeType.AscendancyStart)
            || (from.Type == NodeType.AscendancyStart && to.Type == NodeType.ClassStart));

    private static void LinkNodes(Node from, Node to)
    {
        if (!from.LinkedNodes.Contains(to))
        {
            from.LinkedNodes.Add(to);
        }

        if (!to.LinkedNodes.Contains(from))
        {
            to.LinkedNodes.Add(from);
        }
    }

    private static bool TryReadNodeId(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(element.GetString(), out value),
            _ => false,
        };
    }

    private static (int, int) OrderedPair(int a, int b) =>
        a < b ? (a, b) : (b, a);

    private static double TreeAngle(double cx, double cy, double x, double y) =>
        Math.Atan2(x - cx, cy - y);

    private static double NormalizeSweep(double sweep)
    {
        while (sweep > Math.PI)
        {
            sweep -= Math.Tau;
        }

        while (sweep <= -Math.PI)
        {
            sweep += Math.Tau;
        }

        return sweep;
    }

    private static double Radius(double cx, double cy, double x, double y)
    {
        var dx = x - cx;
        var dy = y - cy;
        return Math.Sqrt(dx * dx + dy * dy);
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

    private sealed record OrbitInfo(int Group, int Orbit, double Cx, double Cy, double Angle, double Radius);
    private sealed record OrbitData(int[] SkillsPerOrbit, double[] OrbitRadii, IReadOnlyList<double>[] OrbitAngles);
}
