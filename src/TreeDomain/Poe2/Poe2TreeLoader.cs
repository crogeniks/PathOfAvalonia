using System.Text;
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
        var dto = JsonSerializer.Deserialize<Poe2GggTreeDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("tree JSON was null");
        return Build(dto, version, gameId);
    }

    private static TreeModel Build(Poe2GggTreeDto dto, string version, GameId gameId)
    {
        var classInfo = BuildClassCatalog(dto.Classes);
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

            var stats = NormalizeDescriptionLines(nd.Stats);
            nodes[id] = new Node
            {
                Id = id,
                BuildPlannerId = string.IsNullOrWhiteSpace(nd.Id) ? null : nd.Id,
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
                Stats = stats.Lines,
                StatLinkSpans = stats.LinkSpans,
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

            if (TryBuildArc(pair, from, to, edge, out var arc))
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

    private static (ClassCatalog Catalog, Dictionary<int, int> SourceToCatalogIndex) BuildClassCatalog(IReadOnlyList<Poe2GggClassDto> classes)
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

    private static bool TryBuildArc((int, int) pair, Node from, Node to, Poe2GggEdgeDto edge, out ArcConnector connector)
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

    private static NormalizedDescriptionLines NormalizeDescriptionLines(string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return new NormalizedDescriptionLines(Array.Empty<string>(), Array.Empty<IReadOnlyList<TextSpan>>());
        }

        var lines = new List<string>();
        var linkSpans = new List<IReadOnlyList<TextSpan>>();
        foreach (var value in values)
        {
            foreach (var line in value.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var parsed = PlainDescriptionText(trimmed);
                lines.Add(parsed.Text);
                linkSpans.Add(parsed.LinkSpans);
            }
        }

        return new NormalizedDescriptionLines(lines, linkSpans);
    }

    private static ParsedDescriptionText PlainDescriptionText(string value)
    {
        var openIndex = value.IndexOf('[');
        if (openIndex < 0)
        {
            return new ParsedDescriptionText(value, Array.Empty<TextSpan>());
        }

        var result = new StringBuilder(value.Length);
        var linkSpans = new List<TextSpan>();
        var index = 0;
        while (index < value.Length)
        {
            openIndex = value.IndexOf('[', index);
            if (openIndex < 0)
            {
                result.Append(value, index, value.Length - index);
                break;
            }

            var closeIndex = value.IndexOf(']', openIndex + 1);
            if (closeIndex < 0)
            {
                result.Append(value, index, value.Length - index);
                break;
            }

            result.Append(value, index, openIndex - index);
            var pipeIndex = value.IndexOf('|', openIndex + 1, closeIndex - openIndex - 1);
            var displayStart = pipeIndex >= 0 ? pipeIndex + 1 : openIndex + 1;
            var resultStart = result.Length;
            var displayLength = closeIndex - displayStart;
            result.Append(value, displayStart, displayLength);
            if (displayLength > 0)
            {
                linkSpans.Add(new TextSpan(resultStart, displayLength));
            }
            index = closeIndex + 1;
        }

        return new ParsedDescriptionText(result.ToString(), linkSpans);
    }

    private sealed record OrbitData(int[] SkillsPerOrbit, double[] OrbitRadii, IReadOnlyList<double>[] OrbitAngles);
    private sealed record ParsedDescriptionText(string Text, IReadOnlyList<TextSpan> LinkSpans);
    private sealed record NormalizedDescriptionLines(IReadOnlyList<string> Lines, IReadOnlyList<IReadOnlyList<TextSpan>> LinkSpans);
}
