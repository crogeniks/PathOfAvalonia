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

In the Avalonia port, `ItemTextSections` normalizes copied item text (line
endings and PoB colour codes), separates header/body lines, and tokenizes
leading `{tag}` metadata once in the domain. `RawItemParser` populates it on
`ImportedItem`; UI presentation and Build Planner export consume that model
rather than reparsing `RawText`.

### Store
`ItemsTab.items[id]` — all instantiated items for the build.

### Equip
`ItemSlotControl` binds an item ID to a slot: Weapon 1/2, Weapon 1 Swap/2 Swap, Helmet, Body Armour, Gloves, Boots, Amulet, Ring 1/2, Belt, Flasks 1–5, Jewels (tree sockets), Abyssal, Graft.

### Apply
`BuildModList()` converts each parsed mod line into game mechanics via `modLib.parseMod()`. Sockets (coloured, abyssal) apply socketed-gem mods. Imbued/scourge/catalyst modifications are layered.

## Item Sets
Alternate gear configurations per build (e.g. mapping/bossing). Each set owns a full equipment layout. Flask sets overlay on top. Switching is a reference swap — no data duplication.

### Avalonia equipment workspace

`EquipmentWorkspace` is the mutable per-build item store used by the Avalonia
equipment tab. It follows the upstream ownership model:

- The item library owns each `ImportedItem` once by ID.
- A loadout only maps canonical equipment slot names to those IDs.
- Passive-tree jewel sockets are stored separately from loadouts because they
  belong to the active passive spec in upstream PoB, not to an item set.
- Creating or editing an item passes PoB-compatible raw text through
  `RawItemParser`; imported and custom items therefore share one representation.
- `ApplyTo(ImportedBuild)` produces an updated build snapshot so subsequent
  Build Planner exports include item-library and loadout edits.

The current slot compatibility layer uses an item's imported or author-selected
default slot family. Exact base-type restrictions will move to the item-base
database when that upstream data is ported. Charm slots and charm equipment are
restricted to PoE2; that game exposes Charm 1 through Charm 3. Flask layout is
also game-specific: PoE1 exposes Flask 1–5, while PoE2 exposes distinct Life
Flask and Mana Flask slots. Legacy PoE2 imports using `Flask 1`/`Flask 2` are
normalized to those semantic slots, and each slot rejects the other flask type.

`PassiveSpec.SetSocketedJewel` updates a single socket without reapplying a build
import. It rebuilds cluster subgraphs and radius effects and prunes allocations
that are no longer permitted by the replaced jewel.

The native basic-stat calculator reads final `Armour:`, `Evasion Rating:`,
`Energy Shield:`, `Ward:`, and shield block properties from equipped copied item
text. Local flat/increased defence modifier lines on an item with the matching
final property are not applied globally, preventing those local modifiers from
being counted twice. Unconditional global attributes, pools, resistances, and
supported defence modifiers are parsed from item body lines. Swap weapons follow
the selected weapon set; flasks are excluded until activation/configuration state
is represented. Saved PoB XML item text often omits final defence properties; in
that case local armour-slot modifiers are conservatively excluded and the UI
marks aggregate item defences (and shield block) as partial lower bounds until
the versioned item-base database is ported.

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
