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
| Heroic Tragedy  | 6 |

Current PoB seed ranges:

| Type | Item seed range | LUT seed range |
|------|-----------------|----------------|
| Glorious Vanity | 100–8,000 | 100–8,000 |
| Lethal Pride | 10,000–18,000 | 10,000–18,000 |
| Brutal Restraint | 500–8,000 | 500–8,000 |
| Militant Faith | 2,000–10,000 | 2,000–10,000 |
| Elegant Hubris | 2,000–160,000 in steps of 20 | 100–8,000 |
| Heroic Tragedy | 100–8,000 | 100–8,000 |

`DataLegionLookUpTableHelper.lua` (333 lines):

- Lazily loads compressed LUTs from `Data/TimelessJewelData/*.zip`.
- Input: `{jewelType, seed, passiveNodeIndex}` → replacement/addition operation;
  the conqueror variant separately determines the keystone.
- Node mapping via `NodeIndexMapping.lua` — 1931 total nodes indexed (452 notables for Glorious Vanity).

PathOfAvalonia parses the active PoB item variant into
`TimelessJewelSpec { type, seed, conqueror, conquerorId }`. The app asset
service loads the definitions and node mapping on its background loading path,
but supplies lazy stream factories for the six compressed LUTs. Only the LUT
for a jewel family actually resolved by the build is opened and inflated; the
result is then retained by the shared `TimelessJewelData`. When radius effects
rebuild, `PassiveSpec` resolves and caches the effective nodes for the active
seed. Rendering and tooltips therefore read cached names, icons, stats, and
conquered-node membership without mutating `TreeModel` or scanning every active
radius effect per node.

As in PoB's `ModParser`, the active seed modifier and named conqueror are the
source of truth for the jewel family. This also supports items created by
`TimelessJewelListControl`, whose display name is annotated as
`Unique Name [seed; score; socket]` rather than being the bare unique name.

The transformation order matches `PassiveSpec:BuildAllDependsAndPaths()`:

- Glorious Vanity replaces both small passives and notables using rolled LUT
  payloads (including Might/Legacy multi-addition outcomes).
- Other families use their LUT operation for notables and their conqueror rule
  for small passives (Strength, Dexterity, Devotion, blank Eternal passives, or
  Ward).
- Keystone replacement is selected by the named conqueror variant, independent
  of the seed.
- Once a node is conquered, ordinary radius-jewel stat transforms do not also
  modify it.

## Abyss / Charm Integration

Mods parse through `ModParser` as usual. `ItemsTab` shows abyss jewels in their slots; charms in charm slots. No radius; apply globally.

## Radius Integration

`PassiveTree` pre-computes `socket.nodesInRadius[radiusIndex]` (small=800,
medium=1200, large=1500 in tree units). PathOfAvalonia stores the equivalent
immutable socket and keystone memberships in a weak, tree-scoped cache, so
multiple builds over the same `TreeModel` do not repeat the source × radius ×
node calculation. `PassiveSpec` rebuilds active radius effects only when an
allocation change touches a socket, keystone, Oracle allocation passive, or an
already-active effect source; ordinary passive allocation does not reparse
every socketed jewel.

## UI Controls

- `TimelessJewelListControl` — type + conqueror + seed picker; decoded via LUT.
- `TimelessJewelSocketControl` — socket-scoped variant (per-socket filtering of replaced notables).
