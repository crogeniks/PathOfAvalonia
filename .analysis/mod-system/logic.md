# Mod System — Logic

## Class Hierarchy

- `ModStore` (`Classes/ModDB.lua:21+`) — abstract query interface.
- `ModDB` — organises mods by name in hash tables for O(1) lookup.
- `ModList` — stores mods as flat arrays (better for iteration).

Different containers optimise different access patterns; all expose the same `Sum/List/Flag/Override/Tabulate` surface.

## Query Pipeline

`:Sum(modType, cfg, ...)` → `SumInternal(context, modType, cfg, flags, keywordFlags, source, ...)` iterates relevant name keys, filters by `mod.type`, then evaluates tags via `EvalMod()` (`ModStore:312–903`).

| Operator | Behaviour |
|----------|-----------|
| `:Sum()` | adds values |
| `:More()` | multiplies, rounding to 2 dp (high-precision override via `data.highPrecisionMods`) |
| `:Flag()` | returns boolean |
| `:Override()` | first match wins |
| `:List()` | collects into array |
| `:Tabulate()` | returns `{value, mod}` pairs |

## Scope / Inheritance Chain

- `GetCondition(var, cfg, noMod)` → `self.conditions[var]` or parent chain → falls back to `:Flag(conditionName[var])`.
- `GetMultiplier(var, cfg, noMod)` → sums stored `multipliers[var]` + parent chain + `Sum("BASE", multiplierName[var])`.
- Magic tables (`ModStore:21–27`) lazily route `conditionName[x] = "Condition:"..x`.

## Config Object

A `cfg` table passed to queries gates which mods apply:
```lua
cfg = {
  flags = ModFlag.Spell | ModFlag.Fire,
  keywordFlags = KeywordFlag.Ignite,
  skillName = "Fireball",
  skillPart = 1,
  slotName = "Body Armour",
  source = "Skill:FireBolt",
}
```

## Source Tracking

Every mod carries a `source` string (e.g. `"Tree:Node"`, `"Item:Weapon 1"`, `"Skill:...:Level 20"`) used for the calc breakdown display, global limits, and debugging.
