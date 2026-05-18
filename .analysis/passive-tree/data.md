# Passive Tree — Data

## Source Format

Per-version under `src/TreeData/{version}/`:

- `tree.lua` (pre-converted lua) — primary.
- `data.json` (PoE official JSON) — fallback; auto-converted via `jsonToLua()` and cached as `tree.lua`.
- `sprites.lua` / `sprites.json` — sprite sheet metadata.

## Root Keys

| Key | Purpose |
|-----|---------|
| `nodes` | All nodes (ID → node) |
| `groups` | Node group defs |
| `classes` | Character classes (0–6) |
| `ascendancies` / `alternate_ascendancies` | Ascendancy class defs |
| `constants` | `skillsPerOrbit`, `orbitRadii` |
| `assets` | Sprite URLs + coords |
| `sprites` / `skillSprites` | Sprite sheets |
| `max_x/min_x/max_y/min_y` | Tree bounding box |

## Node

```lua
node = {
  id,                     -- skill ID
  dn,                     -- display name
  sd,                     -- stat array (flavour text)
  stats,                  -- parsed numeric bonuses
  x, y,                   -- computed from group + orbit
  group, orbit, orbitIndex,
  type,                   -- Normal|Notable|Keystone|Socket|Mastery|ClassStart|AscendClassStart
  isJewelSocket,
  isKeystone, isNotable, isMastery,
  masteryEffects = { {effect, stats}, ... },
  ascendancyName,
  classStartIndex,
  isAscendancyStart,
  icon, activeIcon, inactiveIcon,
  sprites,                -- sprite map entry
  overlay,                -- frame type
  modList,                -- parsed ModList
  linkedId,               -- connected IDs
  out, in,                -- connection arrays
}
```

## Group

```lua
group = {
  x, y,
  nodes,                  -- node IDs in this group
  orbits,                 -- orbit indices present
  ascendancyName,         -- if ascendancy group
  isAscendancyStart,
}
```

## Class

```lua
class = {
  id, name,                                -- "Marauder", ...
  base_str, base_dex, base_int,            -- starting attributes
  classes / ascendancies,                   -- ascendancy defs
  startNodeId,
}
```

## Ascendancy

```lua
ascendClass = {
  id, name,
  flavourText, flavourTextRect,
  startNodeId,
  passive,                -- mastery passives (Ascendant)
}
```

## Version Management

- `treeVersions[ver] = {num, display, legacy}`.
- `latestTreeVersion` — global default.
- v3.10+ migrates node format (skill→id, group→g).
- Sprite zoom: older trees use array index, v3.19+ uses `0.3835` / `1.0` scale keys.

## Pre-built Lookup Maps (PassiveTree.lua:455–461)

- `keystoneMap[name]` (case-insensitive)
- `notableMap[name]`
- `ascendancyMap[name]`
- `clusterNodeMap[name]` — cluster jewel proxies.

## TreeData Directory Versions

`2_6`, `3_10`–`3_25` (and their `_ruthless` variants); each folder carries its own tree, sprites, and legacy node data for old builds.
