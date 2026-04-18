# Config — Logic

## Core Concept

`Modules/ConfigOptions.lua` declares ~2000 config entries. Each entry drives both a UI control and an `apply()` function that translates user input into mods on `modList` / `enemyModList`.

## Condition Categories

- **Player Conditions**: `LowLife`, `FullLife`, `FullMana`, `LowMana`, `Moving`, `Stationary`, `FullEnergyShield`.
- **Player Buffs**: `Onslaught`, `Fortify`, `UnholyMight`, `Phasing`.
- **Enemy Debuffs**: `Cursed`, `Bleeding`, `Poisoned`, `Burning`, `Shocked`, `Chilled`, `Frozen`.
- **Minion Conditions**: `FullLife`, `LowLife`, `CreatedRecently`.
- **Multipliers**: `VirulenceStack`, `IntensityStack`, `CrabBarriers`, charge counts.

## Visibility Predicates

Each config row can gate itself via any combination of:

| Key | Test |
|-----|------|
| `ifNode` | passive / keystone allocated? |
| `ifOption` | another config var set? |
| `ifCond` | condition referenced by calc engine (`mainEnv.conditionsUsed`)? |
| `ifMult` | multiplier tracked non-zero? |
| `ifStat` | stat-ref referenced? |
| `ifMod` | mod name in `modsUsed`? |
| `ifSkill` | active skill matches? |
| `ifFlag` | skill-flag set (`RandomPhys`, `isPvP`, …)? |

## Config Sets (Scenarios)

Multiple named scenarios per build (e.g. "Normal Mapping", "Bossing", "Sirus Uber").

```lua
configSets[id] = { id, title, input = {...}, placeholder = {...} }
```

- `SetActiveConfigSet()` swaps active → rebuilds modList.
- Persisted as `<ConfigSet id="1" title="Bosses">…</ConfigSet>`.
- `input` = explicit user values; `placeholder` = defaults.

## Apply Pattern

```lua
apply = function(val, modList, enemyModList, build)
  if val then
    modList:NewMod("Condition:Onslaught", "FLAG", true, "Config")
  end
  modList:NewMod("Multiplier:PowerCharge", "BASE", val or 0, "Config")
end
```

Both player (`modList`) and enemy (`enemyModList`) DBs are mutated.

## Implication Chain

`implyCondList` / `implyCond` — when set, the config row is treated as "satisfied" even if the associated condition hasn't yet been tracked by the calc engine (e.g. Maim on enemy implied by an active Impale skill).
