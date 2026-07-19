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
key = (jewelType, normalizedSeed, nodeIndex)
replacement = lut[key]      -- stat array
```

The LUT is precomputed by GGG and redistributed in zlib-compressed form.
PathOfAvalonia inflates each family once per application asset cache and caches
the resolved effects of each active jewel on `PassiveSpec` rebuild.

Elegant Hubris normalizes its displayed seed with `seed / 20` before indexing.
For non-Vaal families the fixed-width LUT stores one operation byte for each
of the 452 mapped notables per seed. Operation IDs below 96 select a stat
addition; IDs at or above 96 select a replacement node. Local IDs are converted
through the jewel-specific mapping before this split.

Glorious Vanity uses a variable-width payload. A size byte is stored for every
`(nodeIndex, seed)`, followed by the payloads. Two- or three-byte payloads
select a replacement and provide its rolled values. Six- or eight-byte
payloads select Might/Legacy of the Vaal and pair several addition IDs with
rolls; repeated additions are summed before their stat text is materialized.

## Cluster — added Small Passives

Each small passive is a single random stat from the per-tag small pool; tier gated by the item's `enchantMods` level. Integration with calc engine is via standard `modList` append.
