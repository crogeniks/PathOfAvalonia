# Jewels — Logic

## Jewel Types

1. **Abyss Jewels** — socketed into Abyssal sockets; apply mods globally (subject to slot/condition tags).
2. **Charm Jewels** — new-league variant; similar to abyss but with charm-specific mod pool (`ModJewelCharm.lua`).
3. **Cluster Jewels** — socketed on tree periphery; spawn a full subgraph of extra nodes.
4. **Timeless (Legion) Jewels** — replace the stats of every passive in radius, deterministic from `{seed, jewelType, conqueror}`.

## Cluster Jewels (`Data/ClusterJewels.lua`)

Three sizes:

| Size   | Passive count | Notable slots |
|--------|---------------|---------------|
| Small  | 2–3 | 1 |
| Medium | 4–6 | 1–2 |
| Large  | 8–12 | 3–5 |

Data table keys: small-passive pool, notable pool, socket positions in orbit, base type tag.

Graph generation happens in `PassiveSpec:BuildClusterJewelGraphs()`:

- Read item properties `clusterJewelSkill`, `clusterJewelNodeCount`.
- Pick notables / smalls matching the skill tag.
- Append subgraph to tree: new node IDs ≥ 65536, linked through the socket node.

## Timeless Jewels

Types & id:

| Name | `jewelTypeId` |
|------|---------------|
| Glorious Vanity | 1 |
| Lethal Pride    | 2 |
| Brutal Restraint| 3 |
| Militant Faith  | 4 |
| Elegant Hubris  | 5 |

Seed ranges: Glorious Vanity `1–7999`; others `0–32767`.

`DataLegionLookUpTableHelper.lua` (333 lines):

- Lazily loads compressed LUTs from `Data/TimelessJewelData/*.zip`.
- Input: `{jewelType, conqueror, seed, passiveNodeIndex}` → replacement stats.
- Node mapping via `NodeIndexMapping.lua` — 1931 total nodes indexed (452 notables for Glorious Vanity).

## Abyss / Charm Integration

Mods parse through `ModParser` as usual. `ItemsTab` shows abyss jewels in their slots; charms in charm slots. No radius; apply globally.

## Radius Integration

`PassiveTree` pre-computes `socket.nodesInRadius[radiusIndex]` (small=800, medium=1200, large=1500 in tree units). `PassiveSpec` iterates these for every equipped jewel on tree refresh.

## UI Controls

- `TimelessJewelListControl` — type + conqueror + seed picker; decoded via LUT.
- `TimelessJewelSocketControl` — socket-scoped variant (per-socket filtering of replaced notables).
