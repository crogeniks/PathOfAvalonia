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
