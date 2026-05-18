# UI Framework — Controls

## Input

| Control | Purpose |
|---------|---------|
| `EditControl` | Single- or multi-line text entry. Caret, selection, clipboard, undo stack. Placeholder text. Filter regex. |
| `ResizableEditControl` | Edit control bounded by min/max w/h; grows with content. |
| `CheckBoxControl` | Toggle + label on right. `state` bool. |
| `SliderControl` | Normalised 0–1 knob; optional discrete `divCount`. Scroll wheel for fine-tune. |
| `DropDownControl` | Button + popup list + scrollbar. `list[]`, `selIndex`. Optional `selFunc` callback on change. |
| `DraggerControl` | Click + drag; returns delta; right-click callback. |

## Buttons / Labels / Primitives

| Control | Purpose |
|---------|---------|
| `ButtonControl` | Click callback, optional image + label + tooltip. |
| `LabelControl` | Read-only text; supports `align`, `colour`. |
| `RectangleOutlineControl` | Stroke rectangle (divider/border). |

## Containers

| Control | Purpose |
|---------|---------|
| `ListControl` | Table: rows + columns, selection, drag-to-reorder, column sort, multi-select. Every tab's data list derives from this. |
| `TextListControl` | Scrolled multi-column text list (simpler than `ListControl`). |
| `SectionControl` | Decorative section wrapper: border + title label + child region. |
| `PopupDialog` | Centred modal with title, body controls, buttons. Blocks input behind it. |
| `ScrollBarControl` | Vertical/horizontal scroll. Knob, arrows, `step` / `page`. |

## Overlays / Mixins

| Control | Purpose |
|---------|---------|
| `Tooltip` | Layout helper for multi-section tooltip contents. |
| `TooltipHost` | Mixin adding `tooltip` attachment + render path to any control. |
| `SearchHost` | Mixin adding a search bar + filter callback to lists (used by Gem selector, item DB). |

## Patterns

- **Tab** = `ControlHost` subclass with many `controls.*` children anchored to the viewport.
- **Right-click context** implemented manually per control; no framework-level menu.
- **Keyboard shortcuts** live at the tab level (`OnKeyDown`), not bound to controls.
- **Drag preview**: controls implementing drag set `main.dragDrop = { ... }` and read `main.mouseOverControl`.
