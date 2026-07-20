# Items — UI

## ItemsTab Layout

- **Top**: weapon-swap buttons, passive-tree-spec dropdown, item-set selector, trade query button.
- **Equipment slots**: 18 slots with conditional visibility (grafts/charms only when applicable).
- **Left panel**: `ItemListControl` — all items owned by the build with drag-and-drop to slots.
- **Right panel**: unique DB (`ItemDBControl`) filterable by slot, type, league, source, search term (name or modifier).
- **Display item preview**: tooltip showing rarity, base, mods with colour-coded support state.

## Interactions

- **Paste from clipboard**: `Ctrl+V` parses raw PoE text into a new item.
- **Import from unique DB**: selecting an entry + choosing a roll variant creates a crafted unique.
- **Rare crafting**: prefix/suffix list + custom mod entries; Master / Essence / Beast mods available.
- **Trade search**: opens `TradeQuery` for the current slot/base with weights.
- **Compare**: hover a candidate item to see side-by-side stat delta vs equipped.

## Slot Control

`ItemSlotControl` — dropdown per slot showing owned items of that slot type. Handles:
- Empty state.
- Drag reorder within list.
- Right-click context menu (unequip, delete).

## Item List / Set List

- `ItemListControl` — drag-reorderable, multi-select, group-by rarity.
- `ItemSetListControl` — per-build item sets (map / boss / league-start).
- `SharedItemListControl`, `SharedItemSetListControl` — shared across builds via user's shared-item pool.

## Avalonia equipment workspace

The Avalonia port presents item management as three coordinated panes rather
than reproducing the dense upstream control grid:

1. **Equipped slots** — categorized weapon, armour, jewellery, flask, and
   passive-tree jewel slots, plus three charm slots for PoE2 only. PoE1 has its
   five numbered flask slots; PoE2 instead has exactly one Life Flask slot and
   one Mana Flask slot. The jewel section contains only sockets allocated in
   the active passive spec. Weapon set I/II is switched from the toolbar.
2. **Item library** — searchable reusable items, filtered to the selected slot
   by default, with equip, unequip, duplicate, edit, and guarded delete actions.
3. **Detail/editor** — a large item preview or a PoB-compatible raw-text editor.
   Copied in-game item text can be pasted directly, while new items start from a
   small slot-aware template.

Loadout creation, duplication, deletion, selection, and inline naming are kept
in the equipment toolbar. This replaces the separate import-only item-set picker
and makes the same controls available before any build has been imported.

The real `EquipmentView` is exercised in the dedicated Avalonia headless xUnit
project. Those tests run layout and bindings on the Avalonia dispatcher, use
headless pointer input for toolbar/editor actions, and capture a Skia-rendered
frame at the application's default 1280×800 size.
