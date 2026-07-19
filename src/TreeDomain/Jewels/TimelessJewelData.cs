using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed class TimelessJewelData
{
    private readonly int _additionOffset;
    private readonly IReadOnlyList<TimelessNodeTemplate> _additions;
    private readonly IReadOnlyList<TimelessNodeTemplate> _replacements;
    private readonly IReadOnlyDictionary<string, TimelessNodeTemplate> _replacementsById;
    private readonly int _nodeCount;
    private readonly int _notableNodeCount;
    private readonly IReadOnlyDictionary<int, NodeIndexEntry> _nodeIndices;
    private readonly IReadOnlyDictionary<TimelessJewelType, IReadOnlyDictionary<byte, int>> _localIds;
    private readonly IReadOnlyDictionary<TimelessJewelType, byte[]> _lookups;
    private readonly int[] _gloriousNodeOffsets;

    public static TimelessJewelData Empty { get; } = new(
        96,
        [],
        [],
        0,
        0,
        new Dictionary<int, NodeIndexEntry>(),
        new Dictionary<TimelessJewelType, IReadOnlyDictionary<byte, int>>(),
        new Dictionary<TimelessJewelType, byte[]>());

    public bool IsEmpty => _lookups.Count == 0;

    private TimelessJewelData(
        int additionOffset,
        IReadOnlyList<TimelessNodeTemplate> additions,
        IReadOnlyList<TimelessNodeTemplate> replacements,
        int nodeCount,
        int notableNodeCount,
        IReadOnlyDictionary<int, NodeIndexEntry> nodeIndices,
        IReadOnlyDictionary<TimelessJewelType, IReadOnlyDictionary<byte, int>> localIds,
        IReadOnlyDictionary<TimelessJewelType, byte[]> lookups)
    {
        _additionOffset = additionOffset;
        _additions = additions;
        _replacements = replacements;
        _replacementsById = replacements.ToDictionary(template => template.Id, StringComparer.Ordinal);
        _nodeCount = nodeCount;
        _notableNodeCount = notableNodeCount;
        _nodeIndices = nodeIndices;
        _localIds = localIds;
        _lookups = lookups;
        _gloriousNodeOffsets = BuildGloriousNodeOffsets();
    }

    public static TimelessJewelData Load(
        Stream definitionsStream,
        Stream mappingStream,
        IReadOnlyDictionary<TimelessJewelType, Stream> compressedLookups)
    {
        var definitions = JsonSerializer.Deserialize<DefinitionsDto>(definitionsStream, JsonOptions)
            ?? throw new InvalidDataException("Timeless jewel definitions JSON was null.");
        var mapping = JsonSerializer.Deserialize<MappingDto>(mappingStream, JsonOptions)
            ?? throw new InvalidDataException("Timeless jewel node mapping JSON was null.");

        var nodeIndices = mapping.Nodes.ToDictionary(
            pair => int.Parse(pair.Key, CultureInfo.InvariantCulture),
            pair => new NodeIndexEntry(pair.Value.Index, pair.Value.Size));
        var localIds = mapping.LocalIds.ToDictionary(
            pair => (TimelessJewelType)int.Parse(pair.Key, CultureInfo.InvariantCulture),
            pair => (IReadOnlyDictionary<byte, int>)pair.Value.ToDictionary(
                value => byte.Parse(value.Key, CultureInfo.InvariantCulture),
                value => value.Value));
        var lookups = new Dictionary<TimelessJewelType, byte[]>();
        foreach (var (type, compressedStream) in compressedLookups)
        {
            using var inflater = new ZLibStream(compressedStream, CompressionMode.Decompress, leaveOpen: true);
            using var uncompressed = new MemoryStream();
            inflater.CopyTo(uncompressed);
            lookups[type] = uncompressed.ToArray();
        }

        return new TimelessJewelData(
            definitions.AdditionOffset,
            definitions.Additions.Select(ConvertTemplate).ToArray(),
            definitions.Replacements.Select(ConvertTemplate).ToArray(),
            mapping.Size,
            mapping.SizeNotable,
            nodeIndices,
            localIds,
            lookups);
    }

    public IReadOnlyDictionary<int, TimelessNodeEffect> Resolve(
        TimelessJewelSpec jewel,
        IEnumerable<Node> nodes)
    {
        var result = new Dictionary<int, TimelessNodeEffect>();
        foreach (var node in nodes)
        {
            if (TryResolve(jewel, node, out var effect))
            {
                result[node.Id] = effect;
            }
        }
        return result;
    }

    private bool TryResolve(TimelessJewelSpec jewel, Node node, out TimelessNodeEffect effect)
    {
        effect = null!;
        if (node.Type == NodeType.Keystone)
        {
            return TryReplace(KeystoneId(jewel), null, out effect);
        }
        if (node.Type == NodeType.Notable)
        {
            if (!TryReadOperations(jewel, node.Id, out var operations))
            {
                return false;
            }
            return jewel.Type == TimelessJewelType.GloriousVanity
                ? TryApplyGloriousVanity(node, operations, out effect)
                : TryApplyOperation(node, operations[0], out effect);
        }
        if (node.Type != NodeType.Normal)
        {
            return false;
        }

        var isAttribute = node.Name is "Strength" or "Dexterity" or "Intelligence";
        switch (jewel.Conqueror)
        {
            case TimelessConqueror.Vaal:
                return TryReadOperations(jewel, node.Id, out var operations)
                    && TryApplyGloriousVanity(node, operations, out effect);
            case TimelessConqueror.Karui:
                effect = AddLine(node, $"+{(isAttribute ? 2 : 4)} to Strength");
                return true;
            case TimelessConqueror.Maraketh:
                effect = AddLine(node, $"+{(isAttribute ? 2 : 4)} to Dexterity");
                return true;
            case TimelessConqueror.Templar when isAttribute:
                return TryReplace("templar_devotion_node", null, out effect);
            case TimelessConqueror.Templar:
                effect = AddLine(node, "+5 to Devotion");
                return true;
            case TimelessConqueror.EternalEmpire:
                return TryReplace("eternal_small_blank", null, out effect);
            case TimelessConqueror.Kalguuran:
                effect = AddLine(node, $"{(isAttribute ? 1 : 2)}% increased Ward");
                return true;
            default:
                return false;
        }
    }

    private bool TryApplyOperation(Node node, int operationId, out TimelessNodeEffect effect)
    {
        if (operationId >= _additionOffset)
        {
            return TryReplace(operationId, null, out effect);
        }
        if (operationId < 0 || operationId >= _additions.Count)
        {
            effect = null!;
            return false;
        }

        effect = AddTemplate(node, _additions[operationId], null);
        return true;
    }

    private bool TryApplyGloriousVanity(Node node, IReadOnlyList<int> operations, out TimelessNodeEffect effect)
    {
        effect = null!;
        if (operations.Count is 2 or 3)
        {
            return TryReplace(operations[0], operations, out effect);
        }
        if (operations.Count is not 6 and not 8)
        {
            return false;
        }

        var additionCount = operations.Count / 2;
        var bias = operations.Take(additionCount).Sum(id => id <= 21 ? 1 : -1);
        var baseId = bias >= 0 ? "vaal_notable_random_offense" : "vaal_notable_random_defence";
        if (!_replacementsById.TryGetValue(baseId, out var baseTemplate))
        {
            return false;
        }

        var stats = new List<string>(baseTemplate.Stats);
        var orderedIds = new List<int>();
        var rollsById = new Dictionary<int, int>();
        for (var index = 0; index < additionCount; index++)
        {
            var id = operations[index];
            if (!rollsById.TryAdd(id, operations[index + additionCount]))
            {
                rollsById[id] += operations[index + additionCount];
            }
            else
            {
                orderedIds.Add(id);
            }
        }
        foreach (var id in orderedIds)
        {
            if (id < 0 || id >= _additions.Count)
            {
                continue;
            }
            stats.AddRange(ApplySingleRoll(_additions[id], rollsById[id]));
        }

        effect = new TimelessNodeEffect(baseTemplate.Name, baseTemplate.Icon, stats, ReplacesNode: true);
        return true;
    }

    private bool TryReplace(int operationId, IReadOnlyList<int>? rolls, out TimelessNodeEffect effect)
    {
        var replacementIndex = operationId - _additionOffset;
        if (replacementIndex < 0 || replacementIndex >= _replacements.Count)
        {
            effect = null!;
            return false;
        }
        return TryReplace(_replacements[replacementIndex].Id, rolls, out effect);
    }

    private bool TryReplace(string replacementId, IReadOnlyList<int>? rolls, out TimelessNodeEffect effect)
    {
        if (!_replacementsById.TryGetValue(replacementId, out var template))
        {
            effect = null!;
            return false;
        }
        effect = new TimelessNodeEffect(
            template.Name,
            template.Icon,
            ApplyIndexedRolls(template, rolls),
            ReplacesNode: true);
        return true;
    }

    private bool TryReadOperations(TimelessJewelSpec jewel, int nodeId, out IReadOnlyList<int> operations)
    {
        operations = [];
        if (!TryGetLookupSeed(jewel, out var lookupSeed)
            || !_lookups.TryGetValue(jewel.Type, out var lookup)
            || !_nodeIndices.TryGetValue(nodeId, out var nodeIndex))
        {
            return false;
        }

        var minSeed = MinSeed(jewel.Type);
        var seedSize = MaxSeed(jewel.Type) - minSeed + 1;
        var seedOffset = lookupSeed - minSeed;
        if (jewel.Type != TimelessJewelType.GloriousVanity)
        {
            if (nodeIndex.Index < 0 || nodeIndex.Index >= _notableNodeCount)
            {
                return false;
            }
            var offset = nodeIndex.Index * seedSize + seedOffset;
            if (offset < 0 || offset >= lookup.Length)
            {
                return false;
            }
            operations = [ConvertLocalId(jewel.Type, lookup[offset])];
            return true;
        }

        var sizesOffset = nodeIndex.Index * seedSize;
        var dataOffset = _gloriousNodeOffsets[nodeIndex.Index];
        if (sizesOffset < 0 || sizesOffset + seedOffset >= lookup.Length || dataOffset < 0)
        {
            return false;
        }
        for (var index = 0; index < seedOffset; index++)
        {
            dataOffset += lookup[sizesOffset + index];
        }
        var length = lookup[sizesOffset + seedOffset];
        if (length == 0 || dataOffset + length > lookup.Length)
        {
            return false;
        }

        var decoded = new int[length];
        for (var index = 0; index < length; index++)
        {
            decoded[index] = lookup[dataOffset + index];
        }
        if (length is 2 or 3)
        {
            decoded[0] = ConvertLocalId(jewel.Type, (byte)decoded[0]);
        }
        else if (length is 6 or 8)
        {
            for (var index = 0; index < length / 2; index++)
            {
                decoded[index] = ConvertLocalId(jewel.Type, (byte)decoded[index]);
            }
        }
        operations = decoded;
        return true;
    }

    private int[] BuildGloriousNodeOffsets()
    {
        if (!_lookups.ContainsKey(TimelessJewelType.GloriousVanity) || _nodeCount == 0)
        {
            return [];
        }

        var offsets = Enumerable.Repeat(-1, _nodeCount).ToArray();
        var offset = _nodeCount * (MaxSeed(TimelessJewelType.GloriousVanity) - MinSeed(TimelessJewelType.GloriousVanity) + 1);
        foreach (var entry in _nodeIndices.Values.OrderBy(entry => entry.Index))
        {
            offsets[entry.Index] = offset;
            offset += entry.Size;
        }
        return offsets;
    }

    private int ConvertLocalId(TimelessJewelType type, byte localId) =>
        _localIds.TryGetValue(type, out var mapping) && mapping.TryGetValue(localId, out var globalId)
            ? globalId
            : localId;

    private static bool TryGetLookupSeed(TimelessJewelSpec jewel, out int lookupSeed)
    {
        lookupSeed = jewel.Seed;
        if (jewel.Type == TimelessJewelType.ElegantHubris)
        {
            if (lookupSeed % 20 != 0)
            {
                return false;
            }
            lookupSeed /= 20;
        }
        return lookupSeed >= MinSeed(jewel.Type) && lookupSeed <= MaxSeed(jewel.Type);
    }

    private static int MinSeed(TimelessJewelType type) => type switch
    {
        TimelessJewelType.GloriousVanity => 100,
        TimelessJewelType.LethalPride => 10000,
        TimelessJewelType.BrutalRestraint => 500,
        TimelessJewelType.MilitantFaith => 2000,
        TimelessJewelType.ElegantHubris => 100,
        TimelessJewelType.HeroicTragedy => 100,
        _ => 0,
    };

    private static int MaxSeed(TimelessJewelType type) => type switch
    {
        TimelessJewelType.GloriousVanity => 8000,
        TimelessJewelType.LethalPride => 18000,
        TimelessJewelType.BrutalRestraint => 8000,
        TimelessJewelType.MilitantFaith => 10000,
        TimelessJewelType.ElegantHubris => 8000,
        TimelessJewelType.HeroicTragedy => 8000,
        _ => -1,
    };

    private static string KeystoneId(TimelessJewelSpec jewel) =>
        $"{ConquerorKey(jewel.Conqueror)}_keystone_{jewel.ConquerorId}";

    private static string ConquerorKey(TimelessConqueror conqueror) => conqueror switch
    {
        TimelessConqueror.EternalEmpire => "eternal",
        TimelessConqueror.Karui => "karui",
        TimelessConqueror.Maraketh => "maraketh",
        TimelessConqueror.Templar => "templar",
        TimelessConqueror.Vaal => "vaal",
        TimelessConqueror.Kalguuran => "kalguur",
        _ => string.Empty,
    };

    private static TimelessNodeEffect AddLine(Node node, string line) => new(
        node.Name,
        node.Icon,
        node.Stats.Concat([line]).ToArray(),
        ReplacesNode: false);

    private static TimelessNodeEffect AddTemplate(Node node, TimelessNodeTemplate template, int? roll) => new(
        node.Name,
        node.Icon,
        node.Stats.Concat(roll is { } value ? ApplySingleRoll(template, value) : template.Stats).ToArray(),
        ReplacesNode: false);

    private static IReadOnlyList<string> ApplyIndexedRolls(TimelessNodeTemplate template, IReadOnlyList<int>? values)
    {
        if (values is null || template.Rolls.Count == 0)
        {
            return template.Stats;
        }

        var result = new string[template.Stats.Count];
        for (var index = 0; index < template.Stats.Count; index++)
        {
            var line = template.Stats[index];
            if (index < template.Rolls.Count)
            {
                var roll = template.Rolls[index];
                if (roll.Index >= 0 && roll.Index < values.Count)
                {
                    line = ReplaceRoll(line, roll, values[roll.Index]);
                }
            }
            result[index] = line;
        }
        return result;
    }

    private static IReadOnlyList<string> ApplySingleRoll(TimelessNodeTemplate template, int value)
    {
        var result = new string[template.Stats.Count];
        for (var index = 0; index < template.Stats.Count; index++)
        {
            var line = template.Stats[index];
            foreach (var roll in template.Rolls)
            {
                line = ReplaceRoll(line, roll, value);
            }
            result[index] = line;
        }
        return result;
    }

    private static string ReplaceRoll(string stat, TimelessStatRoll roll, int rawValue)
    {
        var value = (double)rawValue;
        if (roll.Format == "g")
        {
            if (roll.StatKey.Contains("per_minute", StringComparison.Ordinal))
            {
                value = Math.Round(value / 60, 1);
            }
            else if (roll.StatKey.Contains("permyriad", StringComparison.Ordinal))
            {
                value /= 100;
            }
            else if (roll.StatKey.Contains("_ms", StringComparison.Ordinal))
            {
                value /= 1000;
            }
        }

        var replacement = FormatNumber(value);
        if (roll.Min != roll.Max)
        {
            return stat.Replace(
                $"({FormatNumber(roll.Min)}-{FormatNumber(roll.Max)})",
                replacement,
                StringComparison.Ordinal);
        }
        if (roll.Min != value)
        {
            return stat.Replace(FormatNumber(roll.Min), replacement, StringComparison.Ordinal);
        }
        return stat;
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.################", CultureInfo.InvariantCulture);

    private static TimelessNodeTemplate ConvertTemplate(NodeTemplateDto template) => new(
        template.Id,
        template.Name,
        template.Icon,
        template.Stats,
        template.Rolls.Select(roll => new TimelessStatRoll(
            roll.Key,
            roll.Format,
            roll.Index,
            roll.Min,
            roll.Max)).ToArray());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record NodeIndexEntry(int Index, int Size);
    private sealed record TimelessNodeTemplate(
        string Id,
        string Name,
        string? Icon,
        IReadOnlyList<string> Stats,
        IReadOnlyList<TimelessStatRoll> Rolls);
    private sealed record TimelessStatRoll(string StatKey, string Format, int Index, double Min, double Max);

    private sealed class DefinitionsDto
    {
        public int AdditionOffset { get; set; }
        public NodeTemplateDto[] Additions { get; set; } = [];
        public NodeTemplateDto[] Replacements { get; set; } = [];
    }

    private sealed class NodeTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string[] Stats { get; set; } = [];
        public StatRollDto[] Rolls { get; set; } = [];
    }

    private sealed class StatRollDto
    {
        public string Key { get; set; } = string.Empty;
        [JsonPropertyName("fmt")] public string Format { get; set; } = string.Empty;
        public int Index { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }

    private sealed class MappingDto
    {
        public int Size { get; set; }
        public int SizeNotable { get; set; }
        public Dictionary<string, NodeIndexDto> Nodes { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> LocalIds { get; set; } = new();
    }

    private sealed class NodeIndexDto
    {
        public int Index { get; set; }
        public int Size { get; set; }
    }
}
