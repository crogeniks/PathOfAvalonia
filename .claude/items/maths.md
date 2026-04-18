# Items — Maths

## Stat Stack

Final displayed stat = `Base + Implicit + Explicit + Enchant + Corruption + Crucible`, each scaled by its own quality/catalyst path where applicable.

## Quality Scaling

```
Armour/Evasion/ES      = base × (1 + quality/100)
Physical Weapon Damage = base × (1 + quality/100)
```

Catalyst scalar applies per-mod, not globally.

## Base Stats (Data/Bases/)

Example weapon entry:
```lua
weapon = {
  PhysicalMin     = 4,
  PhysicalMax     = 9,
  CritChanceBase  = 5,
  AttackRateBase  = 1.55,
  Range           = 11,
}
```

Armour base:
```lua
armour = {
  Armour  = 120,
  Evasion = 80,
  EnergyShield = 0,
}
```

## Ranged Mod Rolls

`itemLib.applyRange(line, range)` resolves `(min-max)` brackets with a 0–1 range knob. Produces the formatted line for display and the numeric mod for the calc engine.

## Weapon DPS (displayed in tooltip)

```
pDPS       = ((pMin + pMax)/2) × AttackRate × (1 + quality/100)
eDPS       = Σ elemental dmg mid × AttackRate
DPS        = pDPS + eDPS + chaosDPS
```

## Socket Colours — BtH

PoB doesn't simulate Chromatic Orb probability; it honours whatever colours are written in the item, defaulting new sockets to the item's primary attribute colour.
