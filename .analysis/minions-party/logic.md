# Minions & Party — Logic

## Minion Actor Model

Minions are modelled as child actors of the player:

- `env.minion` — dedicated calc environment per minion skill, isolated from `env.player`.
- Each minion has its own `modDB` initialised via `calcs.initModDB()` from base stats in `Data/Minions.lua` (life, armour, evasion, resists, monster level scaling).
- Minion runs its own abbreviated `offence` / `defence` pass; resulting DPS and survivability bubble up as `env.player.output.Minion*` stats.

## Player → Minion Mod Flow

Player mods aimed at minions are stored as `MinionModifier`-typed LIST mods on the player. During `CalcPerform.lua` (~1426–3184):

1. All mods of type `MinionModifier` are extracted from `modDB`.
2. They are appended to the minion's `modDB` with a `GlobalEffect` tag so they survive conditional scoping.
3. Skill gems can also force inheritance via flags:
   - `minionUseMainHandWeapon` — weapon stats flow through.
   - `minionUseGloves`, `minionUseItemSet` — piece inheritance.

## Totems

Flagged via `skillFlags.totem` on the active skill. They use the player's stats but apply the totem-specific damage penalty and speed modifiers (e.g. "Totems use your modifiers to cast speed"). Totem-limit counter tracked via `Multiplier:ActiveTotemLimit`.

## Mirages & Copies (`Modules/CalcMirages.lua`)

Five supported mirage types, each copies the player skill into a separate calc environment:

| Type | Notes |
|------|-------|
| **Mirage Archer** (Support) | Inherits bow attack; less damage / speed caps. |
| **Saviour Warriors** | Sword Saviour unique. |
| **Tawhoa's Chosen** | Ranger ascendancy mirror. |
| **Sacred Wisps** | Martyr of Innocence unique. |
| **General's Cry** | Warcry-triggered exertion copies. |

Each applies the relevant less-damage / speed / cap mods, and prevents resource (mana / rage) cost accounting on the copy.

## Party Play (`Classes/PartyTab.lua`)

- Supports team-play calculations (aurabot, curse-spreader, etc.).
- Imports another build's aura / curse / warcry effects via saved party codes.
- Applies buffs to this build's `modDB` with a `Condition:PartyBuff` tag.
- Applies debuffs/curses to `enemyModList`.
- Shares party-wide enemy conditions like "Cursed" / "Marked".

## Enemy Modifiers

`env.enemyDB` mirrors `modDB` for the enemy target. Used by config options (map mods, boss presets), party curses, poisons, withers, etc. Resistances, armour, and level default from the enemy preset selected in Config tab.
