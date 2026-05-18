# PathOfAvalonia

PathOfAvalonia is a desktop Path of Exile passive tree planner built with
[Avalonia UI](https://avaloniaui.net/) and .NET. The project is an Avalonia/C#
porting effort focused on the passive tree, build import, jewels, and related
planning workflows from Path of Building-style tooling.

The app currently ships with embedded tree data and visual assets for:

- Path of Exile, tree version `3.28`
- Path of Exile 2, tree version `0.4`

## Features

- Cross-platform Avalonia desktop shell.
- Game selection for Path of Exile and Path of Exile 2.
- Passive tree rendering with class and ascendancy selection.
- Passive allocation, hover path previews, and clear/reset support.
- Path of Exile import from passive tree URLs and Path of Building build codes.
- Path of Exile 2 import from Path of Building 2 build codes.
- Equipment import display for supported imported builds.
- Cluster jewel and socketed jewel domain support.
- Jewel radius visuals and radius-based node effect handling.
- Embedded PoE1, PoE2, and shared tree assets.

## Requirements

- .NET SDK 10.0 or newer.
- A desktop environment supported by Avalonia.

For PoE2 asset regeneration only:

- `zstd`
- ImageMagick, available as `magick`

## Getting Started

Restore and build the solution:

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
  TreeApp/        Avalonia desktop app, views, view models, services, assets wiring
  TreeDomain/     Passive tree model, loaders, importers, jewels, cluster logic
tests/
  PathOfAvalonia.TreeDomain.Tests/
                  xUnit coverage for tree loading, imports, jewels, view models
assets/
  PoE1/           Path of Exile tree data and visual assets
  PoE2/           Path of Exile 2 tree data and visual assets
  Shared/         Shared jewel radius assets
tools/
  poe2-assets/    PoE2 sprite and atlas generation utility
  tree-assets/    Notes for asset conversion and sprite-map generation
```

## Development Notes

The solution uses a `.slnx` solution file and targets `net10.0` across the app,
domain library, test project, and asset tool. The desktop app embeds files under
`assets/` as Avalonia resources through `src/TreeApp/PathOfAvalonia.TreeApp.csproj`.

Tree loading and planning behavior lives in `src/TreeDomain`. UI state and
interaction mediation live in `src/TreeApp/ViewModels`, while custom tree
rendering is handled by `src/TreeApp/PassiveTreeView.cs`.

## Asset Generation

The PoE2 sprite generator can rebuild the PoE2 sprite map and packed icon atlas
from external Path of Building 2 tree data and assets:

```sh
dotnet run --project tools/poe2-assets/generate-poe2-sprites.csproj -- \
  --tree /path/to/PathOfBuilding-PoE2/src/TreeData/0_4/tree.json \
  --tree-assets /path/to/PathOfBuilding-PoE2/src/TreeData/0_4 \
  --ui-assets /path/to/PathOfBuilding-PoE2/src/Assets \
  --out assets/PoE2 \
  --version 0_4
```

See `tools/poe2-assets/README.md` for details about required source files and
conversion behavior.

## Testing Focus

The test project covers the main domain surfaces used by the app:

- PoE1 and PoE2 tree loaders.
- PoE2 sprite map loading.
- Passive spec allocation and import application.
- PoE1 and PoE2 build import.
- Cluster jewel insertion and socketed jewel behavior.
- Jewel radius parsing and membership.
- App view model state transitions.

Run `dotnet test PathOfAvalonia.slnx` before making behavioral changes to tree
logic, import handling, or view model state.

## Status

PathOfAvalonia is under active development. The passive tree, import, equipment,
cluster jewel, and jewel radius foundations are present, but broader Path of
Building parity work is still in progress.
