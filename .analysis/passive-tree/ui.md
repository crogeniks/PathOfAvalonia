# Passive Tree — UI

## TreeTab (TreeTab.lua)

- `specList[]` — multiple saved trees per build; `SetActiveSpec()` swaps active.
- Dropdown shows class, ascendancy, points used, jewel sockets. Respec cost is shown when switching.
- Tree viewer instance: `self.viewer = PassiveTreeView`.
- Controls: spec dropdown, add/remove/rename buttons.

## TreeView Zoom & Pan (PassiveTreeView.lua)

- `zoomLevel` int; `zoom = 1.2^zoomLevel` (exponential).
- `zoomX`, `zoomY` — pan offsets.
- **Pan**: left-drag updates `zoomX/Y` after 5+ px threshold.
- **Zoom**: Page Up/Down ±1/3 levels; Ctrl+click ±2 levels; wheel ±1/3 levels.

Coord conversion:
```
scale     = min(vpW, vpH) / tree.size * zoom
screenPos = treePos * scale + offset
treePos   = (screenPos - offset) / scale
```

Pan clamped: `zoomX/Y ∈ [-vpW·k, +vpW·k]` where `k = zoom · 2/3`.

## Node Rendering

Each node renders a frame sprite based on state + type:

| State | Normal | Notable | Keystone | Socket | Mastery |
|-------|--------|---------|----------|--------|---------|
| Unallocated | `PSSkillFrame` | `NotableFrame` | `KeystoneFrame` | `JewelFrame` | `AscendancyFrameLarge` |
| Path-able | `PSSkillFrameHighlighted` (yellow) | `NotableFrame…` | … | … | … |
| Allocated | `PSSkillFrameActive` | `NotableFrameActive` | `KeystoneFrameActive` | `JewelFrameActive` | active icon overlay |

Ascendancy variants: `AscendancyFrameSmall*`.

Hover: `hoverNode` set when cursor intersects node `rsq` (radius squared).

## Jewel Sockets

- Concentric radius rings per jewel type (Legion/Eternal/Karui/…).
- `jewelShadedOuterRing`, `jewelShadedInnerRing` — rotating shaded backgrounds.
- Nodes inside radius pre-computed in `PassiveTree.lua:594–651`, stored in `socket.nodesInRadius[radiusIndex]`.
- Charm sockets: no radius display.

## Shift-Click Path Trace

When Shift is held, `traceMode = true`; `tracePath` contains the sequence of nodes from selected back to class start. Nodes in that trace render in a distinct colour.

## Search / Highlight

- `searchStr`, `searchStrSaved`, `searchStrCached` + `searchStrResults` cache.
- Case-insensitive name/stat match → highlighted overlay.

## Tooltips

`AddNodeTooltip()` — name, stats, type (Notable/Keystone/Socket/Mastery), source mods. When comparing specs: colour-coded diff (red=worse, green=better). Mastery effect options listed for allocated mastery nodes.
