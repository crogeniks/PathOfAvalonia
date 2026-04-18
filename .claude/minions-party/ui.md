# Minions & Party — UI

## Minion Selection

Within a summoner skill group, the `MinionListControl` renders a dropdown per summon skill:

- Lists available minion variants (e.g. Raging Spirits → variant-less; Animate Weapon → per weapon class).
- Shows current level / quality inherited from the parent skill.
- For Spectres: `MinionSearchListControl` — searchable list of allowed monsters; filter by tag (caster / melee / elemental), level range.

## Minion Stat Panel

Sidebar-like readout appears when a minion skill is main. `minionDisplayStats` (BuildDisplayStats.lua) drives rows: Life, ES, DPS (hit / crit / ignite / total), Resistances, Speed. Same compare-delta rules as player stats.

## PartyTab (`Classes/PartyTab.lua`)

- Top: party member list; Add / Remove slots.
- Each slot: paste another build's party export code.
- Below: toggle which auras / curses / warcries / buffs from the other build apply to this one.
- Read-only view of the other build's party-visible stats.
- Right pane: "Enemy Conditions" — flags that multiple party members contribute to (Marked, Cursed, Withered, Exposed).

## Totem / Mine / Trap

Rendered inline with the skill group; no dedicated tab. Config tab options expose placement count, stage count, and trap cooldown.
