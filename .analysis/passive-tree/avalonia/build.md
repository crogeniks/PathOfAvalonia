# Passive Tree — Build Pipeline (Avalonia Port)

How raw tree data becomes the in-memory graph the UI and calculations consume. This doc pairs the Lua source with concrete C#/Avalonia guidance.

## 1. Entry Point & Caching

**PoB (Lua)** — `Modules/Main.lua:314–325`

```
main:LoadTree(version)
  → if main.tree[version] exists, return it
  → else: new("PassiveTree", version) → cache on main.tree[version]
```

Tree is **immutable and cached per version**. One `PassiveTree` instance is shared across every build that uses that version.

**`PassiveTree.new(version)`** — `Classes/PassiveTree.lua:52–91`

1. Try `TreeData/{v}/tree.lua` (pre-compiled Lua table).
2. Fallback: `TreeData/{v}/data.json` → convert via `jsonToLua()` → write `tree.lua` cache.
3. (Disabled) fallback: HTTP fetch if `main.allowTreeDownload`.
4. `loadstring(text)()` → merge fields into `self`.

### Port mapping

| PoB concept | C# / Avalonia |
|---|---|
| Global `main.tree[version]` | `static readonly ConcurrentDictionary<TreeVersion, TreeModel>` on a `TreeRegistry` service |
| `loadstring(treeText)()` | `System.Text.Json` deserialiser straight from `data.json`; the `.lua`-to-JSON dance is not needed |
| Lazy new per version | `TreeRegistry.GetOrLoad(version)` — `Lazy<Task<TreeModel>>` so the first build pays the cost on a background thread |
| Rebuild triggers | **None** — `TreeModel` is frozen after construction. All per-build mutation lives on `PassiveSpec`. |

**Recommendation:** drop the `.lua` intermediate entirely. Ship `data.json` (or a binary-serialised .NET snapshot — see §7) per version and deserialise once at startup or first use.

## 2. Raw Input Shape

Per-version files live at `src/TreeData/{version}/`:

| File | Purpose |
|---|---|
| `tree.lua` | PoB-converted tree (Lua-table syntax) — we go straight from JSON |
| `data.json` | PoE official JSON — **primary source for the port** |
| `sprites.json` | Atlas metadata |
| `Assets.json` | Scale-keyed asset URLs |
| `PassiveSkillScreen*.png` | Jewel-radius rings |

### Root keys

| Key | Type | Notes |
|---|---|---|
| `classes` | `Class[]` | One entry per character class |
| `groups` | `Group[]` | Node cluster containers — x/y centre + orbits |
| `nodes` | `Node[]` sparse by id | All passives (root, skill, socket, mastery, cluster proxies) |
| `constants` | `{ skillsPerOrbit, orbitRadii }` | Orbit geometry |
| `alternate_ascendancies` | | Secondary ascendancy class |
| `imageZoomLevels` | `number[]` | Asset scale tiers |
| `max_x / min_x / max_y / min_y` | `number` | Tree bounding box |

### Node record (canonical modern shape)

```jsonc
{
  "skill": 5865,               // id
  "name": "Physical Damage...",
  "icon": "Art/2DArt/SkillIcons/passives/.../DmgLeech.png",
  "group": 1, "orbit": 2, "orbitIndex": 5,
  "stats": ["30% increased ...", "10% increased ..."],
  "out":  ["9271"],
  "in":   ["29294"],
  "isNotable": false, "isKeystone": false,
  "isJewelSocket": false, "isMastery": false,
  "ascendancyName": "Berserker", "isAscendancyStart": false,
  "masteryEffects": [{ "effect": "...", "stats": [...] }]
}
```

**Port strategy:** write a single `NodeDto` record mapped directly to the current JSON shape with `[JsonPropertyName]`. Normalise once during load.

## 3. Node Graph Construction

All construction happens inside `PassiveTree:new()`. The ordering matters — orbit tables must be ready before node positions are computed.

### 3.1 Class / Ascendancy resolution — `PassiveTree.lua:95–190`

- Build `classNameMap : name → classId`
- Build `ascendNameMap : ascName → { classId, ascendClass, flavourText }`
- Populate `alternateAscendancies` if present in the tree version

### 3.2 Orbit tables — `PassiveTree.lua:192–197`, `CalcOrbitAngles` at 971–992

```
skillsPerOrbit = [1, 6, 12, 12, 40]
orbitRadii     = [0, 82, 162, 335, 493]
orbitAnglesByOrbit[orbitIdx][nodeIdx] = radians
  Orbit 2 (12): non-uniform 30°/45° pattern
  Orbit 4 (40): non-uniform 10°/45° pattern
  Others: uniform 2π / skillsPerOrbit
```

### 3.3 Node classification — `PassiveTree.lua:462–548`

Iterates `nodes`, mutates each to:

1. Tag type (`ClassStart` | `AscendClassStart` | `Mastery` | `Socket` | `Keystone` | `Notable` | `Normal`).
2. For `Mastery`: enumerate `masteryEffects` into `self.masteryEffects[effectId]`.
3. Insert into lookup maps (`keystoneMap`, `notableMap`, `ascendancyMap`, `sockets`). Keys stored lowercased for case-insensitive lookup.
4. Resolve `node.group` pointer; groupless nodes (cluster jewel proxies) go into `clusterNodeMap`.

### 3.4 Connection inference — `PassiveTree.lua:565–592`

- `node.linkedId[]` built by unioning `out[]` and `in[]` of the raw record.
- Skip connections that cross ascendancy boundaries or touch proxy nodes.
- For each surviving edge, call `BuildConnector()` to pre-compute rendering geometry (see `rendering.md`).

### 3.5 Polar → Cartesian — `PassiveTree.lua:805–833`

```
angle  = orbitAnglesByOrbit[node.o + 1][node.oidx + 1]
radius = orbitRadii[node.o + 1]
node.x = group.x + sin(angle) * radius
node.y = group.y - cos(angle) * radius
```

Coordinates stored in **tree-space** (raw PoE units); viewport scales them at render time.

### 3.6 Jewel radius pre-compute — `PassiveTree.lua:595–651`

For every socket and keystone, for every radius tier: list the in-range nodes (AABB pre-filter, squared-distance check). Stored as `socket.nodesInRadius[radiusIndex]`. This is **crucial for jewel mods** and should be kept — computing it live per jewel equip is wasted work.

### Port mapping

```csharp
public sealed record TreeModel(
    TreeVersion Version,
    IReadOnlyDictionary<int, Node> Nodes,
    IReadOnlyDictionary<int, Group> Groups,
    IReadOnlyList<Class> Classes,
    IReadOnlyDictionary<string, Node> KeystoneMap,    // case-insensitive
    IReadOnlyDictionary<string, Node> NotableMap,
    IReadOnlyDictionary<string, Node> AscendancyMap,
    IReadOnlyDictionary<int, MasteryEffect> MasteryEffects,
    IReadOnlyList<Connector> Connectors,               // pre-baked geometry
    Rect Bounds,
    float Size,                                         // min(Δx,Δy) * 1.1
    float[] OrbitRadii,
    float[][] OrbitAngles);
```

- Everything is `IReadOnly*` / records → thread-safe, diff-able, cheap to pass around.
- Keep `StringComparer.OrdinalIgnoreCase` dictionaries for the named lookups.
- `Node` itself is a class (not record) **only** because it has back-pointers (`Group`, `LinkedNodes`) that form cycles — use object identity and a `TreeModelBuilder` that wires pointers in a second pass.

## 4. Mod Parsing Per Node

`PassiveTree.lua:725–802` calls `modLib.parseMod(line)` for each stat string, producing `ModList` attached to `node.modList`. Failed lines set `node.unknown`/`node.extra` flags; keystones also get a synthetic `Keystone` mod.

**Port:** the mod parser is a separate feature (`mod-system/`). The tree-builder's only contract with it is: _"hand me a list of stat strings, get back a `ModList`."_ Call the parser **once during TreeModel construction** and store the parsed `ModList` on each `Node`. Parsing is expensive — never redo it per frame or per build.

## 5. Per-Build State Separation

**`PassiveSpec`** — `Classes/PassiveSpec.lua:19–90`

Each `Build` owns one `PassiveSpec`. It **does not copy the tree**. Instead it builds a thin wrapper per node using Lua's metatable inheritance:

```lua
self.nodes[id] = setmetatable({ linked = {}, power = {} }, treeNode)
```

Reads fall through to the shared `TreeModel` node; writes (`alloc`, `depends`, per-build power) go to the wrapper.

Fields added by `PassiveSpec`:

| Field | Purpose |
|---|---|
| `allocNodes : Set<NodeId>` | Currently allocated main-tree nodes |
| `allocSubgraphNodes` | Allocations inside cluster-jewel subgraphs |
| `jewels : NodeId → ItemId` | Jewels equipped per socket |
| `subGraphs : int → Subgraph` | Dynamic per-jewel subtrees (`BuildSubgraph`, id ≥ 65536) |
| `masterySelections : NodeId → EffectId` | Chosen mastery effects |
| `hashOverrides` | Tattoo overrides |

### Port mapping

```csharp
public sealed class PassiveSpec(TreeModel tree) {
    public TreeModel Tree { get; } = tree;
    public HashSet<int> AllocatedNodes { get; } = new();
    public Dictionary<int, Subgraph> Subgraphs { get; } = new();
    public Dictionary<int, int> MasterySelections { get; } = new();
    // ... etc

    public NodeView Node(int id) => new(Tree.Nodes[id], this);   // lightweight projection
}

public readonly record struct NodeView(Node Base, PassiveSpec Spec) {
    public bool IsAllocated => Spec.AllocatedNodes.Contains(Base.Id);
    public int? ChosenMasteryEffect => Spec.MasterySelections.TryGetValue(Base.Id, out var e) ? e : null;
}
```

The `NodeView` struct replaces Lua's metatable trick — zero allocation, composes base data with per-build state on read.

## 6. Version Management

The port **supports multiple tree versions** but only the modern schema — each supported version ships the same JSON shape, just different content (new nodes, rebalanced stats, changed ascendancies). No cross-schema migration code is needed; `NodeDto` deserialises every supported version uniformly.

```csharp
public sealed record TreeVersion(string Id, string Display, Uri CdnRoot);

public static class TreeVersions {
    public static readonly TreeVersion V3_25 = new("3_25", "3.25", new Uri("https://web.poecdn.com/..."));
    // Add further versions as they release.
    public static readonly TreeVersion Latest = V3_25;
}

public sealed class TreeRegistry {
    readonly ConcurrentDictionary<TreeVersion, Lazy<Task<TreeModel>>> _cache = new();
    public Task<TreeModel> GetAsync(TreeVersion v) =>
        _cache.GetOrAdd(v, key => new Lazy<Task<TreeModel>>(() => LoadAsync(key))).Value;
}
```

- Versions are keyed by their id (`"3_25"`).
- Each build references a `TreeVersion`; `TreeRegistry` hands back the frozen `TreeModel`.
- One version's `TreeModel` never migrates into another — swapping versions means re-allocating from scratch against the new graph (see `selection.md` §10 for the spec migration flow: keep nodes whose id still exists, drop the rest).

## 7. Bundling Strategy for Avalonia

| Option | Pros | Cons |
|---|---|---|
| **Embed `data.json` per version as resource** | Simple, offline-first | Large (~5–10 MB per version, >20 versions). Each startup pays JSON parse cost. |
| **Pre-serialised binary snapshot (`MessagePack`/`Bond`/custom)** | Fast cold start (~10× faster than JSON) | Needs a build-time converter; migration fragility |
| **Lazy per-version download + local cache** | Smallest install | Requires network on first use; matches PoB's CDN model |

**Recommendation:** ship the **latest** version as an embedded `data.json`, and download older versions on demand into `%AppData%/PathOfAvalonia/trees/{version}/`. Mirrors PoB's behaviour, keeps the binary small, and makes the "latest tree" experience fully offline.

## 8. Construction Cost Budget

Orders of magnitude (from PoB timings, roughly portable):

- JSON deserialise (3.25 tree, ~2000 nodes, ~1 MB): 30–80 ms
- Graph wiring + orbit placement: < 10 ms
- Mod parsing (all nodes): 100–300 ms (dominated by regex in ModParser)
- Jewel radius pre-compute: 20–50 ms
- **Total first load: ~200–400 ms** — do this on a background thread with a splash/progress UI; subsequent builds are free.

## 9. Invariants the Port Must Preserve

1. `TreeModel` is **frozen** after construction. Never mutate. If a new version is needed, build a new instance.
2. `PassiveSpec` never mutates `TreeModel.Nodes`. Its own `AllocatedNodes`, `Subgraphs`, `MasterySelections` are the only writable state per build.
3. Node positions are in **tree-space**, not screen-space. Do not pre-multiply by zoom/scale.
4. `keystoneMap` / `notableMap` lookups are **case-insensitive** — any build-import path (search by mod name, import from pob code) relies on this.
5. Cluster-jewel subgraph node IDs start at `65536` to avoid collision with main tree. Keep that offset or use a tagged ID type (`readonly record struct NodeId(int Raw, NodeKind Kind)`).

## 10. Source Map

| Concern | File : lines |
|---|---|
| Entry + cache | `Modules/Main.lua:314–325` |
| Constructor | `Classes/PassiveTree.lua:52–91` |
| Class / ascendancy resolution | `…:95–190` |
| Orbit tables | `…:192–197`, `971–992` |
| Node classification | `…:462–548` |
| Connection wiring | `…:565–592` |
| Polar placement | `…:805–833` |
| Jewel radius cache | `…:595–651` |
| Mod parsing | `…:725–802` |
| Version table | `GameVersions.lua:8–215` |
| Per-build spec init | `Classes/PassiveSpec.lua:19–90` |
| Cluster subgraph build | `Classes/PassiveSpec.lua:1829–2209` |
