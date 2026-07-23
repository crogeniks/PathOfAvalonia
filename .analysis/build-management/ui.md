# Build Management — UI

## Build List Browser (`BuildList.lua`, `BuildListControl.lua`)

- Main list: folders + XML files sorted by name / class / edited time / level.
- `FolderListControl` navigation with a `PathControl` breadcrumb.
- Toolbar: New, New Folder, Open, Copy, Rename, Delete, Sort dropdown.
- Clipboard: Ctrl+X / Ctrl+C / Ctrl+V move builds across folders.
- Search bar (`searchText` EditControl) with regex exclusions.
- Drag-drop: build → path crumb to move between folders.

## External Build Providers (`ExtBuildListControl`, `ExtBuildListProvider`, `PoBArchivesProvider`)

- Tabbed provider system — each provider contributes one or more named lists (e.g. Trending / Latest / Similar).
- `ExtBuildListProvider` base: `.listTitles`, `GetBuilds()`, `GetActiveList()`.
- `PoBArchivesProvider` queries `https://pobarchives.com/api/builds` → JSON → preview cards: name, author, mainSkill, class, ascendancy, DPS, life, EHP, version.
- Buttons: Import (loads build code), Preview (opens external URL).
- "Similar Builds" tab uses similarity scoring against the active build.

## Build Sidebar (within Build.lua)

- Build name + class / level header, character buttons.
- Tab switcher buttons (one per viewMode).
- Stat readout populated via `AddDisplayStatList()`.
- Import code input/output field (base64 deflate).

## PathOfAvalonia workspace header

The fixed workspace header contains an editable build name plus Save, Save As,
saved-build selection, Open, New, and Delete controls. Save updates the current
record; Save As creates a new stable id. Status text reports local storage
results and an unsaved-change indicator is driven by character, equipment, name,
or Atlas mutations. For PoE1 the same save action includes Atlas allocations, so
the Atlas tab does not maintain a second, drifting file identity.
