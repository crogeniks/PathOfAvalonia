# Calculations — Data Contract

## ModDB Core (CalcSetup + modLib)

- `mods[statName]` — list of Mod objects `{name, type, value, source, flags, keywordFlags, tags...}`.
- `conditions` — boolean map: `Ignited`, `Frozen`, `FullLife`, `CritRecently`, `OnKill`, …
- `multipliers` — counter map: `PowerCharge`, `FrenzyCharge`, `PoisonStack`, `NearbyAlly`, …
- `mods.Keystone` — passive keystone mods.

## Mod Types

| Type | Meaning |
|------|---------|
| `BASE` | Additive foundation (added to base) |
| `INC` | Increased/reduced — `(1 + Σ/100)` |
| `MORE` | Multiplicative — product of `(1 + v/100)` |
| `OVERRIDE` | Replaces final value |
| `FLAG` | Boolean gate |
| `LIST` | Appended to a list (aura/curse/extra skill) |
| `MAX` / `MIN` | Clamp value |

## Scope Tag Types

- `Condition` — `var` = ignited/crit/onkill/etc.
- `ActorCondition` — `actor` ∈ enemy/player/minion, `var` = condition name.
- `ItemCondition`, `SocketedIn`, `SlotName`.
- `Multiplier` — `var` = counter name (charges/stacks), optional `div`, `limit`.
- `MultiplierThreshold` / `StatThreshold` — gate on value.
- `PerStat` / `PercentStat` — scale per stat value.
- `DistanceRamp`, `MeleeProximity`, `Limit`.
- `SkillName`, `SkillId`, `SkillPart`, `SkillType`.
- `ModFlagOr`, `KeywordFlagAnd`, `MonsterTag`.
- `IgnoreCond` — overrides conditional scope hierarchy.

## Damage Type Enum (CalcOffence.lua:33–43)

```lua
dmgTypeList  = {"Physical","Lightning","Cold","Fire","Chaos"}
dmgTypeFlags = {Physical=0x01, Lightning=0x02, Cold=0x04,
                Fire=0x08, Elemental=0x0E, Chaos=0x10}
```

## ModFlag (bitfield)

`Attack`, `Spell`, `Melee`, `Projectile`, `Hit`, `DoT`, `Bow`, `Claw`, `Wand`, `Weapon1H`, `Weapon2H`, `Elemental`, `Cold`, `Fire`, `Lightning`, `Physical`, `Chaos`.

## Output Keys (env.player.output)

- **Damage**: `TotalDPS`, `AverageDamage`, `AverageHit`, `CritChance`, `CritMultiplier`.
- **Defence**: `Life`, `EnergyShield`, `Armour`, `Evasion`, `FireResist`, `ColdResist`, `LightningResist`, `ChaosResist`.
- **Ailments**: `BleedDPS`, `IgniteDPS`, `PoisonDPS`, `ImpaleDPS`, `DecayDPS`.
- **Speed**: `Speed`, `HitSpeed`, `CastSpeed`, `ActionSpeedMod`.
- **Charges**: `PowerCharges`, `FrenzyCharges`, `EnduranceCharges`, …

## Breakdown (env.player.breakdown)

Maps stat → nested formula-step table for UI display (e.g. `breakdown.TotalDPS = {base, incMult, final}`).
