# Passive Tree — Maths

## Polar Node Placement (PassiveTree.lua:805–836)

Tree is organised in **groups**; each node is placed in an **orbit** around the group centre:

```
angle       = orbitAnglesByOrbit[orbit][orbitIndex]   -- pre-computed
orbitRadius = orbitRadii[orbit]                        -- (0, 82, 162, 335, 493 px)
node.x = group.x + sin(angle) * orbitRadius
node.y = group.y - cos(angle) * orbitRadius
```

- `skillsPerOrbit = (1, 6, 12, 12, 40)` — nodes per orbit.
- Orbit angle distribution:
  - 16-node orbits → every 30°/45° (irregular).
  - 40-node orbits → every 10°/45° (irregular).
  - Otherwise uniform.

## Connector Drawing (PassiveTree.lua:866–969)

### Same-group, same-orbit → arc

- Arc angle = angular diff between nodes.
- If > 90°, split into two arcs (orbit sprites cover only 90°).
- Rendered as texture-mapped quad; excess clipped via parametric `p`.

### Cross-group / cross-orbit → straight line

- Perpendicular offset from centre; thickness scaled by asset height.
- Quad vertices: `{n1-off, n1+off, n2+off, n2-off}`.

### BuildArc() (929–969)

- Kite-shaped quad: one vertex at group centre.
- `clipAngle = π/4 - arcAngle/2`.
- Texture projection: `p = 1 - max(tan(clipAngle), 0)`.
- Mirrored arcs for > 90° (avoid seam).

## Path-Finding — BFS

- Complexity **O(V + E)**.
- Distance: `1` per unallocated hop, `0` through allocated (traversal through your tree is free).
- Constraints: no ClassStart traversal except from root; no cross-ascendancy except from Ascendant; no outward traversal from Mastery nodes.

## Spec Compare Delta (PassiveTreeView.lua:147–168)

`GetCompareNodeColor()` returns `{r,g,b}`:

- `{0,1,0}` — added.
- `{1,0,0}` — removed.
- `{0,0,1}` — modified (mastery effect mismatch / jewel swap).
- neutral otherwise.
