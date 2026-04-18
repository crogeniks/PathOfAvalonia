# Mod System — Parser

`Modules/ModParser.lua` (~6783 lines) converts in-game modifier text into structured mod objects.

## Entry Point

`parseMod(line, order)` (line 6406):

1. Tokenises against `formList` (lines 67–153) — 50+ regex patterns to detect form.
2. Extracts quantifier + mod form: `INC`, `RED`, `MORE`, `LESS`, `BASE`, `GAIN`, `LOSE`, `FLAG`, `CHANCE`, `PEN`, `REGEN`, `DMG`, `OVERRIDE`, `DOUBLED`.
3. Scans `modNameList`, `modFlagList`, `modTagList`, `specialModList` for semantic bindings.

## Key Tables

- `formList` (67–153) — regex → form descriptor.
- `modNameList` (156–5804) — ~2000+ entries mapping text (e.g. `"strength and dexterity"`) → internal names (`{"Str","Dex","StrDex"}`) with optional tag attachment.
- `modFlagList` — attaches ModFlag bits (melee/spell/attack…).
- `modTagList` — attaches runtime tags (Condition, Multiplier, ActorCondition, SkillType, …).
- `specialModList` — lookup for keystones and hand-crafted rules that don't fit the grammar.

## Special Cases (6548–6754)

- `FLAG` → sets `modValue.name/type`.
- `DOUBLED` → emits two mods: 100% MORE plus a Multiplier override.
- `GRANTS`/`REMOVES` → add a Condition tag for hand attacks.
- `addToAura` / `addToMinion` / `addToSkill` → wrap mods in `ExtraAuraEffect` / `MinionModifier` / `ExtraSkillMod` LIST types.

## Caching

`cache[line]` stores the parsed result (6759–6782). Returns `(modList, leftoverText)` where leftover is any unrecognised trailing text.
