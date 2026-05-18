# Calculations — Maths

## Hit Damage (CalcOffence.lua:68–139)

```
Dmg(type) = (Base_min × (1 + Inc/100) × More) + AddMin
          + (Base_max × (1 + Inc/100) × More) + AddMax
Inc  = sum of INC mods (%)
More = product of MORE mods (each as 1 + v/100)
```

Conversions are applied recursively in order `Physical → Lightning → Cold → Fire → Chaos`; each conversion routes a fraction of base damage through the destination type's INC/MORE pipeline.

## Average DPS (CalcOffence.lua:3550)

```
TotalDPS      = AverageDamage × HitSpeed × dpsMult × quantityMult
AverageDamage = AverageHit × HitChance/100
AverageHit    = NonCritAvg × (1 - CC/100) + CritAvg × (CC/100)
```

## Critical (CalcOffence.lua:2900+)

```
CritEffect = 1 + (CritMultiplier - 100)/100 × (combined crit mods)
Actual     = (NonCritDmg + CritDmg × CritEffect) / 100
```

Crit chance caps at 100% after bonuses, with base floor from base crit × (1 + Inc/100) × More; "always crit" flag overrides.

## Bleed DPS (CalcOffence.lua:4306)

```
BaseBleedDPS = avgHitBleedDmg × (basePercent/100) × effectMod × rateMod
             × activeBleeds × effMult
final        = min(BaseBleedDPS, DotDpsCap)
avgHitBleedDmg = min + (max - min) × bleedRollAverage/100
```

## Ignite DPS (CalcOffence.lua:4400+)

```
IgniteDotMulti = Sum(BASE, "DotMultiplier") + Sum(BASE, "FireDotMultiplier")
IgniteDPS      = sourceFireDmg × IgniteDotMulti × effectMod × duration
```

## Poison DPS (CalcOffence.lua:4500+)

```
PoisonDPS = sourceChaosDmg × (basePercent/100) × dotMultiplier × effectMod
          × activePoisonStacks
```

## Life (CalcPerform.lua:90)

```
Life = max(Base × (1 + Inc/100) × More × (1 - Conv/100), 1)
if ChaosInoculation: Life = 1
```

## Energy Shield (CalcDefence.lua:1044)

```
ES = Base × (1 + Inc/100) × More
```

Conversions (Mana→ES, Life→ES, etc.) are added as separate sources with their own INC/MORE.

## Armour Mitigation (CalcDefence.lua:41–50)

```
Reduction% = Armour / (Armour + Raw × 5) × 100
Raw        = incoming hit damage after resistances
```

## Evasion / Hit Chance (CalcDefence.lua:32–37)

```
HitChance = Accuracy / (Accuracy + (Evasion/5)^0.9) × 125
clamp to [5, 100]
```

## Resistance

```
EffectiveDmg = BaseDmg × (1 - Resist/100)
cap Resist ≤ 75% (per-type override possible)
```

## Block / Suppression

Block roll: `min(BlockChance, MaxBlock)` after suppress/avoidance fails. Suppression halves 50% of incoming spell damage when rolled.
