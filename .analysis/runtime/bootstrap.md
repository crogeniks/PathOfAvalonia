# Runtime — Bootstrap

## Launch Sequence

```
host binary (SimpleGraphic / PoB engine)
  loads  Launch.lua
    → SetMainObject(launch)
    → launch:OnInit()
      → require("Modules/Main")
      → SetMainObject(main)
      → main:Init()
        → load tree data (TreeData/<ver>/tree.lua)
        → load mod cache (Data/ModCache.lua)
        → load user build path, settings
        → LoadModule chain (Build, BuildList, Config, Skills, Items, …)
    → ready; main:OnFrame() runs each frame
```

## Host Primitives

The native runtime registers these callbacks / APIs on the main object:

### Lifecycle / callbacks

- `OnInit()`, `OnExit()`, `OnFrame()`.
- `OnKeyDown(key, doubleClick)`, `OnKeyUp(key)`, `OnChar(char)`.
- `OnSubCall(name, ...)`, `OnSubFinished(...)` — for async subscripts.
- `CanExit()`.

### Drawing

- `NewImageHandle(filename)` → handle.
- `handle:Load(filename)`, `handle:IsValid()`, `handle:ImageSize()`.
- `SetDrawLayer(layer, sub)`, `SetDrawColor(r,g,b,a)`, `SetViewport(x,y,w,h)`, `SetBlendMode(mode)`.
- `DrawImage(handle, x, y, w, h, u0, v0, u1, v1)`.
- `DrawImageQuad(handle, x1,y1,x2,y2,x3,y3,x4,y4, u1,v1,...u4,v4)`.
- `DrawString(x, y, align, size, font, text)`.
- `DrawStringWidth(size, font, text)`, `DrawStringCursorIndex(...)`.

### Window / input

- `SetWindowTitle(title)`, `GetCursorPos()`, `IsKeyDown(key)`.
- `GetScreenSize()`, `GetVirtualScreenSize()`.
- `SetProfiling(enable)`, `GetTime()`.

### File / net

- `MakeDir`, `RemoveDir`, `DeleteFile`, `Copy`, `RenamePath`, `FileExists`, `GetFolderList`, `GetFileList`.
- `OpenURL(url)`.
- `DownloadPage(url, headers, body, callback)` — async HTTP.
- `LaunchSubScript(script, args) → threadHandle` + `AbortSubScript`.

### Misc

- `ConExecute(cmd)`, `ConPrint(text)` — console / debug.
- `GetRuntimePath()` — install dir.
- `GetScriptPath()` — script dir.

## Main Object Contract

A Lua table set via `SetMainObject(t)`. Host invokes methods on it every frame and on input events. The current main:

- `launch` (boot phase) — shows "Loading…" + performs UpdateCheck + swaps to `main` once initialised.
- `main` (app phase) — orchestrates `mode = "BUILD" | "LIST"`.

Each mode is a class (`Build`, `BuildList`) implementing its own `:Draw()`, `:OnKeyDown()`, `:OnChar()`.

## Frame Pipeline

1. `main:OnFrame()` (CLS viewport, compute viewport size).
2. `currentMode:OnFrame()` (event processing + recalc when `modFlag` set).
3. `currentMode:Draw(viewPort)` → nested `ControlHost:DrawControls()`.
4. Tooltips drawn on a higher layer.
