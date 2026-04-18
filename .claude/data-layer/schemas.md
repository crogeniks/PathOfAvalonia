# Data Layer — Schemas

## `Data/Gems.lua`

Indexed by GGG gameId (metadata path):

```lua
["Metadata/Items/Gems/SkillGemFireball"] = {
  name, baseTypeName, variantId, grantedEffectId,
  tags = { spell = true, projectile = true, fire = true, area = true, ... },
  tagString,
  reqStr, reqDex, reqInt,
  naturalMaxLevel,
}
```

## `Data/Bases/<slot>.lua`

Indexed by base name:

```lua
itemBases["Titan Greaves"] = {
  type       = "Boots",
  subType    = "Str",
  socketLimit= 4,
  tags       = { armour = true, str_armour = true, boots = true, ... },
  influenceTags = { shaper = "boots_shaper", elder = "boots_elder", ... },
  implicitModTypes = { ... },
  armour = { Armour = 150 },
  req    = { level = 68, str = 120 },
}
```

Weapons carry `weapon = { PhysicalMin, PhysicalMax, CritChanceBase, AttackRateBase, Range }`.

## `Data/Uniques/<slot>.lua`

Returns an **array of raw multi-line strings**:

```lua
return {
[[Kaom's Heart
Glorious Plate
...
Variant: Pre 2.0
Variant: Current
...
]],
...
}
```

Parsed at runtime by `Item:ParseRaw()`.

## `Data/Mod*.lua`

Indexed by affix unique id:

```lua
["ItemPhysicalDamagePercent1"] = {
  type      = "Prefix",
  affix     = "Heavy",
  description = "#% increased Physical Damage",
  statOrder = { 100 },
  level     = 1,
  group     = "LocalPhysicalDamagePercent",
  weightKey = { "sword", "default" },
  weightVal = { 1000, 0 },
  modTags   = { "damage", "attack", "physical" },
}
```

## `Data/Skills/*.lua`

Indexed by skill name:

```lua
skills["Fireball"] = {
  name, baseTypeName,
  color       = 3,
  baseEffectiveness, incrementalEffectiveness,
  description,
  skillTypes  = { [SkillType.Spell] = true, ... },
  statDescriptionScope,
  castTime,
  statMap     = { ["stat_id"] = { mod(...) } },
  baseFlags   = { spell = true, area = true, ... },
  baseMods    = { ... },
  qualityStats= { Default = { { "stat_id", value } }, Anomalous = { ... }, ... },
  levels      = { [1] = { ... }, [20] = { ... } },
}
```

## `Data/Pantheons.lua`

```lua
pantheons["Soul of the Brine King"] = {
  isMajorGod = true,
  souls = {
    [1] = { name = "Captured Soul of Brine King", mods = { { line, value } } },
    [2] = { ... },
  },
}
```

## `Data/Minions.lua`

```lua
minions["RaisedZombie"] = {
  name, monsterTags, monsterCategory,
  life, armour, fireResist, coldResist, lightningResist, chaosResist,
  damageSpread,
  damageFixup,
  skillList = { "MonsterBasicStrikeZombie", "MonsterZombieSlam" },
  modList   = { ... },
}
```

## `Data/Misc.lua`

Global monster-scaling tables (evasion, accuracy, life, damage, armour) indexed by monster level.

## `Data/Enchantment*.lua`

Indexed by source tag (`HARVEST`, `HEIST`, `LABYRINTH`, …); value is an array of enchantment text strings.

## `TreeData/<ver>/tree.lua`

See `passive-tree/data.md`.

## `TimelessJewelData/`

- `LegionPassives.lua` — `{ nodeIndex = passiveNodeId, ... }`.
- `LegionTradeIds.lua` — replacement stat → `trade_stat_id`.
- `NodeIndexMapping.lua` — `{ passiveNodeId = nodeIndex }`.
