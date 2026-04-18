# Runtime — Headless Wrapper

`src/HeadlessWrapper.lua` lets PoB run without a graphics/input host — used by tests and programmatic build evaluation.

## How it Works

1. Stubs out every host primitive:
   - Drawing calls become no-ops.
   - `GetScreenSize()`, `GetVirtualScreenSize()` → `1, 1`.
   - `NewImageHandle()` → dummy object.
   - `DrawString*` size queries → `0`.
   - Input callbacks are never triggered.
   - `DownloadPage` still works (real HTTP), if network is available.
2. Sets a global `continuousIntegrationMode = true` so app code can branch on it (skip dialogs, skip asset loads, etc.).
3. `require "Launch"` — runs the normal entry flow.
4. Calls `launch:OnInit()` + a single `launch:OnFrame()`; the main object is swapped as usual.
5. Exposes `build`, `main`, etc. as module-level globals for test code to inspect.

## Use Cases

- Unit tests under `tests/` evaluate a build then assert on `build.calcsTab.mainOutput.TotalDPS`, etc.
- CLI scripts generate DPS comparison tables across item variations.
- Export pipeline scripts (some run under headless).
- CI validating mod parser coverage against a corpus of pasted items.

## Limitations

- UI-only behaviour (hover, animations, tooltips) is never exercised.
- Async features relying on `LaunchSubScript` may need synchronous fallbacks.
- Trade fetch works but without retry polling (caller must drive the event loop).
