# Calculations — Logic

## Entry Point Pipeline

`calcs.buildOutput()` (Calcs.lua:417) → `calcs.initEnv()` (CalcSetup.lua) → `calcs.perform()` (CalcPerform.lua:1098) → `calcs.offence()` + `calcs.defence()` + ailment calcs → `env.player.output`.

## Data Flow

1. **Setup** (`CalcSetup.lua`): initialises `env`, `env.modDB`, `env.enemyDB`, items, passive tree, gems.
2. **Perform** (`CalcPerform.lua`): master orchestration; applies conditions, merges keystones, builds minion skills.
3. **Offence** (`CalcOffence.lua:319`): hit damage per type, crit, attack/cast speed, ailments (ignite/bleed/poison).
4. **Defence** (`CalcDefence.lua:638`): life, ES, mana, armour, evasion, resistances, block.
5. **Output aggregation**: final DPS, survivability, DoT stacking.

## Key Structures

- `env.modDB` — ModDatabase of player modifiers, conditions, multipliers.
- `env.player.output` — result table: `TotalDPS`, `Life`, `Armour`, `CritChance`, `CritMultiplier`, `BleedDPS`, `IgniteDPS`, `PoisonDPS`, resistances, charges, speed.
- `env.skillModList` — active skill's modifier list (gem + supports + gear).
- `env.breakdown` — detailed formula breakdowns for UI.

## Scope System

ModDB uses scope tags: `Global` (all skills), `Cond` (conditional), `IgnoreCond` (override), plus source-specific (items, keystones, tree).

## Key File References

- `Modules/Calcs.lua:417` — buildOutput entry.
- `Modules/CalcSetup.lua:18` — initModDB.
- `Modules/CalcSetup.lua:386` — environment structure.
- `Modules/CalcPerform.lua:1098` — perform orchestrator.
- `Modules/CalcOffence.lua:319` — offence entry.
- `Modules/CalcOffence.lua:3550` — total DPS.
- `Modules/CalcOffence.lua:4300+` — ailment DPS.
- `Modules/CalcDefence.lua:638` — defence entry.
- `Modules/CalcDefence.lua:1044` — defences aggregate.
