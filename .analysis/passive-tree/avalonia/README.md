# Passive Tree — Avalonia Port Deep-Dive

Deeper, port-oriented analysis building on the Lua-level summaries in the parent folder (`../data.md`, `../logic.md`, `../maths.md`, `../ui.md`).

| File | Scope |
|---|---|
| [`build.md`](build.md) | Data pipeline: loading, parsing, graph construction, per-build state separation |
| [`assets.md`](assets.md) | Sprites / images / atlases and how to package + load them for .NET |
| [`selection.md`](selection.md) | Allocation & deallocation rules as a C# state machine |
| [`rendering.md`](rendering.md) | Per-frame draw loop mapped to Avalonia + SkiaSharp |
| [`architecture.md`](architecture.md) | Synthesis — module split, lifecycle, threading, testing |

**Reading order:** `build` → `assets` → `selection` → `rendering` → `architecture`.

The parent-folder files remain the Lua-centric map useful when reading PoB's original source.
