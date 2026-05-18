# PathOfBuilding — Feature Analysis

Analysis of `/home/crogeniks/dev/PathOfBuilding/`, organised by feature × layer. Source of truth for an Avalonia port.

## Features

| Folder | Scope |
|--------|-------|
| [`calculations/`](calculations/) | DPS / defence / ailment math pipeline |
| [`mod-system/`](mod-system/) | ModDB / ModList / ModStore + text parser |
| [`passive-tree/`](passive-tree/) | Tree model, path-finding, view, encoding — Avalonia-port deep-dive in [`passive-tree/avalonia/`](passive-tree/avalonia/) |
| [`items/`](items/) | Item parse / equip / mods / sets |
| [`skills/`](skills/) | Gems, supports, socket groups |
| [`build-management/`](build-management/) | Build lifecycle, XML schema, browser, undo |
| [`trade/`](trade/) | pathofexile.com/trade integration |
| [`ui-framework/`](ui-framework/) | Custom Lua control system |
| [`config/`](config/) | Per-build options / conditions / boss presets |
| [`jewels/`](jewels/) | Cluster / timeless / abyss / charm jewels |
| [`import-export/`](import-export/) | Account import, build codes, GGPK pipeline |
| [`data-layer/`](data-layer/) | Static `Data/` tables — inventory + schemas |
| [`runtime/`](runtime/) | Launch, headless, update |
| [`minions-party/`](minions-party/) | Minion actors, mirages, totems, party play |

## Layer Conventions

- `logic.md` — behaviour / state flow / algorithms at a system level.
- `maths.md` — concrete formulas (damage, radius, polar layout, etc.).
- `ui.md` — tab / control composition, interactions.
- `data.md` — persistent / static data shapes.
- `architecture.md` / `controls.md` — UI framework only.
- `parser.md` — mod-system only.
- `network.md` — trade only.
- `import.md` / `export-pipeline.md` / `build-code.md` — import-export.
- `bootstrap.md` / `headless.md` / `update.md` — runtime.
- `inventory.md` / `schemas.md` — data-layer.

## Reading Order for a Port

1. `runtime/bootstrap.md` — what the host must provide.
2. `ui-framework/` — widget contract.
3. `mod-system/` + `calculations/` — the actual engine.
4. `passive-tree/` → `items/` → `skills/` → `jewels/` — per-tab features.
5. `build-management/` + `import-export/` — persistence.
6. `config/`, `trade/`, `minions-party/` — ancillary features.
7. `data-layer/` — exists as an immutable input to all of the above.
