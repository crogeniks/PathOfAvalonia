# Jewels — Maths

## Radius (tree units)

| Size  | Radius | Radius Index |
|-------|--------|--------------|
| Small  | 800  | 1 |
| Medium | 1200 | 2 |
| Large  | 1500 | 3 |
| Very Large | 1800 | 4 (Stormshroud/Thread of Hope) |

Distance test:

```
dx = socket.x - node.x
dy = socket.y - node.y
if (dx*dx + dy*dy) <= (radius*radius): in range
```

Pre-computed and cached on the socket during `PassiveTree` init.

## Cluster Jewel Node Generation

Inputs from the item:

- `passive_count` (2–12).
- `notable_count` (0–5 depending on size).
- `added_skill_types` → notable pool filter.

Node layout:

- `orbit` index determined by total node count; `orbitIndex` chosen to distribute around the socket.
- Notable positions at canonical slots (e.g. 12/4/8 o'clock for three notables).
- Socket node inserted at mid-orbit for Medium/Large (extra sockets).

## Timeless Replacement

Deterministic lookup (pseudo):

```
key = (jewelType, conqueror, seed, nodeIndex)
replacement = lut[key]      -- stat array
```

LUT is precomputed by GGG and redistributed in `.zip` form. Decompression happens once per jewel instance; results cached in memory.

## Cluster — added Small Passives

Each small passive is a single random stat from the per-tag small pool; tier gated by the item's `enchantMods` level. Integration with calc engine is via standard `modList` append.
