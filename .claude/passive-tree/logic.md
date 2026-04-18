# Passive Tree — Logic

## Allocation (PassiveSpec.lua)

- `allocNodes[nodeId]` — set of currently allocated nodes.
- `AllocNode(node, altPath)` — allocates target node + all required path nodes. Handles Intuitive Leap (bypasses pathing). Supports an explicit `altPath`.
- `DeallocNode(node)` — cascading: removes node + all dependents.
- `BuildAllDependsAndPaths()` — rebuilds dependency/path graph after any change.

## Path-Finding (BFS)

`BuildPathFromNode(root)` (lines 889–928): BFS fills `node.path` array (ordered shortest path).

- Root `pathDist = 0`; others ∞.
- Queue processes nodes in distance order (all 1-hop before 2-hop, etc.).
- Edge cost `1` for unallocated, `0` for already-allocated (free traversal).
- Constraints: cannot cross ascendancy boundaries except from Ascendant; cannot traverse through ClassStart/AscendClassStart nodes (except as root); no traversal away from Mastery nodes.

`GetShortestPathToClassStart(rootId)` (975–1016) — parent-tracking BFS with backtracking (used for Split Personality distance).

## Class / Ascendancy

- `SelectClass(classId)` — deallocate old start, allocate new start, preserve connected.
- `SelectAscendClass(ascendClassId)` — swap ascendancy; ascendancy-only nodes auto-deallocate.
- `SelectSecondaryAscendClass()` — alternate ascendancies (newer mechanic).

## Masteries

- `masterySelections[nodeId] = effectId`.
- `AddMasteryEffectOptionsToNode(node)` — populate `node.sd` with all available mastery effects.
- Mastery node click → UI modal for effect selection; persisted in XML encode/decode.

## Cluster Jewel Subgraphs

- `subGraphs` — dynamically generated node graphs from equipped cluster jewels.
- `allocSubgraphNodes`, `allocExtendedNodes` — allocations inside clusters.
- `BuildSubgraph()` — recursively builds cluster node trees with parent-socket links.
- Cluster node IDs ≥ 65536.

## URL Encode / Decode

`EncodeURL(prefix)` — base64 (URL-safe: `+` → `-`, `/` → `_`). Version 6 layout:

```
4 bytes  version
1 byte   classId
1 byte   ascendancy bits
1 byte   nodeCount
2 bytes × nodeCount      (main node IDs)
1 byte   clusterCount
2 bytes × clusterCount   (cluster IDs, offset by 65536)
1 byte   masteryCount
4 bytes × masteryCount   ({effectId, nodeId})
```

`DecodeURL(url)` reverses; validates version/class. `DecodePoePlannerURL()` imports from PoePlanner.
