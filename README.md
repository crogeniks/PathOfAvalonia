# PathOfAvalonia

PathOfAvalonia is a cross-platform Path of Exile build and passive-tree planner
built with [Avalonia UI](https://avaloniaui.net/) and .NET. It is an early-stage
Avalonia/C# port of Path of Building, currently focused on passive planning,
build import, equipment, jewels, basic character stats, and Atlas planning.

The app ships with the following embedded data and visual assets:

| Game | Passive tree versions | Default | Other tree data |
| --- | --- | --- | --- |
| Path of Exile | `3.28.0`, `3.29.0` | `3.29.0` | Atlas `3.29.0` |
| Path of Exile 2 | `0.4.0`, `0.5.0` | `0.5.0` | — |

## Features

- Passive tree rendering, search, class and ascendancy selection, allocation,
  hover path previews, and allocation reset for Path of Exile and Path of
  Exile 2.
- Tree version switching and visual tree diffs where multiple embedded
  versions are available.
- Path of Exile import from passive tree URLs, Path of Building codes, and
  `pobb.in` URLs.
- Path of Exile 2 import from Path of Building 2 codes and `pobb.in` URLs, plus
  import and export of official Build Planner `.build` files.
- Passive-tree and item-set variant selection for imported builds.
- Equipment loadouts, an item library and editor, socketed jewels, and imported
  skill-group display.
- Cluster, radius, and timeless jewel behavior for supported Path of Exile
  builds.
- Live basic character stats from the passive tree and equipment, including
  hover-preview deltas. Imported calculation values are shown separately as
  comparison snapshots.
- Path of Exile 3.29 Atlas tree planning with search, allocation summaries, and
  local persistence.
- A local build library with save, save-as, open, new, and delete workflows.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or newer.
- A desktop environment supported by Avalonia.

## Getting Started

Restore and build the full solution:

```sh
dotnet restore PathOfAvalonia.slnx
dotnet build PathOfAvalonia.slnx
```

Run the desktop app:

```sh
dotnet run --project src/TreeApp/PathOfAvalonia.TreeApp.csproj
```

Run the test suite:

```sh
dotnet test PathOfAvalonia.slnx
```

## Repository Layout

```text
PathOfAvalonia.slnx
src/
  TreeApp/        Avalonia desktop app, controls, views, view models, services, assets wiring
  TreeDomain/     Passive tree model, loaders, importers, jewels, cluster logic
tests/
  PathOfAvalonia.TreeDomain.Tests/
                  Domain, service, and view-model tests
  PathOfAvalonia.TreeApp.HeadlessTests/
                  Avalonia headless interaction and rendering tests
assets/
  PoE1/           Path of Exile passive/Atlas data and visual assets
  PoE2/           Path of Exile 2 tree data and visual assets
  Shared/         Shared jewel radius assets
tools/
  poe2-assets/    Legacy PoE2 sprite tooling for PoB-era assets
  tree-assets/    Notes for asset conversion and historical sprite-map generation
```

## Development Notes

The solution uses a `.slnx` solution file and targets `net10.0` across the app,
domain library, test projects, and asset tool. The desktop app embeds files
under `assets/` as Avalonia resources through
`src/TreeApp/PathOfAvalonia.TreeApp.csproj`.

Tree loading and planning behavior lives in `src/TreeDomain`. UI state and
interaction mediation live in `src/TreeApp/ViewModels`, while custom tree
rendering is handled by the controls in `src/TreeApp/Controls`.

Game asset path conventions are isolated behind `IGameAssetLayout`
implementations in `src/TreeApp/Services`. The app registers one layout per
game and resolves them through `IGameAssetLayoutRegistry`.

Saved builds and settings are stored locally under
`%APPDATA%\PathOfAvalonia` on Windows, or
`$XDG_CONFIG_HOME/PathOfAvalonia` (falling back to
`~/.config/PathOfAvalonia`) on other platforms.

## Asset Generation

PoE2 now loads the official GGG passive tree export directly. Refresh the
embedded PoE2 assets by copying a downloaded export into `assets/PoE2`:

```sh
mkdir -p assets/PoE2/0_5_0/assets
cp /path/to/poe2-skilltree-export-0.5.0/data.json assets/PoE2/0_5_0/data.json
cp /path/to/poe2-skilltree-export-0.5.0/assets/* assets/PoE2/0_5_0/assets/
```

At runtime the app derives each PoE2 sprite map from that version folder's
`assets/skills.json`, `assets/frame.json`, and `assets/jewel.json`.

## Testing Focus

The domain and application test project covers:

- PoE1 and PoE2 passive-tree loaders.
- PoE1 Atlas loading, allocation, stat aggregation, and view-model behavior.
- PoE2 sprite map loading.
- Passive spec allocation and import application.
- PoE1 and PoE2 build import, including `pobb.in`.
- PoE2 Build Planner import and export.
- Cluster jewel insertion and socketed jewel behavior.
- Radius and timeless jewel behavior.
- Equipment workspaces, basic stat calculation, local build persistence, and
  application view-model state transitions.

The headless Avalonia test project covers core UI journeys and interactions in
the passive tree, Atlas tree, and equipment views.

Run `dotnet test PathOfAvalonia.slnx` before making behavioral changes to tree
logic, import handling, or view model state.

## Status

PathOfAvalonia is under active development and is not yet a full replacement
for Path of Building. Current calculations cover basic character totals; the
broader calculation engine and other Path of Building workflows remain parity
work in progress.
