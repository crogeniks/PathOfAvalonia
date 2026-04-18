# Passive Tree — Avalonia Port Architecture

Synthesis doc: how the four deep-dives (`build`, `assets`, `selection`, `rendering`) combine into a coherent C# module.

## 1. High-Level Layering

```
┌─────────────────────────────────────────────────────────────┐
│  Avalonia View Layer                                        │
│   PassiveTreeView : Control                                 │
│    └── TreeDrawOp : ICustomDrawOperation  (Skia immediate)  │
│    └── TooltipPresenter : ContentControl                    │
│    └── PassiveTreeViewModel (zoom, pan, search, hover)      │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ binds
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Domain Layer                                               │
│   PassiveSpec          (mutable, per-build)                 │
│    └── Alloc / Dealloc / SelectClass / EquipJewel           │
│    └── Subgraphs, MasterySelections, AllocatedNodes         │
│   TreeModel            (immutable, per-version)             │
│    └── Nodes, Groups, Connectors, Classes, LookupMaps       │
│    └── Loaded once per version, cached in TreeRegistry      │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ loads
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Asset / Data Layer                                         │
│   TreeRegistry        → TreeModel per version               │
│   SpriteRegistry      → SKImage atlases + UV lookup         │
│   AtlasLoader         → CDN fetch + disk cache              │
│   TreeDataLoader      → JSON → DTO → normalise → TreeModel  │
└─────────────────────────────────────────────────────────────┘
```

Three boundaries worth enforcing:

1. **View ↔ Domain:** one-way binding plus `PassiveSpec.SpecChanged` → `InvalidateVisual()`. The view never mutates the spec except via explicit commands (click, shift-click, etc.).
2. **Domain ↔ Asset:** the domain only exposes logical sprite identifiers (`node.Icon`, `node.OverlayKey`); the view resolves them through `SpriteRegistry`. Lets you swap asset backends (e.g. test-time stubs).
3. **Asset ↔ Data:** `TreeModel` and `SpriteRegistry` are **completely immutable** once constructed. Hot-reloading a new version is "discard and rebuild."

## 2. Type Catalogue

### Domain

```csharp
public sealed record TreeVersion(string Id, double Num, string Display, Uri? CdnRoot);

public sealed record TreeModel(
    TreeVersion Version,
    IReadOnlyDictionary<int, Node> Nodes,
    IReadOnlyDictionary<int, Group> Groups,
    IReadOnlyList<Class> Classes,
    IReadOnlyDictionary<string, Node> KeystoneMap,   // OrdinalIgnoreCase
    IReadOnlyDictionary<string, Node> NotableMap,
    IReadOnlyDictionary<string, Node> AscendancyMap,
    IReadOnlyDictionary<int, MasteryEffect> MasteryEffects,
    IReadOnlyList<Connector> Connectors,              // pre-baked vertex geometry
    Rect Bounds,
    float Size,
    float[] OrbitRadii,
    float[][] OrbitAngles);

public sealed class Node {        // class, not record — has back-pointers
    public int Id;
    public string Name;
    public NodeType Type;         // Normal, Notable, Keystone, Socket, Mastery, ClassStart, AscendClassStart
    public Vector2 Position;      // tree-space
    public Group Group;
    public IReadOnlyList<Node> LinkedNodes;
    public string Icon;           // logical atlas path
    public Overlay Overlay;       // { AllocKey, PathKey, UnallocKey, HitRadiusSq }
    public IReadOnlyList<MasteryEffect> MasteryEffects;    // mastery nodes only
    public ModList ModList;       // parsed at construction
    public string? AscendancyName;
    public bool IsJewelSocket, IsKeystone, IsNotable, IsMastery, IsProxy;
}

public sealed class PassiveSpec {
    public TreeModel Tree { get; }
    public HashSet<int> AllocatedNodes { get; } = new();
    public Dictionary<int, Subgraph> Subgraphs { get; } = new();
    public Dictionary<int, int> MasterySelections { get; } = new();
    public int ClassId, AscendancyId, SecondaryAscendancyId;

    // Derived (rebuilt after every mutation):
    public IReadOnlyDictionary<int, IReadOnlyList<int>> Paths { get; private set; }
    public IReadOnlyDictionary<int, IReadOnlySet<int>> Dependencies { get; private set; }

    public event Action? SpecChanged;
    // ... Allocate, Deallocate, SelectClass, etc.
}
```

### Assets

```csharp
public sealed record SpriteRef(AtlasSheet Sheet, SKRect UvRect, SKSize PixelSize);

public sealed class SpriteRegistry {
    readonly Dictionary<AtlasKey, AtlasSheet> _sheets = new();
    readonly Dictionary<(string logicalPath, string state), SpriteRef> _sprites = new();
    public SpriteRef Get(string logicalPath, string state);
}
```

### View

```csharp
public sealed class PassiveTreeView : Control { /* Spec, ViewState, Render override */ }

public sealed class PassiveTreeViewModel : INotifyPropertyChanged {
    public int ZoomLevel;
    public float PanX, PanY;
    public int? HoverNodeId;
    public List<int> TracePath;
    public string SearchText;
    public HashSet<int> SearchMatches;        // computed off-loop when SearchText changes
}
```

## 3. Lifecycle Narrative

1. App starts → `TreeRegistry` eagerly loads the latest tree as `TreeModel` on a background task while the shell UI paints.
2. User opens a build → `Build` instantiates a `PassiveSpec(tree)`. `PassiveSpec` initial state = only the class start allocated.
3. User clicks **Tree tab** → `PassiveTreeView` is materialised. It receives `Spec` via binding and subscribes to `SpecChanged`.
4. Each click in the view:
   - Hit-test → node id.
   - Dispatch to `Spec.Allocate(id)` / `Spec.Deallocate(id)` / mastery flow.
   - `Spec` rebuilds paths + dependencies (~1 ms on 2500-node tree).
   - `SpecChanged` fires → view calls `InvalidateVisual()`.
5. User switches tree version (rare) → new `TreeModel` loaded, current `PassiveSpec` migrated (nodes that still exist carry over; missing ones are dropped; class/ascendancy re-validated).

## 4. Threading Model

| Thread | What runs there |
|---|---|
| UI / dispatcher | `PassiveSpec` mutations, render loop, hit-testing, tooltip |
| Background | `TreeDataLoader` (JSON parse), `AtlasLoader` (HTTP fetch, `SKImage.FromEncodedData`), search index build on `SearchText` change |

Keep `PassiveSpec` strictly single-threaded. Calculations read `PassiveSpec` but do so **after** render has finished — use a snapshot (e.g. an `ImmutableHashSet<int>` of allocated ids produced at the end of each mutation) for cross-thread reads.

## 5. Mutation Pipeline

```
Click event
  → ViewModel.HandleClick(nodeId, modifiers)
    → spec.Allocate / Deallocate / SelectMasteryEffect / ...
      → RebuildPathsAndDependencies (internal, O(V+E))
      → SpecChanged fires
        → ViewModel.Snapshot = spec.SnapshotAllocated()    (ImmutableHashSet)
        → View.InvalidateVisual()
```

Every mutation is synchronous. Calculations and exports consume `ViewModel.Snapshot`, never the live `HashSet`.

## 6. Render Pipeline (Per Frame)

```
TreeDrawOp.Render(ctx):
  lease Skia → canvas
  1. Compute transform matrix (zoom, pan) → canvas.SetMatrix
  2. Draw background + group backgrounds
  3. Draw connectors:
        for each pre-baked Connector vertices:
           paint.Image = atlas for state (Normal/Intermediate/Active)
           paint.ColorFilter = tint
           canvas.DrawVertices(vertices, paint)
  4. Draw node icons  (atlas batched)
  5. Draw node frames (atlas batched)
  6. Draw search highlights (only for matched nodes)
  7. Draw jewel socket overlays (only if hover socket)
  8. (Tooltip is a separate Avalonia control, not drawn here)
```

Group connectors by atlas before drawing: the loop becomes ~6 batched `DrawVertices` calls instead of thousands of individual ones.

## 7. Module Split (Project Structure)

```
src/
  PathOfAvalonia.TreeDomain/          ← pure, no UI deps
    TreeModel.cs, Node.cs, Group.cs, Connector.cs, Class.cs
    PassiveSpec.cs, PassiveSpec.PathFinding.cs, PassiveSpec.Dependencies.cs
    Subgraph.cs, ClusterJewelBuilder.cs

  PathOfAvalonia.TreeData/            ← loaders, DTOs
    TreeDataLoader.cs, TreeDto.cs, TreeMigrator.cs
    TreeRegistry.cs
    SpritesDto.cs, SpriteRegistry.cs, AtlasLoader.cs

  PathOfAvalonia.Tree.Avalonia/       ← UI layer
    PassiveTreeView.axaml(.cs)
    TreeDrawOp.cs
    PassiveTreeViewModel.cs
    TooltipPresenter.axaml(.cs)
    InputRouter.cs

  PathOfAvalonia.Tree.Tests/
    GraphTests.cs                     ← BFS, dependency cascades
    AllocationRulesTests.cs           ← R1..R6, invariants
    ClusterJewelTests.cs
    PersistenceTests.cs               ← URL encode/decode matches PoB
```

`TreeDomain` and `TreeData` have **no reference to Avalonia or Skia** — they're pure .NET and fully unit-testable.

## 8. Test Strategy

1. **Golden trees.** Load PoB's `tree.lua` into both PoB (as reference) and the port. Assert node/group/connector counts match exactly, and node positions match within float epsilon.
2. **Known builds.** Import a handful of pobb.in builds, assert `AllocatedNodes` and `MasterySelections` match the expected set after decode.
3. **BFS oracle.** For each node, compute its path distance with both implementations; must match.
4. **Dependency invariant.** Property-based test: after any sequence of random `Allocate`/`Deallocate`, every allocated node must be reachable from the class start.
5. **Round-trip.** Encode → decode → encode must be idempotent.

## 9. Versioning

- `TreeVersion` record, one entry per supported tree version — all share the same modern JSON schema, so deserialisation is uniform (no per-version shims).
- `TreeRegistry` lazily loads each requested version as an independent frozen `TreeModel`.
- Cross-version **spec migration** (`PassiveSpec.MigrateTo(TreeModel newTree)`) is intentionally dumb: keep allocations whose node id still exists in the new tree, drop the rest, revalidate class/ascendancy. No attempt is made to "heal" the allocation set — the user reviews it after migration.

## 10. Performance Envelope

Measured PoB targets, portable to .NET:

| Operation | Target |
|---|---|
| Cold load (JSON + graph + mod parse) | < 400 ms (background) |
| Allocate / Deallocate round-trip | < 2 ms (main thread) |
| Render full tree | < 4 ms/frame |
| Search index build (name + stat match) | < 50 ms |
| URL encode / decode | < 5 ms |

Any of these getting >2× slower is a red flag; profile before shipping.

## 11. What to Deliberately **Not** Port

- **Lua mod-parser regex engine** → rewrite against a cleaner grammar. Port the data (list of recognised mod patterns) but not the control flow.
- **PoB's immediate-mode UI framework** → use Avalonia controls for everything except the tree canvas itself. Don't emulate `Control.lua`.
- **PoB's update/download machinery** → .NET has `ClickOnce`, `Velopack`, and auto-updater libraries; use one.
- **`setmetatable` prototype chains** → replace with `NodeView` struct projections (see `build.md §5`).
- **Pre-modern-schema shims** → `PassiveTree.lua:95–172, 464–479` migrates old tree formats to the current one. The port only ingests the modern schema, so none of this applies.

## 12. Open Questions for the Port

These are decisions the existing PoB source doesn't answer for you — flag them early:

1. **Bundled tree versions vs. download?** Recommendation: bundle latest, download older on demand.
2. **Offline-first vs. online-first?** PoB is offline-first with optional downloads. Match that — no network calls on the render path.
3. **Compare mode UX?** PoB overlays both specs on one canvas. Worth considering a side-by-side view for readability — but keep the overlay as default for parity.
4. **Touch / trackpad gestures?** Avalonia gets pinch-zoom for free on touch devices; PoB doesn't have it. Opportunity to improve.

## 13. Reading Order

1. `build.md` — data model and graph construction.
2. `assets.md` — sprite pipeline and file layout.
3. `selection.md` — state machine for allocation.
4. `rendering.md` — per-frame draw loop.
5. This doc — how the pieces fit together.

Older summary-level files (`data.md`, `logic.md`, `maths.md`, `ui.md`) remain as a Lua-centric map for anyone reading PoB source directly.
