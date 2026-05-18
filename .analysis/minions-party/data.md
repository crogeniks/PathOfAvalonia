# Minions & Party — Data

## Minion Entry (`Data/Minions.lua`)

```lua
minions["RaisedZombie"] = {
  name           = "Raised Zombie",
  monsterTags    = { "zombie", "undead", "cold_resist_high" },
  monsterCategory= "Undead",

  life           = 1.0,    -- multiplier on base monster life
  armour         = 1.5,
  fireResist     = 40,
  coldResist     = 40,
  lightningResist= 40,
  chaosResist    = 0,

  damageSpread   = 0.20,
  damageFixup    = 1.0,

  skillList      = { "MonsterBasicStrikeZombie", "MonsterZombieSlam" },
  modList        = { ... },   -- intrinsic mods
}
```

Monster-level base values come from `Data/Misc.lua` scaling tables (evasion, accuracy, life, damage, armour).

## Spectre List (`Data/Spectres.lua`)

Array of monster IDs approved for spectre use, with level and category metadata. Referenced by `MinionSearchListControl`.

## Party Export Code

Same envelope as a build code, but contains only the subset of tabs relevant for party play (auras active, curses active, party tab config). Encoded with the same `XML → zlib → base64 URL-safe` pipeline.

## Party Flags on Mods

Tags used when party mods are imported:
- `Condition:PartyMember` — gate for party-only mods.
- `Condition:PartyBuff` — applied by imported buffs.
- `GlobalEffect` — ensures survival through conditional scoping.
