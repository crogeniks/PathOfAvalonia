# Jewels — Data

## Cluster Jewels (`Data/ClusterJewels.lua`)

Per jewel-base entry (~12K lines total):

```lua
clusterJewels = {
  jewels = {
    ["Large Cluster Jewel"]     = { size = "Large",  ... },
    ["Medium Cluster Jewel"]    = { size = "Medium", ... },
    ["Small Cluster Jewel"]     = { size = "Small",  ... },
  },
  notableSortOrder = { ["NotableName"] = orderIndex, ... },
  -- orbit-index templates per (size, passive_count)
}
```

Per passive-skill entry:
- `id` (internal key)
- `name` (notable)
- `skill`/`tag` classification (e.g. `attack`, `spell`, `life`, `aura`)
- `stats` — raw mod lines

## Timeless Jewel LUTs (`Data/TimelessJewelData/`)

- `LegionPassives.lua` — list of replaceable nodes per jewel type.
- `LegionTradeIds.lua` — mapping to trade-site stat IDs (for trade queries).
- `NodeIndexMapping.lua` — PoE tree node ID → LUT index (1931 nodes total).
- Compressed LUT blobs (outside repo, downloaded to user data dir) — keyed by `(jewelType, conqueror, seed, nodeIndex)`.

## Abyss Jewel Mods (`Data/ModJewelAbyss.lua`)

Affix rows:

```lua
{
  type    = "Prefix" | "Suffix",
  affix   = "Annealed",
  description = "+# to Level of Socketed Gems",
  statOrder = { 100 },
  level   = 10,
  group   = "PhysDmgLocal",
  weightKey = { "searing_exarch_jewellery_mods", "default" },
  weightVal = { 0, 100 },
  modTags = { "damage", "attack", "physical" },
}
```

## Charm Mods (`Data/ModJewelCharm.lua`)

Same shape as abyss mods, scoped to charm-type jewels.

## Cluster Mods (`Data/ModJewelCluster.lua`)

Affix pool specifically for the enchant that determines cluster node count / notable count. Separate small-passive and notable mod tables referenced by `ClusterJewels.lua`.
