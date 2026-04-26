using System.Buffers.Binary;

namespace PathOfAvalonia.TreeDomain.Import;

// Decodes a GGG passive-skill-tree URL (the format PoB's PassiveSpec:DecodeURL consumes).
// Byte layout (version 6):
//   4 bytes   version (big-endian u32)
//   1 byte    classId
//   1 byte    ascendancy bits — low 2 = ascendClassId, next 2 = secondaryAscendClassId
//   1 byte    nodeCount
//   2N bytes  node IDs (big-endian u16) × nodeCount
//   1 byte    clusterCount
//   2N bytes  cluster IDs (big-endian u16, add 65536 to get real id)
//   1 byte    masteryCount
//   4N bytes  mastery effects — {effectId u16, nodeId u16}
// Versions 4–6 are supported; earlier versions predate the modern schema we target.
public static class PobTreeUrlDecoder
{
    public static ImportedBuild Decode(string url)
    {
        var tail = StripUrlPrefix(url);
        var bytes = Base64UrlDecode(tail);
        if (bytes.Length < 6)
        {
            throw new InvalidDataException("Tree URL payload is too short");
        }
        var version = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if (version < 4 || version > 6)
        {
            throw new InvalidDataException($"Unsupported tree URL version '{version}' (expected 4–6)");
        }
        var classId = bytes[4];
        var ascendBits = bytes[5];
        var ascendClassId = ascendBits & 0x3;
        var secondaryAscendClassId = (ascendBits >> 2) & 0x3;

        // v4 packs nodes directly from byte 7 to the end of the buffer with no count;
        // v5+ prefixes the node list with a u8 count at byte 6 (matches PoB's 1-indexed
        // b:byte(7) where nodesStart = b:byte(8)).
        int nodesStart;
        int nodesEnd;
        if (version >= 5)
        {
            var nodeCount = bytes[6];
            nodesStart = 7;
            nodesEnd = nodesStart + (nodeCount * 2);
        }
        else
        {
            // v4 layout: header ends at byte 7; nodes consume the rest, aligned to u16 pairs.
            nodesStart = 7;
            var payloadLen = bytes.Length - nodesStart;
            nodesEnd = nodesStart + (payloadLen - (payloadLen % 2));
        }
        if (bytes.Length < nodesEnd)
        {
            throw new InvalidDataException("Tree URL is truncated inside node list");
        }
        var nodes = ReadIds(bytes.AsSpan(nodesStart, nodesEnd - nodesStart), offset: 0);

        var clusters = Array.Empty<int>();
        var masteries = new Dictionary<int, int>();

        if (version >= 5 && bytes.Length > nodesEnd)
        {
            var clusterCount = bytes[nodesEnd];
            var clusterStart = nodesEnd + 1;
            var clusterEnd = clusterStart + (clusterCount * 2);
            if (bytes.Length < clusterEnd)
            {
                throw new InvalidDataException("Tree URL is truncated inside cluster list");
            }
            clusters = ReadIds(bytes.AsSpan(clusterStart, clusterCount * 2), offset: 65536);

            if (version >= 6 && bytes.Length > clusterEnd)
            {
                var masteryCount = bytes[clusterEnd];
                var masteryStart = clusterEnd + 1;
                var masteryEnd = masteryStart + (masteryCount * 4);
                if (bytes.Length < masteryEnd)
                {
                    throw new InvalidDataException("Tree URL is truncated inside mastery list");
                }
                for (var i = 0; i < masteryCount; i++)
                {
                    var p = masteryStart + (i * 4);
                    var effectId = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p, 2));
                    var nodeId = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p + 2, 2));
                    masteries[nodeId] = effectId;
                }
            }
        }

        return new ImportedBuild(
            ClassId: classId,
            AscendClassId: ascendClassId,
            SecondaryAscendClassId: secondaryAscendClassId,
            NodeHashes: nodes,
            ClusterNodeHashes: clusters,
            MasterySelections: masteries,
            TreeVersion: null,
            Source: "tree-url");
    }

    public static bool LooksLikeTreeUrl(string text)
    {
        var t = text.Trim();
        return t.Contains("pathofexile.com/passive-skill-tree", StringComparison.OrdinalIgnoreCase)
               || t.Contains("poeplanner.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripUrlPrefix(string url)
    {
        var trimmed = url.Trim();
        var slash = trimmed.LastIndexOf('/');
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }

    private static int[] ReadIds(ReadOnlySpan<byte> payload, int offset)
    {
        var count = payload.Length / 2;
        var ids = new int[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(i * 2, 2)) + offset;
        }
        return ids;
    }

    internal static byte[] Base64UrlDecode(string s)
    {
        var standard = s.Replace('-', '+').Replace('_', '/');
        var pad = (4 - standard.Length % 4) % 4;
        if (pad > 0)
        {
            standard += new string('=', pad);
        }
        return Convert.FromBase64String(standard);
    }
}
