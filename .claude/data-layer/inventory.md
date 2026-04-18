# Data Layer — Inventory

Static game data under `src/Data/`. All files are auto-generated from GGG data (see `import-export/export-pipeline.md`).

## Item Bases (`Bases/`, 22 files)

Per slot / weapon type: `amulet`, `axe`, `belt`, `body`, `boots`, `bow`, `claw`, `dagger`, `fishing`, `flask`, `gloves`, `graft`, `helmet`, `jewel`, `mace`, `quiver`, `ring`, `shield`, `staff`, `sword`, `tincture`, `wand`.

Each file returns `itemBases[baseName]` entries.

## Uniques (`Uniques/`, 27 files)

Mirror of base slot files; plus `Special/` subfolder:
- `BoundByDestiny.lua`, `Generated.lua`, `New.lua`, `WatchersEye.lua`, `race.lua`.

Each file returns an array of raw-text unique blocks.

## Skills (`Skills/`, 11 files)

Active / support / granted skills, grouped by attribute:
- `act_dex.lua`, `act_int.lua`, `act_str.lua` — active gems by attribute.
- `sup_dex.lua`, `sup_int.lua`, `sup_str.lua` — supports by attribute.
- `glove.lua` — granted by gloves.
- `minion.lua` — minion-specific skills.
- `spectre.lua` — spectre monster skills.
- `other.lua` — misc (item-granted, trigger-only).

## Stat Descriptions (`StatDescriptions/`, 23 files)

Per-category mod text → numeric scope: `aura`, `buff`, `brand`, `curse`, `minion`, `monster`, `debuff`, …

## Modifier Pools (16 files)

`ModItem.lua`, `ModItemExclusive.lua`, `ModFlask.lua`, `ModGraft.lua`, `ModJewel.lua`, `ModJewelAbyss.lua`, `ModJewelCharm.lua`, `ModJewelCluster.lua`, `ModMap.lua`, `ModMaster.lua`, `ModNecropolis.lua`, `ModTincture.lua`, `ModVeiled.lua`, `ModFoulborn.lua`, `ModFoulbornMap.lua`.

## Enchantments (7 files)

`EnchantmentBody.lua`, `EnchantmentHelmet.lua`, `EnchantmentBoots.lua`, `EnchantmentGloves.lua`, `EnchantmentWeapon.lua`, `EnchantmentBelt.lua`, `EnchantmentFlask.lua`.

## Metadata & Reference

- `Gems.lua` — active + support gem metadata (tags, requirements, effectiveness).
- `Global.lua` — colour hex codes, global constants.
- `Misc.lua` — monster scaling tables (evasion, accuracy, life, damage, armour).
- `Minions.lua` — minion life/resist/skill/mod definitions.
- `Spectres.lua` — usable spectres list.
- `Pantheons.lua` — Pantheon god paths + soul effects.
- `Bosses.lua`, `BossSkills.lua` — boss presets for config tab.
- `TattooPassives.lua` — tattoo mod pool.
- `Essence.lua` — essence crafting pools.
- `Crucible.lua` — Crucible tree / passive pool.
- `BeastCraft.lua` — beastcrafting recipes.
- `ClusterJewels.lua` — cluster jewel layout tables.
- `Costs.lua` — skill cost defs.
- `SkillStatMap.lua` — cross-cutting stat→mod transforms.
- `Rares.lua` — rare monster templates.
- `QueryMods.lua` — trade-site query metadata.
- `ModCache.lua` — pre-computed mod parse cache (performance).
- `FlavourText.lua` — flavour text strings.

## TimelessJewelData/

- `LegionPassives.lua` — replaceable passive list.
- `LegionTradeIds.lua` — trade stat-ID map.
- `NodeIndexMapping.lua` — PoE node ID → LUT index.

## TreeData (`src/TreeData/`)

Per-patch tree snapshots. Folders: `2_6`, `3_10`–`3_25` (plus `_ruthless` variants). Each contains `tree.lua` + `sprites.lua` (auto-generated from GGG JSON). Allows opening builds from any supported patch.
