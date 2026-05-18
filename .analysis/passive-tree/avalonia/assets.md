# Passive Tree — Assets & Sprite Pipeline (Avalonia Port)

Where every pixel on the tree comes from, and how to package / load it in .NET.

## 1. Image Categories

| Category | Source | State variants | Notes |
|---|---|---|---|
| **Node frames** (outer rings around nodes) | Atlas sheets in `TreeData/{v}/sprites.lua` | Unallocated / CanAllocate / Allocated | Pre-rendered — no runtime tinting |
| **Node icons** (skill art inside the frame) | Atlas sheets `skills-{n}.jpg` (CDN) | Normal / Active (allocated look) / Inactive | Thousands of distinct 26×26–64×64 icons |
| **Mastery icons** | Atlas; per-effect `activeIcon` + `inactiveIcon` | Selected / unselected | Plus an animated "active effect" overlay |
| **Connectors** (lines/arcs between nodes) | Atlas | Normal / Intermediate / Active | Intermediate = "hover path preview" |
| **Backgrounds** | Large single images per attribute quadrant (`BackgroundStr`, `BackgroundDex`, ...) + class overlays | Static | Mostly JPG |
| **Ascendancy backgrounds** | Per-class ring sprites (`ClassesMarauder`, ...) | Static | Rendered under ascendancy group |
| **Jewel radius rings** | `PassiveSkillScreen*.png` (Eternal, Karui, Maraketh, Templar, Vaal, Kalguur pairs) + `ShadedOuter/InnerRing` | Per-jewel-type | Animated (rotation only) |
| **Decorations** | `Assets/ring.png`, `Assets/small_ring.png`, `PSLineDeco*` | Static | UI chrome |
| **Ascendancy portraits** | `Assets/ascendants/*.jpeg` | Static | Used in spec dropdown, not on tree |

Everything is **pre-rendered** — no runtime tinting, no generative sprites. State differences are always a different sprite key.

## 2. Sprite Sheet / Atlas Schema

`TreeData/{v}/sprites.lua` (or `.json`) is a nested table of **sheet → type → coords**. PoB normalises it into a lookup `spriteMap[name][type] = {handle, w, h, u0, v0, u1, v1}` (`PassiveTree.lua:261–290`).

### Raw shape (3.25 example)

```lua
["normalActive"] = {
    filename = "https://web.poecdn.com/image/passive-skill/skills-3.jpg?8ccec72b",
    w = 962, h = 1370,
    coords = {
        ["Art/2DArt/SkillIcons/passives/2handeddamage.png"] = { x=0,  y=0,  w=26, h=26 },
        ["Art/2DArt/SkillIcons/passives/dualwield.png"]      = { x=26, y=0,  w=26, h=26 },
        ...
    }
}
```

### After PoB normalisation

```
spriteMap["Art/2DArt/.../dualwield.png"]["normalActive"] = {
    handle = <atlas texture>,
    width  = 26, height = 26,
    u0 = 26/962, v0 = 0/1370,
    u1 = 52/962, v1 = 26/1370,
}
```

A single atlas holds hundreds of icons; a sprite is identified by **(logical path, state)**.

## 3. Per-Zoom-Level Assets

Assets are provided at multiple pre-rendered scales, keyed by scale factor — e.g. `0.1246`, `0.2109`, `0.2972`, `0.3835`, `1.0`. PoB picks `data[0.3835]` as the default and falls back to `data[1]` if missing (`PassiveTree.lua:263–269`).

In practice PoB loads **one copy** at the best available quality and lets the GPU downsample via the viewport transform. Keep this simplification — there is no LOD switching during scroll. Only use multiple zoom tiers if profiling shows memory pressure on low-end GPUs.

## 4. Where Files Live

### Shipped in the PoB repo

```
src/Assets/                               ← static UI chrome (~2 MB PNG/JPEG)
  ring.png, small_ring.png, ShadedOuterRing.png, ...
  ascendants/*.jpeg

src/TreeData/{version}/
  data.json                               ← ~1 MB
  sprites.json                            ← ~50 KB
  Assets.json                             ← scale-keyed asset URL table
  PassiveSkillScreen*.png                 ← jewel-ring pairs (~3–5 MB/version)
```

### Downloaded lazily (if `main.allowTreeDownload`)

The **big atlas JPEGs** — `skills-N.jpg`, `group-background-N.jpg`, etc. URLs are baked into `sprites.json` / `Assets.json` as fully-qualified `https://web.poecdn.com/image/passive-skill/...?{hash}` strings.

`LoadImage(url)` at `PassiveTree.lua:839–863`:
1. Check `TreeData/{v}/{localName}` on disk.
2. Check `TreeData/{localName}` (version-agnostic cache).
3. If `allowTreeDownload` and not cached: HTTP fetch → write to disk.
4. Silent fallback to blank on failure.

### File formats in the wild

| Extension | Where | Note |
|---|---|---|
| `.png` | `src/Assets/`, some jewel rings | Alpha matters (radius overlays) |
| `.jpg` / `.jpeg` | Most CDN atlases, backgrounds, portraits | No alpha; backgrounds |

## 5. Loading Mechanism in Lua

Host-provided primitive:

```lua
handle = NewImageHandle()
handle:Load(pathOrUrl, "CLAMP", "ASYNC", "MIPMAP"?)
w, h = handle:ImageSize()
```

Render-time draw call:

```lua
DrawImage(handle, x, y, w, h, u0, v0, u1, v1)
DrawImageQuad(handle, x1,y1, x2,y2, x3,y3, x4,y4, u1..v4)
```

Everything else (atlas unpacking, UV maths) lives in Lua. The host is just **"blit a rectangle of this texture onto this quad."**

## 6. Mapping to Avalonia

### 6.1 Target stack

Draw the tree on a custom `Control` that overrides `Render(DrawingContext)` using a `CustomDrawOp : ICustomDrawOperation` — this lets you use `SkiaSharp` directly for per-frame primitives. Avalonia's built-in `DrawingContext` is too high-level for the volume of quads here.

```csharp
public sealed class PassiveTreeView : Control {
    public override void Render(DrawingContext ctx) {
        ctx.Custom(new TreeDrawOp(this /* pass state */));
    }
}

sealed class TreeDrawOp : ICustomDrawOperation {
    public void Render(ImmediateDrawingContext ctx) {
        var lease = ctx.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
        var canvas = lease.SkCanvas;
        // custom Skia pass
    }
}
```

### 6.2 Atlas loading

```csharp
public sealed record AtlasSheet(SKImage Image, int Width, int Height);
public sealed record SpriteRef(AtlasSheet Sheet, SKRect Uv, SKSize PixelSize);

public sealed class SpriteRegistry {
    readonly Dictionary<AtlasKey, AtlasSheet> _sheets = new();
    readonly Dictionary<(string logicalPath, NodeState state), SpriteRef> _sprites = new();

    public SpriteRef Get(string logicalPath, NodeState state) => _sprites[(logicalPath, state)];
}
```

- Load each atlas once into an `SKImage` (GPU-resident).
- Build `_sprites` from `sprites.lua` → UV rectangles computed at load time.
- Key sprites by `(logical-path, state)` exactly as PoB does.

### 6.3 Draw primitives

| PoB (Lua) | Avalonia + Skia equivalent |
|---|---|
| `DrawImage(h, x, y, w, h, u0, v0, u1, v1)` | `SKCanvas.DrawImage(image, srcRect, dstRect, paint)` |
| `DrawImageQuad(h, x1..y4, u1..v4)` | No direct Skia equivalent. Use `SKCanvas.DrawVertices(SKVertexMode.TriangleFan, verts, uvs, null, paint)` with a 4-vertex fan. This is what you need for arc connectors whose quads aren't axis-aligned. |
| `SetDrawColor(r,g,b,a)` | `SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(...) }` for tinting; `SKPaint.Color` for pure colour fills |
| `SetDrawLayer(nil, layer)` | You're in immediate mode — just call draws in layer order. Don't need to emulate the layer number. |
| `GetTime()` | `Stopwatch` started on control attach, read in `Render` |

### 6.4 File format support

Avalonia/Skia reads **PNG, JPEG, GIF, WebP, BMP** out of the box via `SKImage.FromEncodedData`. No other decoders needed.

## 7. What to Ship with the Port

### Bundled (embedded resources)

- `src/Assets/*` — small, stable, shared across versions (~2 MB).
- Latest `TreeData/{Latest}/` — `data.json`, `sprites.json`, `Assets.json`, `PassiveSkillScreen*.png` (~8–15 MB).

### Downloaded on demand (cached under `%AppData%/PathOfAvalonia/`)

- CDN atlases referenced by `sprites.json` (`skills-N.jpg`, etc.) — several MB per version.
- Non-latest tree versions when a build referencing one is opened.

### Download flow

```csharp
async Task<Stream> LoadAtlasAsync(Uri cdnUrl, CancellationToken ct) {
    var cachePath = CachePathFor(cdnUrl);
    if (File.Exists(cachePath)) return File.OpenRead(cachePath);

    using var http = new HttpClient();
    await using var net = await http.GetStreamAsync(cdnUrl, ct);
    await using var tmp = File.Create(cachePath + ".part");
    await net.CopyToAsync(tmp, ct);
    File.Move(cachePath + ".part", cachePath);
    return File.OpenRead(cachePath);
}
```

Cache by **URL hash + etag** — PoE bumps the `?{hash}` query string when an asset changes.

## 8. GPU Residency

A full tree view keeps ~20 atlases resident (frames, icons, connectors, backgrounds). At ~1–3 MB each decoded that's 30–60 MB VRAM — fine. Don't evict on zoom/pan; only evict when the user switches tree version. Hold atlases in `SKImage` (GPU-side) rather than `SKBitmap` (CPU-side).

## 9. What About Tinting / Colour Overlays?

PoB overlays colours in three places:

1. **Ascendancy fade** — non-active ascendancy drawn at `(0.75, 0.75, 0.75)` tint (`PassiveTreeView.lua:680`).
2. **Compare-mode diff** — added/removed/changed colour-coded (`:147–168`).
3. **Heatmap power view** — per-node gradient tint.

All are simple RGB multiplications, implemented with `SetDrawColor` before the draw. In Avalonia: set `SKPaint.ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.Modulate)` or `SKPaint.ColorF = new SKColorF(r, g, b, 1)` if using a shader-based path. No special case needed.

## 10. Quick Checklist for the Port

- [ ] `SpriteRegistry` service per tree version — loads atlases once, exposes `Get(path, state)`.
- [ ] `AtlasLoader` with cache + lazy CDN download.
- [ ] `PassiveTreeView : Control` with `ICustomDrawOperation` → Skia direct access.
- [ ] Build `SKVertices` helper for arbitrary quads (arc connectors).
- [ ] Embed latest tree + `Assets/` as resources; older versions + CDN atlases cached on disk.

## 11. Source Map

| Concern | File : lines |
|---|---|
| Atlas normalisation | `Classes/PassiveTree.lua:261–290` |
| Zoom-level selection | `Classes/PassiveTree.lua:263–269` |
| `LoadImage` + caching | `Classes/PassiveTree.lua:839–863` |
| Draw-time sprite use | `Classes/PassiveTreeView.lua:519–549`, `905–912` |
| Bundled static assets | `src/Assets/` |
| Per-version assets | `src/TreeData/{v}/` |
