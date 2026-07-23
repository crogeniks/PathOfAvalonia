# Build Management — Logic

## Lifecycle

- **Create**: `BuildList:New()` → `main:SetMode("BUILD", false, "Unnamed build")` — empty build, TREE viewMode, level 1.
- **Load**: `Build:LoadDBFile()` → `LoadDB(xmlText)` parses via `common.xml.ParseXML()`, restores every tab from child nodes, handles version conversion popup.
- **Save**: `SaveDB()` → `common.xml.ComposeXML()` builds `<PathOfBuilding>` root + `<Build>` + per-tab child sections, writes to disk.
- **Delete**: confirm popup → `os.remove()` / `RemoveDir()`.

## Tabs (viewMode enum)

`TREE`, `SKILLS`, `ITEMS`, `CALCS`, `CONFIG`, `NOTES`, `PARTY`, `COMPARE`, `IMPORT`.

Switching tabs updates `controls.mode<X>.locked` so the current tab's button reads as pressed.

## Refresh / Recalc

- `modFlag` set `true` on any data change.
- Per-frame: `CalcPerform:PerformCalc()` recomputes via `CalcSetup` → `CalcOffence` / `CalcDefence`.
- Result cached on `calcsTab.mainEnv`.

## Sidebar Stats

`BuildDisplayStats.lua` — `displayStats[]` defines rows: stat name, label, format, colour, visibility condition.

- `AddDisplayStatList()` — populates the sidebar from `player.output`.
- `CompareStatList()` — computes delta % for node/item hover compare.
- `minionDisplayStats` — separate panel when a minion skill is main.

## Undo / Redo (`UndoHandler`)

- `.undo`, `.redo` stacks (max 101 states).
- `CreateUndoState()` snapshots the build after every significant change.
- `RestoreUndoState()` rolls back.
- `Undo()` / `Redo()` traverse stacks.
- Mouse4 / Mouse5 in the tree trigger PathControl undo/redo.

## External Import Flow

- Paste build code → base64 URL-safe decode → `inflate()` zlib → XML → `LoadDB()`.
- Export → `ComposeXML()` → zlib deflate → base64 URL-safe.

## PathOfAvalonia local build lifecycle

PathOfAvalonia exposes a local build library in the workspace header.
`GameWorkspaceViewModel` coordinates Save, Save As, Open, New, and Delete while
`IBuildLibraryService` owns config-directory I/O. A save captures the current
`PassiveSpec` through `CreateBuildSnapshot`, the equipment/loadout projection,
and the optional Atlas version and allocations in one immutable `SavedBuild`.

The last saved build id is stored in user settings. `ShellViewModel` loads that
record first on startup, selects its game and character-tree version, creates a
normal workspace, and restores the snapshot through public domain/view-model
operations. Missing saves fall back to the last game rather than preventing the
application from opening. Opening another saved build is blocked while the
current workspace has unsaved changes.

This is deliberately PathOfAvalonia-owned JSON rather than upstream PoB XML:
PoB has no Atlas-planner section, while the import model is the canonical
representation for the character features currently ported. External PoB/build
code compatibility remains in the import pipeline.
