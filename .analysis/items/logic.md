# Items — Logic

## Lifecycle

### Parse
`Item:ParseRaw()` parses text (in-game copy/paste, trade fetch, unique DB entry). It extracts:

- Rarity.
- Base type → `Data/Bases/<slot>.lua` lookup.
- Quality.
- Sockets / links / gems-in-item.
- Modifier lines (implicit / explicit / crafted / enchant / scourge / crucible / desecrated).
- Influence flags (Shaper, Elder, Warlord, Hunter, Crusader, Redeemer, Searing Exarch, Eater of Worlds).
- Requirements.

### Store
`ItemsTab.items[id]` — all instantiated items for the build.

### Equip
`ItemSlotControl` binds an item ID to a slot: Weapon 1/2, Weapon 1 Swap/2 Swap, Helmet, Body Armour, Gloves, Boots, Amulet, Ring 1/2, Belt, Flasks 1–5, Jewels (tree sockets), Abyssal, Graft.

### Apply
`BuildModList()` converts each parsed mod line into game mechanics via `modLib.parseMod()`. Sockets (coloured, abyssal) apply socketed-gem mods. Imbued/scourge/catalyst modifications are layered.

## Item Sets
Alternate gear configurations per build (e.g. mapping/bossing). Each set owns a full equipment layout. Flask sets overlay on top. Switching is a reference swap — no data duplication.

## Quality & Catalysts

- Quality scales base stats (armour/evasion/ES, physical weapon damage) by `(1 + q/100)`.
- Catalysts scale specific mod-tag pools on jewellery: Attack, Speed, Life, Caster, Attribute, Chaos, Resistance, Defense, Elemental, Critical.
- `getCatalystScalar()` applies catalyst tags to matching mods only.

## Sockets

- Colour defaults to attribute requirement: R=Str, G=Dex, B=Int, W=white.
- Abyssal sockets are independent of links.
- Links grouped by `-` in `Sockets:` field; `R-G B W` = RG linked, B and W solo.

## Influence

`influenceItemMap` maps flag to a mod pool namespace (e.g. `shaper_sword`). 8 influence types; a single item can carry multiple (combining in Maven orb etc.).
