# Items — Data

## Rarity Domain

`NORMAL`, `MAGIC`, `RARE`, `UNIQUE`, `RELIC`.

## Slot Types

`weapon`, `armour`, `jewellery`, `flask`, `jewel`, `abyss jewel`, `charm`, `graft`.

Subtypes: `"One Handed Sword"`, `"Two Handed Axe"`, `"Body Armour - Armour"`, `"Ring"`, `"Life Flask"`, etc.

## Base Row

```lua
itemBases[baseName] = {
  type         = "Body Armour",
  subType      = "str_dex",
  socketLimit  = 6,
  tags         = { armour = true, body_armour = true, str_armour = true, ... },
  influenceTags= { shaper = "body_armour_shaper", elder = "body_armour_elder", ... },
  implicit     = "... stat text ...",
  implicitModTypes = { ... },
  armour / evasion / energyShield / weapon / flask = { ... },   -- base stats
  req          = { level = 62, str = 120, dex = 50 },
}
```

## Unique Entry (`Data/Uniques/<category>.lua`)

Each file returns a list of multi-line raw-text strings. Each entry is literal PoE item text, optionally broken into `Variant:` blocks:

```
Kaom's Heart
Glorious Plate
Variant: Pre 2.0
Variant: Pre 3.0
Variant: Current
...
```

Parsed via same `Item:ParseRaw()` path as user input; `variantList`, `variant` index select active variant.

## Influence → Mod Pool

`influenceItemMap`:

```lua
{
  shaper      = "shaper",
  elder       = "elder",
  warlord     = "adjudicator",
  hunter      = "basilisk",
  crusader    = "crusader",
  redeemer    = "eyrie",
  exarch      = "cleansing",
  eater       = "tangled",
}
```

Combined with base tags to select mod pools (e.g. `shaper_sword`).

## Mod Pool Files (`Data/Mod*.lua`)

- `ModItem.lua`, `ModItemExclusive.lua` — rare affix pools.
- `ModJewelAbyss.lua`, `ModJewelCluster.lua`, `ModJewelCharm.lua` — jewel pools.
- `ModFlask.lua` — flask pools.
- `ModGraft.lua`, `ModFoulborn.lua`, `ModFoulbornMap.lua` — league-specific.

Rows include affix name, level requirement, tier weight, tag list, numeric range, and the text template.
