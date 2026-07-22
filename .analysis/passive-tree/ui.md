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

### Avalonia bottom toolbar

The Avalonia tree places class, ascendancy, passive-tree variant selection,
search, and version comparison in a fixed two-row toolbar below the tree
canvas; these controls do not float over the rendered tree. The import workflow
is available from the toolbar's **Import** button and opens in a flyout so the
tree keeps its full canvas area when importing is not in progress. Terms are
case-insensitive and all must match a node's name, stats, mastery effects, or
type; quoted text is treated as one term. Matches receive a red outline, and
the overlay reports the live match count. Class starts and non-selectable
mastery decorations are omitted, matching PoB's exclusion. Unlike PoB's Lua
pattern search and anoint-recipe (`oil:`) search, this initial port uses safe
literal text matching because recipe data is not currently exposed by the
domain model.

## Tooltips

`AddNodeTooltip()` — name, stats, type (Notable/Keystone/Socket/Mastery), source mods. When comparing specs: colour-coded diff (red=worse, green=better). Mastery effect options listed for allocated mastery nodes.

The Avalonia tree tooltip also shows supported basic-stat differences. For an
unallocated passive it previews the node and every queued path node; for an
allocated passive it previews refunding the target and any disconnected
dependents. Positive changes are green and negative changes are red, matching
PoB's `PassiveTreeView.lua:1512–1559` interaction at the current calculator's
smaller coverage level.

## Avalonia live stat sidebar

The passive-tree workspace reserves a 286-pixel left column for locally
calculated basic stats, corresponding to PoB's persistent build sidebar. It
shares character level with the Calculations tab; both views always use the
fixed worst resistance penalty. While a passive is hovered, the column displays projected totals and inline
green/red deltas; no allocation is committed. Pointer exit restores the current
build totals. The preview notice occupies a fixed-height, single-line slot even
while hidden, so entering or leaving a node does not move the stat groups.
Detailed partial-preview warnings remain in the passive tooltip. Partial
item-defence and unsupported-modifier warnings remain visible at the bottom of
the column.

The sidebar groups rows under Attributes, Pools, Recovery, Defences, Avoidance,
Resistances, and Movement headings. Compact separated row cards provide vertical
rhythm, while values use semantic colors (including distinct attribute, pool,
defence, and elemental-resistance tones) so adjacent totals remain scannable.

When a passive click commits an allocation, the shared level is raised to PoB's
estimated minimum for the allocated passive count. The level is intentionally
one-way: refunding passives does not lower a higher manual or imported level.
