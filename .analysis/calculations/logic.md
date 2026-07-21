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

## Avalonia basic-stat milestone

`TreeDomain.Calculations.BasicStatCalculator` is the first native calculation
slice. It deliberately does not attempt to port `calcs.perform()` wholesale.
It consumes a stable snapshot of:

1. Effective stat lines from allocated passives (`PassiveSpec` applies mastery,
   radius/timeless jewel, PoE2 attribute-choice, and weapon-set selection first).
2. The active equipment loadout and selected weapon set.
3. Character level imported from build XML or edited in the Calculations UI.

Resistance penalty is deliberately not configurable. The native calculator
always uses the worst campaign penalty (-60%) for both games.

The calculator parses only unconditional basic-stat forms, evaluates attributes
before their inherent life/mana/defence bonuses, then evaluates pools and basic
defences. `EquipmentViewModel` refreshes the result after spec, equipment,
level, loadout, or weapon-set changes. Saved `<PlayerStat>` values are
retained as a comparison snapshot, not used as calculator inputs.

After a passive-spec change, `PassiveSpec` counts allocated point-consuming
nodes and `CharacterProgression` applies PoB's act/quest-point progression
estimate. `EquipmentViewModel` raises the shared level to that minimum before
recalculating. It does not lower the level after refunds; an imported level is
also retained when it is already higher than the allocation minimum.

Unsupported relevant lines are counted and surfaced as partial-coverage UI.
Conditional effects, buffs, flasks, reservations, keystone flags/conversions,
skill costs, EHP/max-hit simulation, and all DPS remain future milestones.

### Passive hover preview

The calculator also accepts an optional `PassiveAllocationPreview`. The preview
is a set overlay on the stable allocated-node snapshot: queued nodes are added,
or the target and refund dependents are removed, before effective passive stat
lines are parsed. It never mutates `PassiveSpec`.

`PassiveTreeViewModel` derives that overlay from the current hover target.
`BuildWorkspaceState` coordinates it with `EquipmentViewModel`, which evaluates
the projected totals against the same active items, level, fixed worst penalty,
and weapon set as the committed result. The projected rows and their deltas feed both the
tree sidebar and the passive tooltip. Leaving the node restores the committed
totals immediately.

Allocating or refunding a socket with an equipped radius jewel may change the
effective state of other nodes. That follow-on state transition is not yet
simulated by the overlay, so those previews carry an explicit partial warning.
