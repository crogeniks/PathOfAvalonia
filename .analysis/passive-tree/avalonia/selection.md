# Passive Tree — Selection & Allocation (Avalonia Port)

Every rule a click / shift-click / deallocation must respect, expressed as a state machine the C# port can enforce.

## 1. State Model

```csharp
public sealed class PassiveSpec {
    HashSet<int> AllocatedNodes;
    Dictionary<int, Subgraph> Subgraphs;              // per equipped cluster jewel
    Dictionary<int, int> MasterySelections;           // nodeId → effectId
    Dictionary<int, string> HashOverrides;            // tattoo overrides
    int ClassId;
    int AscendancyId;
    int SecondaryAscendancyId;

    // Derived, rebuilt after every mutation:
    Dictionary<int, IReadOnlyList<int>> NodePaths;    // node → shortest path to allocation
    Dictionary<int, HashSet<int>> NodeDependencies;   // node → nodes that lose connectivity if it's removed
    HashSet<int> ConnectedToStart;
}
```

Every mutation (`Alloc`, `Dealloc`, `SelectClass`, `SelectAscendancy`, `EquipJewel`, `SelectMasteryEffect`) must end by running the equivalent of `BuildAllDependsAndPaths`.

## 2. Legality Rules for Allocation

A node is **allocatable** iff **all** of the following hold:

### R1. Reachability
- `node.Path` is non-empty (an allocated node must be reachable via a BFS from class start through allocated nodes), **OR**
- `node.IntuitiveLeapLikesAffecting.Count > 0` (a jewel like Intuitive Leap covers the node).

Source: `PassiveSpec.lua:763–793`, `889–928`.

### R2. Ascendancy boundary
- Path may not cross ascendancy boundaries unless one endpoint is outside any ascendancy (entering from the main tree).
- Ascendant class may path across other ascendancies via "Path of the X" nodes.

Enforced inside BFS: `node.ascendancyName == other.ascendancyName || (curDist == 0 && !other.ascendancyName)` — `PassiveSpec.lua:911`.

### R3. ClassStart / AscendClassStart are not traversable
- Paths start **from** them, but never pass **through** them.

Enforced in BFS: `other.type != "ClassStart" && other.type != "AscendClassStart"` — `:911, 1006`.

### R4. Mastery requires effect
- A mastery node cannot enter the `Allocated` state without a chosen `effectId`.
- On click: show effect-picker popup. Allocation only commits after the user picks an effect.
- Source: `PassiveTreeView.lua:484–490`, `TreeTab.lua:1001–1003`.

### R5. Socket nodes
- No extra rules — treated as a normal node for allocation.
- Right-click an allocated socket → navigate to items UI (`PassiveTreeView.lua:494–502`).

### R6. Point budget
- **Not enforced at the model level.** PoB lets you over-allocate and shows a warning via the points-used counter in the tab. The port should mirror this — display "used / max" and colour red when over, but don't block the click.

## 3. BFS Path-Finding — `BuildPathFromNode`

```csharp
// Pseudocode faithful to PassiveSpec.lua:889–928
void BuildPath(Node root) {
    var dist = new Dictionary<int, int> { [root.Id] = 0 };
    var queue = new Queue<Node>();
    queue.Enqueue(root);

    while (queue.TryDequeue(out var cur)) {
        var curDist = dist[cur.Id];
        foreach (var other in cur.LinkedNodes) {
            // Traversal gate (R2, R3, mastery-is-dead-end)
            if (other.Type == NodeType.Mastery) continue;
            if (other.Type == NodeType.ClassStart || other.Type == NodeType.AscendClassStart) continue;
            if (!AscendancyOk(cur, other, curDist)) continue;

            int stepCost = AllocatedNodes.Contains(other.Id) ? 0 : 1;
            int newDist  = curDist + stepCost;
            if (dist.TryGetValue(other.Id, out var d) && d <= newDist) continue;

            dist[other.Id] = newDist;
            other.Path = [other, ..cur.Path];     // prepend-like
            queue.Enqueue(other);
        }
    }
}
```

**Edge costs:** 0 through already-allocated nodes (they're free), 1 otherwise. This means `node.Path.Count` is exactly "number of extra points needed to allocate this node." The UI shows that number in the hover tooltip.

## 4. Dependency Tracking — `BuildAllDependsAndPaths`

Every allocated node carries a `Dependencies` set: _"if I am deallocated, all of these lose their connection to start and must also be deallocated."_

Computation (`PassiveSpec.lua:1082–1556`):

1. Initialise `node.Dependencies = {}` for every allocated node.
2. For each allocated `A`: treat `A` as removed; run DFS from class start (`FindStartFromNode`) across the remaining allocated set.
3. Nodes that can no longer reach start become `A.Dependencies`.
4. Nodes covered by an Intuitive-Leap jewel are **excluded** from dependency propagation.
5. Mastery nodes don't block traversal when they have multiple allocated neighbours — only when they're the sole link.

Cascade deallocation:

```csharp
void Deallocate(int nodeId) {
    var node = Spec.Node(nodeId);
    foreach (var dep in node.Dependencies) AllocatedNodes.Remove(dep);
    AllocatedNodes.Remove(nodeId);
    if (MasterySelections.ContainsKey(nodeId)) MasterySelections.Remove(nodeId);
    RebuildPathsAndDependencies();
}
```

**Multi-phase resolution** (`:1384–1556`):
- Phase 1 — detect hard dependencies via DFS.
- Phase 2 — resolve ambiguities for Intuitive-Leap-affected nodes.
- Phase 3 — rebuild paths for all still-allocated nodes.

## 5. AllocNode / DeallocNode

### Alloc

```csharp
void Allocate(int targetId, IReadOnlyList<int>? altPath = null) {
    var node = Tree.Nodes[targetId];

    // Mastery requires effect first
    if (node.Type == NodeType.Mastery && !MasterySelections.ContainsKey(targetId))
        throw new InvalidOperationException("Mastery effect not chosen");

    // Intuitive Leap bypass: allocate only the target
    if (node.IntuitiveLeapAffecting.Count > 0) {
        AllocatedNodes.Add(targetId);
        RebuildPathsAndDependencies();
        return;
    }

    // Otherwise walk the path (or user-supplied alt path)
    var path = altPath ?? node.Path;
    foreach (var id in path) AllocatedNodes.Add(id);
    RebuildPathsAndDependencies();
}
```

### Dealloc

Always cascades via `Dependencies`. Class start and ascendancy start cannot be directly deallocated — they only change via class/ascendancy switching.

## 6. AltPath (Shift-Click Path Tracing)

Enables the user to **hand-pick** the path to a node instead of accepting BFS's shortest.

- Shift held → `traceMode = true`, UI shows `tracePath`.
- Each click on a linked node extends `tracePath`.
- When the final click lands on the intended target: `Allocate(target, altPath: tracePath)`.

Source: `PassiveTreeView.lua:266–360, 487`.

**Validation:** the port must verify that every consecutive pair of node IDs in `altPath` is actually linked in the tree, and that the first node is adjacent to the already-allocated set.

## 7. Class / Ascendancy Switching

### `SelectClass(classId)`

1. Deallocate old class's `startNodeId`.
2. Allocate new class's `startNodeId`.
3. Reset ascendancy (`SelectAscendClass(0)`).
4. Rebuild paths/deps — orphaned nodes auto-prune.
5. UI confirms if the existing tree cannot reach the new start.

Source: `PassiveSpec.lua:555–578`.

### `SelectAscendClass(ascId)`

1. Deallocate old ascendancy start.
2. Allocate new ascendancy start.
3. Nodes belonging to the previous ascendancy become orphaned → pruned.
4. Rebuild.

Source: `PassiveSpec.lua:592–610`.

### `SelectSecondaryAscendClass(ascId)` (alternate / bloodlines ascendancies)

Same machinery, tracked separately in `secondaryAscUsed`. Source: `:612–648`.

**Carry-over:** main-tree allocations persist as long as they stay connected to the new start. Everything disconnected is pruned silently in the rebuild.

## 8. Masteries

State matrix:

| User-visible state | `MasterySelections[id]` | Allocated in set |
|---|---|---|
| Not yet on tree | `none` | `false` |
| Options visible (clicked, not yet picked) | `none` (transient UI) | `false` |
| Allocated with chosen effect | `int effectId` | `true` |

Rules:

- Allocating requires picking one of `node.MasteryEffects`.
- An effect that is **already assigned to another mastery node in the spec** is greyed out in the picker (`TreeTab.lua:1017–1023`).
- Right-click an allocated mastery → reopen picker (swap effect).
- Deallocating clears the selection and restores all effect options.

## 9. Cluster Jewels

### Lifecycle

1. User equips a cluster jewel into a socket node.
2. `BuildSubgraph(socket, item)` generates a subgraph: proxy group, entrance node linked to the parent socket, orbit of expansion nodes, final notables/smalls per jewel affixes.
3. Subgraph node IDs are high-bit offset (≥ 65536) to avoid collision.
4. Subgraph is stored on `Spec.Subgraphs[socketId]` — `TreeModel` is untouched.

Source: `PassiveSpec.lua:1829–2209`, `BuildClusterJewelGraphs :1667`.

### Allocation persistence

`allocSubgraphNodes` is a **list of node IDs** that were allocated in the subgraph before the last rebuild. After rebuilding the subgraph (jewel swap, etc.), the code tries to restore those allocations in the new graph. IDs are **not stable** across jewel swaps — the persistence is by the list, not by matching IDs.

### Removal

Unequipping a jewel (or deallocating its socket) deallocates every node in the subgraph and clears `allocSubgraphNodes` for that socket.

## 10. Multiple Specs per Build

- `specList[]` on `TreeTab`. Each is a full `PassiveSpec`.
- `activeSpec` is used by calculation.
- `activeCompareSpec` drives the compare overlay in the viewer (red/green/blue diff).
- Rules are identical across specs — each is independent. Switching active spec does not affect any other spec.

### Respec cost display

Computed in the spec dropdown tooltip (`TreeTab.lua:72–97`):
- Ascendancy nodes that differ: 5× base.
- Regular nodes that differ: 1× base.
- Socket / cluster nodes: no cost.
- Base cost = `data.goldRespecPrices[characterLevel]`.

Display only — not enforced.

## 11. Reset / Respec Operations

- **Reset tree** button (`TreeTab.lua:133–138`) → `ResetNodes()` — deallocate everything except class/ascendancy starts; clear mastery selections.
- No partial-respec command. Users deallocate one node at a time.
- Switching between specs is how users compare "before/after" tree shapes.

## 12. Input Modifiers

| Modifier | Effect |
|---|---|
| Left-click | Alloc unallocated / dealloc allocated |
| Right-click on Socket (allocated) | Open items UI for this socket |
| Right-click on Mastery (allocated) | Reopen effect picker |
| Shift | Trace-path mode (builds `tracePath`) |
| Ctrl + left-click | Zoom in (±2 levels) |
| Ctrl + right-click | Zoom out |
| Ctrl (held while hovering) | **Suppresses tooltip** — useful when reading what's behind the mouse |
| Mouse wheel | Zoom ±1/3 level |
| PageUp / PageDown | Zoom ±1 level |

Source: `PassiveTreeView.lua:243–244, 266–273, 367–515`.

## 13. Invariants the Port Must Enforce

1. **Allocated ⇒ reachable.** Every id in `AllocatedNodes` must either (a) be a ClassStart/AscendStart for the current class, or (b) have a non-empty path to allocated neighbours, or (c) be covered by an Intuitive-Leap-like jewel.
2. **Dependencies transitively closed after every mutation.** Orphans must never survive a `RebuildPathsAndDependencies` call.
3. **Masteries in `AllocatedNodes` ⇒ entry in `MasterySelections`.**
4. **One effect per mastery, and each effect id appears at most once across `MasterySelections`.**
5. **Class/ascendancy starts are always in `AllocatedNodes`** and change only via the dedicated switch methods.
6. **Cluster subgraph nodes live under `allocSubgraphNodes`, not `AllocatedNodes`** — keeping them separate avoids leaking proxy IDs into main-tree logic.
7. **No point cap** — the model happily accepts over-allocation; UI flags it.

## 14. C# API Shape

```csharp
public sealed class PassiveSpec {
    public bool CanAllocate(int nodeId, out string? reason) { ... }
    public void Allocate(int nodeId, IReadOnlyList<int>? altPath = null);
    public void Deallocate(int nodeId);
    public void SelectMasteryEffect(int nodeId, int effectId);

    public void SelectClass(int classId);
    public void SelectAscendancy(int ascId);
    public void SelectSecondaryAscendancy(int ascId);

    public void EquipJewel(int socketId, Item jewel);
    public void UnequipJewel(int socketId);

    public IReadOnlyList<int> Path(int nodeId);          // computed path to node
    public IReadOnlySet<int> Dependents(int nodeId);     // nodes that fall with this one

    public event Action? SpecChanged;                     // fires after RebuildPathsAndDependencies
}
```

The **`SpecChanged` event** is what the view subscribes to for re-render. Keep the API surface synchronous — every operation is O(V+E) on the allocated set and runs in sub-millisecond time on modern hardware.

## 15. Source Map

| Rule | File : lines |
|---|---|
| Alloc / Dealloc | `PassiveSpec.lua:763–812` |
| Path BFS | `PassiveSpec.lua:889–928` |
| Start BFS (Split-Personality) | `PassiveSpec.lua:975–1016` |
| Dependency multi-phase | `PassiveSpec.lua:1082–1556` |
| Class switch | `PassiveSpec.lua:555–578` |
| Ascendancy switch | `PassiveSpec.lua:592–648` |
| Cluster subgraph build | `PassiveSpec.lua:1829–2209` |
| Point counts | `PassiveSpec.lua:815–835` |
| Click routing | `PassiveTreeView.lua:367–515` |
| Shift-trace path | `PassiveTreeView.lua:266–360, 487` |
| Mastery popup | `TreeTab.lua:1001–1035` |
| Respec cost | `TreeTab.lua:72–97` |
