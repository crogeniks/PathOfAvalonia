using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathOfAvalonia.TreeDomain.Poe1;

public sealed class Poe1TreeLoader : ITreeLoader
{
    public TreeModel Load(Stream stream, string version, GameId gameId)
    {
        var dto = JsonSerializer.Deserialize<TreeDto>(stream, JsonOpts)
                  ?? throw new InvalidDataException("tree JSON was null");
        return Build(dto, version, gameId);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static TreeModel Build(TreeDto dto, string version, GameId gameId)
    {
        var orbitAngles = BuildOrbitAngles(dto.Constants.SkillsPerOrbit);
        var orbitRadii = dto.Constants.OrbitRadii;
        var groupPositions = new Dictionary<int, GroupPosition>(dto.Groups.Count);
        foreach (var (groupId, group) in dto.Groups)
        {
            if (int.TryParse(groupId, out var gid))
            {
                groupPositions[gid] = new GroupPosition(group.X, group.Y);
            }
        }

        // Per-node placement, plus side-table of orbit info needed for arc detection.
        var nodes = new Dictionary<int, Node>(dto.Nodes.Count);
        var clusterNodeTemplates = new Dictionary<string, Node>(StringComparer.Ordinal);
        var orbitInfo = new Dictionary<int, (int Group, int Orbit, double Cx, double Cy, double Angle, double Radius)>(dto.Nodes.Count);

        foreach (var (key, nd) in dto.Nodes)
        {
            if (!TryResolveNodeId(key, nd, out var id))
            {
                continue;
            }

            if (nd.Group is null || !dto.Groups.TryGetValue(nd.Group.Value.ToString(), out var grp))
            {
                var templateType = ClassifyNode(nd);
                if (nd.Name is not null && templateType is NodeType.Notable or NodeType.Keystone)
                {
                    clusterNodeTemplates[nd.Name] = new Node
                    {
                        Id = id,
                        Name = nd.Name,
                        Type = templateType,
                        X = 0,
                        Y = 0,
                        Icon = nd.Icon,
                        Stats = NormalizeLines(nd.Stats),
                        ReminderText = NormalizeLines(nd.ReminderText),
                        FlavourText = NormalizeLines(nd.FlavourText),
                        ActiveIcon = nd.ActiveIcon,
                        InactiveIcon = nd.InactiveIcon,
                        AscendancyName = nd.AscendancyName,
                        ClassStartIndex = nd.ClassStartIndex,
                        GroupId = 0,
                        Orbit = 0,
                        OrbitIndex = 0,
                        ExpansionSocket = null,
                        MasteryEffects = null,
                    };
                }
                continue;
            }
            if (nd.Orbit is null || nd.OrbitIndex is null)
            {
                continue;
            }
            var orbit = nd.Orbit.Value;
            if (orbit < 0 || orbit >= orbitRadii.Length)
            {
                continue;
            }
            if (nd.OrbitIndex.Value < 0 || nd.OrbitIndex.Value >= orbitAngles[orbit].Length)
            {
                continue;
            }

            var angle = orbitAngles[orbit][nd.OrbitIndex.Value];
            var radius = orbitRadii[orbit];
            var x = grp.X + Math.Sin(angle) * radius;
            var y = grp.Y - Math.Cos(angle) * radius;

            var type = ClassifyNode(nd);
            IReadOnlyList<MasteryEffect>? effects = null;
            if (nd.MasteryEffects is { Length: > 0 } meArr)
            {
                var list = new List<MasteryEffect>(meArr.Length);
                foreach (var me in meArr)
                {
                    list.Add(new MasteryEffect(me.Effect, me.Stats ?? Array.Empty<string>()));
                }
                effects = list;
            }
            var expansionSocket = ParseExpansionSocket(nd);
            nodes[id] = new Node
            {
                Id = id,
                Name = nd.Name ?? $"Node {id}",
                Type = type,
                X = x,
                Y = y,
                Icon = nd.Icon,
                Stats = NormalizeLines(nd.Stats),
                ReminderText = NormalizeLines(nd.ReminderText),
                FlavourText = NormalizeLines(nd.FlavourText),
                ActiveIcon = nd.ActiveIcon,
                InactiveIcon = nd.InactiveIcon,
                AscendancyName = nd.AscendancyName,
                ClassStartIndex = nd.ClassStartIndex,
                GroupId = nd.Group.Value,
                Orbit = orbit,
                OrbitIndex = nd.OrbitIndex.Value,
                ExpansionSocket = expansionSocket,
                MasteryEffects = effects,
            };
            orbitInfo[id] = (nd.Group.Value, orbit, grp.X, grp.Y, angle, radius);
        }

        // Wire links (union of in + out), skip pairs that don't both resolve.
        var connectorSet = new HashSet<(int, int)>();
        var connectors = new List<Connector>();
        foreach (var (key, nd) in dto.Nodes)
        {
            if (!TryResolveNodeId(key, nd, out var id) || !nodes.TryGetValue(id, out var me))
            {
                continue;
            }
            if (me.Type == NodeType.Proxy)
            {
                continue;
            }
            foreach (var neighId in Enumerable.Concat(nd.Out ?? Array.Empty<int>(), nd.In ?? Array.Empty<int>()))
            {
                if (!nodes.TryGetValue(neighId, out var other))
                {
                    continue;
                }
                if (ReferenceEquals(me, other))
                {
                    continue;
                }
                if (other.Type == NodeType.Proxy)
                {
                    continue;
                }
                // Skip edges that cross ascendancy boundaries (build.md §3.4).
                if (me.AscendancyName != other.AscendancyName)
                {
                    continue;
                }

                me.LinkedNodes.Add(other);

                // Mastery nodes have edges in the data but render standalone in PoB.
                if (me.Type == NodeType.Mastery || other.Type == NodeType.Mastery)
                {
                    continue;
                }

                var a = Math.Min(id, neighId);
                var b = Math.Max(id, neighId);
                if (!connectorSet.Add((a, b)))
                {
                    continue;
                }

                var na = nodes[a];
                var nb = nodes[b];
                var ia = orbitInfo[a];
                var ib = orbitInfo[b];

                if (ia.Group == ib.Group && ia.Orbit == ib.Orbit && ia.Radius > 0)
                {
                    // Pick the shorter arc — sweep ∈ (-π, π].
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
                    connectors.Add(new LineConnector(a, b, na.X, na.Y, nb.X, nb.Y));
                }
            }
        }

        return new TreeModel
        {
            GameId = gameId,
            Version = version,
            Classes = ClassCatalog.CreatePoe1(),
            Nodes = nodes,
            ClusterNodeTemplates = clusterNodeTemplates,
            Connectors = connectors,
            Bounds = new TreeBounds(dto.MinX, dto.MinY, dto.MaxX, dto.MaxY),
            Groups = groupPositions,
            SkillsPerOrbit = dto.Constants.SkillsPerOrbit,
            OrbitRadii = dto.Constants.OrbitRadii,
            OrbitAngles = orbitAngles,
        };
    }

    private static ExpansionSocketInfo? ParseExpansionSocket(NodeDto nd)
    {
        if (nd.ExpansionJewel is null)
        {
            return null;
        }
        return new ExpansionSocketInfo(
            nd.ExpansionJewel.Size,
            nd.ExpansionJewel.Index,
            int.Parse(nd.ExpansionJewel.Proxy),
            string.IsNullOrEmpty(nd.ExpansionJewel.Parent) ? null : int.Parse(nd.ExpansionJewel.Parent));
    }

    private static bool TryResolveNodeId(string key, NodeDto nd, out int id)
    {
        if (nd.Id > 0)
        {
            id = nd.Id;
            return true;
        }
        if (nd.Skill is { } skill)
        {
            id = skill;
            return true;
        }
        return int.TryParse(key, out id);
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

    private static NodeType ClassifyNode(NodeDto nd)
    {
        if (nd.ClassStartIndex.HasValue)
        {
            return NodeType.ClassStart;
        }
        if (nd.IsAscendancyStart)
        {
            return NodeType.AscendancyStart;
        }
        if (nd.IsProxy)
        {
            return NodeType.Proxy;
        }
        if (nd.IsKeystone)
        {
            return NodeType.Keystone;
        }
        if (nd.IsMastery)
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

    private static double[][] BuildOrbitAngles(int[] skillsPerOrbit)
    {
        var result = new double[skillsPerOrbit.Length][];
        for (var o = 0; o < skillsPerOrbit.Length; o++)
        {
            var n = skillsPerOrbit[o];
            // Modern schema uses non-uniform patterns for some orbits; for a render-only
            // PoC, uniform angles are visually indistinguishable on most monitors.
            // Known non-uniform exceptions can be added here later.
            var a = new double[Math.Max(n, 1)];
            if (n <= 0)
            {
                result[o] = a;
                continue;
            }
            if (n == 16)
            {
                // Orbits 2 & 3 on the 3.28 tree: 16 skills, non-uniform (30°/45° pattern).
                var deg = new[] { 0, 30, 45, 60, 90, 120, 135, 150, 180, 210, 225, 240, 270, 300, 315, 330 };
                for (var i = 0; i < 16; i++)
                {
                    a[i] = deg[i] * Math.PI / 180.0;
                }
            }
            else if (n == 40)
            {
                var deg = new[] {
                    0, 10, 20, 30, 40, 45, 50, 60, 70, 80,
                    90, 100, 110, 120, 130, 135, 140, 150, 160, 170,
                    180, 190, 200, 210, 220, 225, 230, 240, 250, 260,
                    270, 280, 290, 300, 310, 315, 320, 330, 340, 350,
                };
                for (var i = 0; i < 40; i++)
                {
                    a[i] = deg[i] * Math.PI / 180.0;
                }
            }
            else
            {
                var step = Math.Tau / n;
                for (var i = 0; i < n; i++)
                {
                    a[i] = i * step;
                }
            }
            result[o] = a;
        }
        return result;
    }

    // --- DTOs ---

    private sealed class TreeDto
    {
        [JsonPropertyName("min_x")] public double MinX { get; set; }
        [JsonPropertyName("min_y")] public double MinY { get; set; }
        [JsonPropertyName("max_x")] public double MaxX { get; set; }
        [JsonPropertyName("max_y")] public double MaxY { get; set; }
        [JsonPropertyName("constants")] public ConstantsDto Constants { get; set; } = new();
        [JsonPropertyName("groups")] public Dictionary<string, GroupDto> Groups { get; set; } = new();
        [JsonPropertyName("nodes")] public Dictionary<string, NodeDto> Nodes { get; set; } = new();
    }

    private sealed class ConstantsDto
    {
        [JsonPropertyName("skillsPerOrbit")] public int[] SkillsPerOrbit { get; set; } = Array.Empty<int>();
        [JsonPropertyName("orbitRadii")] public double[] OrbitRadii { get; set; } = Array.Empty<double>();
    }

    private sealed class GroupDto
    {
        [JsonPropertyName("x")] public double X { get; set; }
        [JsonPropertyName("y")] public double Y { get; set; }
    }

    private sealed class NodeDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("skill")] public int? Skill { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("icon")] public string? Icon { get; set; }
        [JsonPropertyName("activeIcon")] public string? ActiveIcon { get; set; }
        [JsonPropertyName("inactiveIcon")] public string? InactiveIcon { get; set; }
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
        [JsonPropertyName("isJewelSocket")] public bool IsJewelSocket { get; set; }
        [JsonPropertyName("isProxy")] public bool IsProxy { get; set; }
        [JsonPropertyName("isAscendancyStart")] public bool IsAscendancyStart { get; set; }
        [JsonPropertyName("ascendancyName")] public string? AscendancyName { get; set; }
        [JsonPropertyName("classStartIndex")] public int? ClassStartIndex { get; set; }
        [JsonPropertyName("masteryEffects")] public MasteryEffectDto[]? MasteryEffects { get; set; }
        [JsonPropertyName("expansionJewel")] public ExpansionJewelDto? ExpansionJewel { get; set; }
    }

    private sealed class MasteryEffectDto
    {
        [JsonPropertyName("effect")] public int Effect { get; set; }
        [JsonPropertyName("stats")] public string[]? Stats { get; set; }
    }

    private sealed class ExpansionJewelDto
    {
        [JsonPropertyName("size")] public int Size { get; set; }
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("proxy")] public string Proxy { get; set; } = string.Empty;
        [JsonPropertyName("parent")] public string? Parent { get; set; }
    }
}
