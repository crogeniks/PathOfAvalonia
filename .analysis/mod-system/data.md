# Mod System — Data

## Mod Object Shape (ModTools.lua:20–46)

```lua
{
  name         = "FireDamage",
  type         = "INC",                      -- aggregation method
  value        = 15,                          -- numeric OR {value, mod} table
  flags        = ModFlag.Gem,                 -- bitfield
  keywordFlags = KeywordFlag.Fire,
  source       = "Skill:FireBolt",
  -- positional elements [1..n] are tags:
  [1] = { type = "Multiplier", var = "CastSpeed", div = 2 },
  [2] = { type = "Condition",  var = "Burning" },
}
```

## Mod `type` Domain

`BASE`, `INC`, `MORE`, `FLAG`, `LIST`, `OVERRIDE`, `MAX`, `MIN`.

## Tag Types (ModStore:317–889)

| Category | Tags |
|----------|------|
| Scale | `Multiplier`, `MultiplierThreshold`, `PerStat`, `PercentStat`, `StatThreshold` |
| Proximity | `DistanceRamp`, `MeleeProximity` |
| Cap | `Limit` (global cumulative cap per key, lines 892–902) |
| Condition | `Condition`, `ActorCondition`, `ItemCondition`, `SocketedIn` |
| Skill | `SkillName`, `SkillId`, `SkillPart`, `SkillType` |
| Slot | `SlotName` |
| Bitfield | `ModFlagOr`, `KeywordFlagAnd` |
| Enemy | `MonsterTag` |

## Global Limits

`Limit` tags share a per-key counter across every mod that references the same var. Used for stackable unique effects (e.g. "× per endurance charge, up to N times").

## Source String Conventions

- Tree nodes: `"Tree:<nodeId>"`
- Items: `"Item:<slot>"` or `"Item:<rarity>:<name>"`
- Gems: `"Skill:<skillName>:<level>"`
- Config: `"Config"`
- Keystones: `"Keystone:<name>"`
