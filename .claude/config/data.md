# Config — Data

## Config Option Row

```lua
{
  var          = "enemyIsBoss",         -- key into input[]
  section      = "Enemy Stats",
  label        = "Boss:",
  col          = 1,                     -- preferred column (1-3)

  type         = "list",                -- check|count|integer|float|list|text
  resizable    = false,                 -- for text
  defaultState = "None",
  defaultIndex = 1,                     -- for list
  defaultPlaceholderState = nil,
  list         = { { val="None", label="None" }, { val="Boss", label="Boss" }, ... },

  ifNode       = "keystoneName",
  ifOption     = "someVar",
  ifCond       = "Stationary",
  ifMult       = "VirulenceStack",
  ifStat       = "PerFlatPrecision",
  ifMod        = "LifeOnBlock",
  ifSkill      = "Cyclone",             -- string or table
  ifSkillFlag  = "randomPhys",
  ifSkillData  = "explodeCorpse",

  tooltip      = "Help text.",
  tooltipFunc  = function(...) return "..." end,
  inactiveText = "Not available",
  implyCondList= { "CondA", "CondB" },
  implyCond    = "StatCondition",

  legacy          = true,
  doNotHighlight  = false,
  hideIfInvalid   = false,

  apply = function(val, modList, enemyModList, build)
    if val == "Fire" then
      modList:NewMod("FireExposure", "BASE", 100, "Config")
    end
  end,
}
```

## Boss Row (`Data/Bosses.lua`)

```lua
bosses["BossName"] = {
  armourMult  = 50,    -- % scaling vs normal armour
  evasionMult = 0,
  isUber      = true,
}
```

## Boss Skill Preset (`Data/BossSkills.lua`)

```lua
["Atziri Flameblast"] = {
  DamageType = "Spell",                         -- Spell|Melee|SpellProjectile|DamageOverTime
  DamageMultipliers = { Fire = { 51.087, 0.255 } },  -- { base_at_70%, per_roll_range_% }
  UberDamageMultiplier = 1.26,
  UberSpeed = nil,
  DamagePenetrations      = { FirePen = 8 },
  UberDamagePenetrations  = { FirePen = 10 },
  speed      = 25000,                           -- ms
  critChance = 0,
  additionalStats = {
    base = { CannotBeBlocked = "flag", ... },
    uber = { CannotBeBlocked = "flag", ... },
  },
  tooltip    = "...",
  earlierUber= true,
}
```

## Enemy Presets

`enemyIsBoss` list: `None → Boss → Pinnacle → Uber`. Each preset sets placeholders for resistances (40/50%), damage (×1.5 × DPS mult), level (83/84/85). Boss-skill preset applies damage multipliers and penetrations.

- `SetPlaceholder(var, val)` — default for a var.
- `input[var]` — explicit user override (wins over placeholder).
