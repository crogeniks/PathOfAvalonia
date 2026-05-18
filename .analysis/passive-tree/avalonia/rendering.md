# Passive Tree — Rendering System (Avalonia Port)

PoB's tree renderer is a per-frame, immediate-mode walk over every node and connector. Porting it to Avalonia means a custom `Control` that hands control down to Skia for the inner loop.

## 1. Frame Model

PoB: `PassiveTreeViewClass:Draw(build, viewPort, inputEvents)` runs every frame. No retained geometry, no dirty tracking — walks all nodes + connectors each frame, selects sprites per state, issues draw calls.

Measured cost: ~2500 nodes + ~4000 connectors × a few draws each = ~20–30k draw calls per frame. Batched by atlas, this runs at 60 FPS on modest hardware in PoB.

### Port target

Use a custom `Control` with `ICustomDrawOperation` to drop into Skia directly:

```csharp
public sealed class PassiveTreeView : Control {
    public static readonly StyledProperty<PassiveSpec?> SpecProperty = ...;
    public PassiveSpec? Spec { get => GetValue(...); set => SetValue(...); }

    public override void Render(DrawingContext ctx) {
        ctx.Custom(new TreeDrawOp(Bounds, ViewState, Spec));
    }
}

sealed class TreeDrawOp(Rect bounds, ViewState state, PassiveSpec spec) : ICustomDrawOperation {
    public bool HitTest(Point p) => bounds.Contains(p);
    public Rect Bounds => bounds;
    public void Render(ImmediateDrawingContext ctx) {
        using var lease = ctx.TryGetFeature<ISkiaSharpApiLeaseFeature>()!.Lease();
        var canvas = lease.SkCanvas;
        RenderTree(canvas);
    }
}
```

`InvalidateVisual()` is called by the view whenever `Spec` fires `SpecChanged`, when the cursor moves, or when zoom/pan changes. Jewel rings rotate continuously, so you also need a `DispatcherTimer` (or composition clock) running at 60 Hz while the view is visible.

## 2. Coordinate Transform

```
size    = min(max_x - min_x, max_y - min_y) * 1.1    // tree.size
scale   = min(viewport.W, viewport.H) / size * zoom
zoom    = 1.2 ^ zoomLevel                             // exponential
offsetX = panX + viewport.W/2
offsetY = panY + viewport.H/2
screen  = tree * scale + offset
tree    = (screen - offset) / scale
panX ∈ [-viewport.W * zoom * 2/3, +viewport.W * zoom * 2/3]
```

Source: `PassiveTreeView.lua:249–264`.

### Port

Apply the transform once as an `SKMatrix` at the top of `RenderTree`:

```csharp
var m = SKMatrix.CreateIdentity();
m = SKMatrix.CreateTranslation(offsetX, offsetY).PostConcat(m);
m = SKMatrix.CreateScale(scale, scale).PreConcat(m);
canvas.SetMatrix(m);
// Now draw everything in tree-space.
```

Inverse transform for mouse → tree is `canvas.TotalMatrix.Invert().MapPoint(cursor)` (or keep your own `Matrix3x2` and call `Invert`).

## 3. Draw Order

PoB uses explicit `SetDrawLayer(nil, N)` calls; Avalonia immediate-mode just draws in order. Preserve this sequence:

| # | Layer | Source |
|---|---|---|
| 1 | Tree background (class quadrants, big image) | `PassiveTreeView.lua:519–549` |
| 2 | Group backgrounds, ascendancy rings | `:610–628` |
| 3 | Connectors (all three states, in one pass — state selects the sprite) | `:691` (z=20) |
| 4 | Mastery active-effect glow | `:899` (z=15, but drawn before node sprite visually) |
| 5 | Node frame + icon | `:774, 827` (z=25) |
| 6 | Search-match highlight ring | `:958` (z=30) |
| 7 | Jewel socket radius rings (annulus + rotating shaded rings) | `:1050–1128` |
| 8 | Hover tooltip | `:985` (z=100) |

**No off-screen pre-pass, no retained vertex buffers.** This is simpler than it looks because `SKVertices` objects can be **cached** per frame, and Skia batches draws against the same atlas automatically.

## 4. Nodes

### 4.1 State selection — `PassiveTreeView.lua:762–847`

```
isAlloc = node.alloc
         || build.calcsTab.mainEnv.grantedPassives[nodeId]       // from items granting passives
         || (compareNode && compareNode.alloc)
state =
  isAlloc                                        ? "alloc"
  : hoverNode == node                            ? "alloc"     // hover previews target as lit
  : traceMode && node == tracePath[#tracePath]   ? "alloc"
  : hoverPath[node]                              ? "path"
  :                                                "unalloc"
```

### 4.2 Sprite picking — `PassiveTree.lua:387–438`

Each node precomputes `node.overlay = { alloc, path, unalloc, rsq }` where `rsq` is its click-radius-squared. The sprite key format:

```
{type}{state}              // "NotableFrameAllocated", "KeystoneFrameCanAllocate"
{type}{state}{Ascendancy?} // "NotableFrameAllocatedAscend" on ascendancy nodes
{type}{state}{Blighted?}   // "BlightedNotableFrameAllocated"
```

### 4.3 Render per node

```csharp
foreach (var node in tree.Nodes.Values) {
    if (node.IsProxy || node.Group?.IsProxy == true) continue;

    var (state, overlayKey) = ComputeState(node, ctx);
    var iconSprite  = sprites.Get(node.Icon, state == NodeState.Alloc ? "Active" : "Inactive");
    var frameSprite = sprites.Get(overlayKey, state);

    DrawSprite(canvas, iconSprite, node.Position, node.IconScale);
    DrawSprite(canvas, frameSprite, node.Position, node.FrameScale);
}
```

### 4.4 Hover hit-test — `PassiveTreeView.lua:277–305`

```
for each node with group, not proxy:
    dx, dy = cursor - node.position   (tree space)
    if dx*dx + dy*dy <= node.rsq: hover = node; break
```

One squared-distance test per node — ~3000 per frame, trivially fast. Do **not** rely on Avalonia hit-testing; keep the loop on your side.

## 5. Connectors

### 5.1 Pre-baked geometry

`PassiveTree.lua:866–969` pre-computes every connector during tree construction. Two kinds:

**Line** (cross-group / cross-orbit) — `:909–927`:

```
art     = assets.LineConnectorNormal
scale   = art.height * 1.33 / distance
n       = perpendicular unit vector
quad    = [n1 - n*scale,   n1 + n*scale,   n2 + n*scale,   n2 - n*scale]
UVs     = clamped so that endS = distance / art.textureWidth  (texture repeats along the line)
```

**Arc** (same-group, same-orbit) — `:929–969`:

```
arcAngle  = |node2.angle - node1.angle|
if arcAngle > 90°: split into two halves, second mirrored  (texture sprite only covers 90°)
clipAngle = π/4 - arcAngle/2
p         = 1 - max(tan(clipAngle), 0)
quad      = kite anchored at group centre; outer edge clipped by p
```

### 5.2 State → texture

```
if both endpoints allocated        → "Active"       (bright)
else if both in hoverPath          → "Intermediate" (highlighted preview)
else                               → "Normal"       (dim)
```

`PassiveTreeView.lua:634–645`.

### 5.3 Per-connector tint

Applied on top of the sprite (`SetDrawColor` before the draw):

| Situation | Tint |
|---|---|
| Both endpoints dependent on hover-deallocate target | `{1, 0.5, 0.5}` (warn red) |
| On Split-Personality path | `{0.2, 1, 0.2}` |
| Belongs to inactive ascendancy | `{0.75, 0.75, 0.75}` |

### 5.4 Port implementation

Four-vertex `SKVertices` per connector, UV-mapped, drawn with `SKCanvas.DrawVertices`:

```csharp
SKVertices BuildQuad(SKPoint p1, SKPoint p2, SKPoint p3, SKPoint p4,
                     SKPoint u1, SKPoint u2, SKPoint u3, SKPoint u4)
    => SKVertices.CreateCopy(
          SKVertexMode.TriangleFan,
          [p1, p2, p3, p4],
          [u1, u2, u3, u4],
          colors: null);
```

Pre-build connector quads at `TreeModel` construction and store them (they don't change). At render time only the **paint** changes (atlas sheet for the target state + colour tint).

## 6. Jewel Socket Overlays

`PassiveTreeView.lua:1050–1128`.

- Drawn only when the socket is hovered.
- Annulus (outer ring – inner ring) coloured per radius tier (`data.jewelRadius[idx].col`).
- Timeless jewels draw **two additional rotating sprites** per type:

| Jewel | Sprites | Rotation |
|---|---|---|
| Brutal Restraint | Maraketh1, Maraketh2 | ±0.7 rad/s (counter-rotating) |
| Elegant Hubris | Eternal1, Eternal2 | ±0.7 |
| Glorious Vanity | Vaal1, Vaal2 | ±0.7 |
| Lethal Pride | Karui1, Karui2 | ±0.7 |
| Militant Faith | Templar1, Templar2 | ±0.7 |
| Heroic Tragedy | Kalguur1, Kalguur2 | ±0.7 |

- Impossible Escape: shows on keystones in-radius, not on the socket itself.

### Rotation — `DrawImageRotated`, `:1130–1153`

```
if main.showAnimations == false: skip rotation
t     = GetTime() * 0.00003
rot   = angle * t                       // angle param sets speed/direction
v1..4 = centre + rotate([±w/2, ±h/2], rot)
DrawImageQuad(handle, v1..4, u1..v4)
```

### Port

```csharp
float t = stopwatch.ElapsedMilliseconds * 0.00003f;
float rot = angle * t;
canvas.Save();
canvas.Translate(centre.X, centre.Y);
canvas.RotateRadians(rot);
canvas.DrawImage(ring.Sheet.Image,
    srcRect: ring.PixelRect,
    dstRect: new SKRect(-halfW, -halfH, halfW, halfH));
canvas.Restore();
```

## 7. Search Highlight, Trace Path, Compare Diff

### Search

- Maintained outside the render loop: `searchStrResults` recomputed only when the search string changes (`:736–759`).
- At render time, matched nodes get an extra sprite ring drawn on top (z=30) with radius `175 * scale / zoom^0.4` (`:961`).

### Trace path (shift-held)

- `tracePath[]` is a list of node IDs the user has stepped through.
- Used as `hoverPath` for connector state computation → the trace visibly "lights up" connectors in the `Intermediate` style.

### Compare diff (`:147–168`)

- `GetCompareNodeColor(node)` returns one of:
  - `{0,1,0}` added (allocated only in compare)
  - `{1,0,0}` removed (allocated only in primary)
  - `{0,0,1}` modified (same alloc, different mastery effect / jewel socket)
  - neutral → no tint
- Applied as `SetDrawColor` before drawing the node frame.

## 8. Zoom-Dependent Detail

Sprite zoom tiers exist in the data (scales like 0.1246 / 0.2109 / 0.2972 / 0.3835 / 1.0) but PoB **picks one at load** (`data[0.3835] or data[1]`) — no runtime LOD switching. The GPU downsamples for low zoom.

For the port: same simplification. Mipmapped `SKImage` atlases handle zoom-out quality for free. Only bother with multi-tier LOD if profiling shows the atlas is memory-bound.

## 9. Tooltip

`:983–992, 1278–1599`.

- Shown on hover **unless Ctrl is held**.
- Contents (in order): node-type header, name, mastery-effect list, stat lines (colour-coded by parse status), reminder text, flavour text, stat diff, path distance, tips.
- Drawn at z=100 (topmost). Positioned to stay on-screen.

### Port

Use Avalonia's `ToolTip` for the **frame** (so it inherits styling and accessibility), but render the **contents** yourself via a `ContentControl` with a `DataTemplate`. The tooltip's data source is `spec.Node(hoverNode)`.

Don't use Avalonia's pointer-hover timing — the tree hover is "instant-on" (no delay) and switches on cursor move. You'll want to drive it from your own hit-test loop (§4.4) rather than the `IsPointerOver` / `ToolTip.ShowDelay` flow.

## 10. Primitive Set Needed

| PoB primitive | Avalonia + Skia |
|---|---|
| `DrawImage(h, x, y, w, h, u..v)` | `canvas.DrawImage(image, src, dst, paint)` |
| `DrawImageQuad(h, 4×{x,y}, 4×{u,v})` | `canvas.DrawVertices(TriangleFan, 4 verts, 4 UVs, paint)` |
| `SetDrawColor(r, g, b, a)` | `paint.Color` for text / solid; `paint.ColorFilter = SKColorFilter.CreateBlendMode(c, SKBlendMode.Modulate)` for texture tint |
| `SetDrawLayer(nil, L)` | draw in order |
| `GetTime()` | `Stopwatch` on the control |
| `GetCursorPos()` | Avalonia `PointerEventArgs.GetPosition(this)` |
| `IsKeyDown(k)` | Avalonia `Key*` + own modifier state |
| `DrawStringWidth(size, font, s)` | `SKFont.MeasureText` |

Everything else (text layout, input focus, scrolling) is off the critical path.

## 11. Performance Notes

- **Batch by atlas.** Draw all "Normal"-state connectors in one pass (one Paint, one Image), then all "Intermediate", then "Active". Same for nodes.
- **Cache `SKVertices`.** Connector geometry never changes; build once, reuse forever.
- **Only invalidate on actual state change.** Hover changes invalidate; zoom/pan invalidate; otherwise the view is static. Don't run at 60 FPS for a static tree — jewel ring rotation is the only reason to animate.
- **Avoid per-frame GC.** No `new SKRect(...)` in hot loops — reuse structs or pass `in` parameters.
- **Hit-test loop allocates nothing.** Keep the nodes array as an `ImmutableArray<Node>` or plain `Node[]` iterated with a `for` loop.

Budget target: <4 ms per frame on a 2019 laptop for a 2500-node tree. That leaves plenty of headroom for tooltip layout and Avalonia's compositor.

## 12. Animations

Only one: **jewel ring rotation**, driven by `GetTime()`. Gated by `main.showAnimations`. No tweening, no hover pulse, no allocation flash. Allocation state is a hard swap of sprite.

The port should match this — adding transitions later is cheap, but gratuitous animation hurts precision (users are clicking 1-pixel targets).

## 13. Source Map

| Concern | File : lines |
|---|---|
| `Draw` entry | `PassiveTreeView.lua:170` |
| Zoom / pan / scale | `PassiveTreeView.lua:59, 73–78, 249–264` |
| Hit test | `PassiveTreeView.lua:277–305` |
| Node state + sprite pick | `PassiveTreeView.lua:762–847`, `PassiveTree.lua:387–438` |
| Connector geometry | `PassiveTree.lua:866–969` |
| Connector state / tint | `PassiveTreeView.lua:634–686` |
| Jewel ring overlay | `PassiveTreeView.lua:1050–1128` |
| Ring rotation | `PassiveTreeView.lua:1130–1153` |
| Search ring | `PassiveTreeView.lua:736–759, 956–981` |
| Compare diff colour | `PassiveTreeView.lua:147–168` |
| Tooltip | `PassiveTreeView.lua:985, 1278–1599` |
