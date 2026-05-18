# Skills & Gems — Logic

## Socket Group Model

A **socket group** represents one linked set of gems (i.e. what you'd socket into an item's linked colour group).

```lua
socketGroup = {
  label,                -- user-visible name
  enabled = true,
  slot = "Body Armour", -- item location: "None" | "Weapon 1" | "Weapon 2" | ...
  source,               -- nil = manual; non-nil = granted by item (immutable)
  gemList = { gemInstance, gemInstance, ... },
  includeInFullDPS = false,
  imbuedSupport    = "Added Fire Damage",
  mainActiveSkill  = 1, -- index into gemList
}
```

- First non-support in `gemList` is the active skill unless `mainActiveSkill` overrides.
- `enabled` toggles the whole group; gems can also be individually disabled.

## Gem Instance

```lua
gemInstance = {
  nameSpec     = "Fireball",
  gemId        = "Metadata/Items/Gems/SkillGemFireball",
  skillId      = "Fireball",
  level        = 20,
  quality      = 23,
  enabled      = true,
  count        = 1,
  skillPart        = 1,       -- multi-part selector (e.g. Scorching Ray stages)
  skillMinion      = "...",   -- minion variant pick
  skillMineCount   = 2,       -- for mines/traps/stage-based
}
```

## Support Validation (`CalcActiveSkill.lua:createActiveSkill`)

`canGrantedEffectSupportActiveSkill()` checks:

- `grantedEffect.requireSkillTypes[]` — active skill must have ALL of these types.
- `grantedEffect.excludeSkillTypes[]` — must have NONE.
- `grantedEffect.weaponTypes{}` — weapon gating.
- Minion skill-type gating for summoner supports.
- Excludes: non-gemmed item-granted skills, Vaal-on-trigger combos.

## Quality Variants / Awakened

- `variantId` on gem data — Anomalous / Divergent / Phantasmal selection.
- `plusVersionOf` links an Awakened gem to its base.
- `legacy = true` marks legacy/locked variants.
- Quality scales both the standard quality stats and any `qualityStats[]` from the variant.

## Auras / Curses / Buffs

- Active-skill's `buffList` contains aura / curse / buff definitions with `GlobalEffect` tag.
- When the group's `enabled = true` and skill is an aura, reserves life/mana automatically (calc engine).

## Item-Granted Skills

`socketGroup.source = "Item:<slot>:<mod>"` — group derived from an item mod; gemList is recomputed on item change and user edits are rejected (except skill-part / enable toggle).

## Imbued Supports

`imbuedSupportBySlot{}` — one imbued support per slot. Applied as an additional support to every gem socketed in that slot.
