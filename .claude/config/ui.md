# Config — UI

## ConfigTab Layout (`ConfigTab.lua:14–624`)

### Top Bar

- **Set selector** (dropdown) — switch active config set.
- **Search bar** (EditControl) — regex filter on label.
- **Show All** button — toggle ineligible entries.
- **Manage** button — opens `ConfigSetListControl` popup.

### Section Grid

- Sections auto-lay into up to 3 columns, each 370 px wide.
- Each `SectionControl` contains the var controls.
- Section height = sum of visible child heights.
- `col` property — preferred column; auto-distribute otherwise.

## Control Type Mapping

| `type` | Control |
|--------|---------|
| `check` | `CheckBoxControl` |
| `count` / `integer` / `float` | `EditControl` (numeric validation) |
| `list` | `DropDownControl` |
| `text` | `EditControl` or `ResizableEditControl` |
| `computed` | bare `Control` (display-only) |

## Visibility Logic (lines 218–440)

`control.shown()` returns true iff:

1. Search pattern matches label (case-insensitive regex).
2. Not blacklisted by `isShowAllConfig()` (legacy, PvP, "recently", "in last N seconds").
3. All `shownFuncs` (ifNode, ifOption, ifCond, ifMult, ifSkill, …) pass.
4. If `hideIfInvalid = false`, still render when value is set but the underlying condition isn't tracked.

## Invalid Config Highlight

- Red border + red label when value is set but the condition the option depends on is not currently satisfied (e.g. "While Onslaught" enabled but no Onslaught source).
- Tooltip: "conditional with missing source and invalid".
- Placeholder values render in a lighter colour; active (user-set) in a darker border.

## Draw Flow (764–851)

1. Process input events + undo/redo (Ctrl+Z/Y).
2. Compute column layout + scrollbar offset.
3. Refresh set-dropdown list.
4. `ControlHost:DrawControls(viewPort)` renders everything.
