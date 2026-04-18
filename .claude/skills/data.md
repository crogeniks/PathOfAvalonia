# Skills & Gems — Data

## Gem (`Data/Gems.lua`)

```lua
["Metadata/Items/Gems/SkillGemFireball"] = {
  name            = "Fireball",
  grantedEffectId = "Fireball",
  variantId       = "Fireball",      -- quality variants use different IDs
  plusVersionOf   = nil,             -- set for Awakened gems
  vaalGem         = false,
  tags            = { projectile=true, spell=true, area=true, fire=true, ... },
  reqStr / reqDex / reqInt = 0,
  naturalMaxLevel = 20,
}
```

## Granted Effect / Skill (`Data/Skills/*.lua`)

```lua
skills["Fireball"] = {
  name            = "Fireball",
  color           = 3,                -- 1=Red (Str), 2=Green (Dex), 3=Blue (Int)
  support         = false,
  skillTypes      = { [SkillType.Spell]=true, [SkillType.Area]=true, ... },
  requireSkillTypes = { SkillType.Damage },
  excludeSkillTypes = { SkillType.Minion },
  weaponTypes     = { ["Wand"]=true, ["Staff"]=true },
  parts           = { { name = "Cast", stage = true }, ... },
  levels = {
    [1]  = { stat1, stat2, ..., levelRequirement = 1,  cost = { Mana = 10 }, ... },
    [20] = { ... },
  },
  statMap = { ["stat_id"] = { mod(...), mod(...) } },
}
```

## Support Gem

Same shape as skill, plus:
- `support = true`.
- `requireSkillTypes[]` — array of required SkillType enum values.
- `excludeSkillTypes[]`.
- `ignoreMinionTypes` — bypass minion-skilltype check.
- `isTrigger` — triggers are one-per-group.
- `addSkillTypes[]` — types added to supported skill.

## SkillType Enum

`Damage`, `Attack`, `Spell`, `Area`, `Projectile`, `Melee`, `Cast`, `Duration`, `Aura`, `Curse`, `Movement`, `Mine`, `Trap`, `Totem`, `Minion`, `Vaal`, `Fire`, `Cold`, `Lightning`, `Physical`, `Chaos`, `Triggered`, `MeleeSingleTarget`, `RangedAttack`, …

## Colour Domain

| value | attribute |
|-------|-----------|
| 1 | Str (Red) |
| 2 | Dex (Green) |
| 3 | Int (Blue) |
| 4 | White |

## Level-up Scaling

Per-level table contains: required level, required attribute (str/dex/int), cost (Mana/Life/Rage/…), stat1–N (skill-specific numeric rows mapped via `statMap`).
