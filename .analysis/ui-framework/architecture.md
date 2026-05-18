# UI Framework — Architecture

PoB uses a **custom retained/immediate-mode hybrid** built on top of the host's low-level primitives.

## Host Primitives

The host runtime (SimpleGraphic / PoB engine) provides:

- `NewImageHandle(filename)` → image handle.
- `SetDrawColor(r, g, b, a)`.
- `SetDrawLayer(layer, sub)`.
- `DrawImage(handle, x, y, w, h, u0, v0, u1, v1)`.
- `DrawString(x, y, align, size, font, text)`.
- `GetStringCursorIndex(...)`, `DrawStringWidth/Height(...)`.
- Input callbacks: `OnFrame`, `OnKeyDown/Up`, `OnChar`, `OnMouseDown/Up/Wheel`, `OnSubCall`.

## Control Base (`Classes/Control.lua`)

Every widget inherits from `Control`:

```lua
Control = {
  x, y, width, height,   -- layout
  anchor = { point, other, otherPoint, collapse },
  enabled,
  shown,                 -- fn or bool
  tooltipText / tooltipFunc,
}
```

Key methods:

- `GetPos()` — recursively resolves anchor chain to an absolute (screen) position using 9 normalised anchor points: 8 cardinals + CENTER.
- `IsMouseOver()` — bounds check using current absolute position.
- `SetAnchor(point, other, otherPoint, x?, y?)` — attach to parent/sibling.

## ControlHost (`ControlHost.lua`)

Parent-of-controls mixin. Owns a `controls` table and:

- `DrawControls(viewPort)` — loops visible children, respects `shown` / `enabled`.
- `ProcessControlsInput(inputEvents)` — dispatches key/char/mouse events; returns `self` to claim focus, `nil` to pass through.
- `TabAdvance()` — cycles focus using shared `tabOrder` arrays.

Every tab (`TreeTab`, `SkillsTab`, …) inherits `ControlHost`.

## Event Propagation

- Top-level `Main:OnKeyDown()` routes to `currentTab:OnKeyDown()` → `ControlHost:ProcessControlsInput()` → focused child first, then unfocused ones.
- Returning `self` from `OnKeyDown` = "I handled it"; suppresses default behaviour.

## Focus

- Single focused control at a time; click/tab moves it.
- Edit-style controls steal focus on click; blur on click outside or Escape.

## Layers

`SetDrawLayer(layer, sub)` — integer layers (0..9) + sub-order. Tooltips typically use layer 10.

## Draw Loop

Each frame:
1. Host calls `OnFrame()`.
2. `Main:OnFrame()` → current build's tab `:Draw(viewPort)`.
3. Tab draws its controls via `ControlHost:DrawControls()`.
4. Tooltips drawn last, on a separate layer.
