# Atlas Passive Tree — Avalonia Design

## Source and scope

The bundled PoE1 Atlas tree is loaded from
`assets/PoE1/{version}/Atlas/data.json` with its sibling `assets/` directory.
The schema is GGG's passive-tree JSON variant: node/group/orbit geometry and
sprite metadata are shared in shape with the character tree, while the root,
point metadata, category medallions, backgrounds, and frames are Atlas-specific.

The sibling Path of Building repository does not currently provide an Atlas
planner implementation to port. Exact graph geometry and display text therefore
come from the GGG data; the interaction and aggregation behaviour described here
is a PathOfAvalonia design decision.

## Domain boundary

Atlas concepts do not live in the character passive classes. The feature uses:

- `AtlasTreeModel` / `AtlasNode` / `AtlasNodeType` for immutable loaded data.
- `AtlasPassiveSpec` for its one-root allocation state and dependency pruning.
- `AtlasPassiveStatAggregator` for allocated modifier summaries.
- `AtlasTreeViewModel` for Atlas search, version/diff state, point presentation,
  and temporary category-highlight intent.
- `AtlasTreeView` for Atlas rendering, hit testing, pan/zoom, and input.

Low-level, domain-neutral primitives remain shared: `Connector`, `TreeBounds`,
`GroupPosition`, `SpriteMap`, sprite atlas loading, and image asset resolution.
This preserves renderer/data reuse without making `PassiveSpec`,
`PassiveTreeViewModel`, or `PassiveTreeView` branch on Atlas concepts.

## Root and allocation

GGG exposes a synthetic string-keyed `root` whose single outgoing connection is
the real starting node. `Poe1AtlasTreeLoader` resolves that connection and marks
exactly one `AtlasNodeType.Start`; the start is permanently allocated and costs
no point.

Atlas allocations use the same user-facing path behaviour as the character
tree: selecting a reachable target allocates its shortest path, and refunding a
node also refunds allocations disconnected from the single start. Cluster
category icons are never allocatable.

Nodes marked `isWormhole` are Atlas gateways. Paired gateways remain adjacent
in `AtlasNode.LinkedNodes`, so allocation and connectivity cross the pair as a
teleport. The gateway-to-gateway edge is deliberately omitted from drawable
connectors; only each gateway's ordinary local connections are rendered.

`points.totalPoints` is a display threshold (138 in 3.29), not an allocation
gate. The planner deliberately permits allocation above the threshold. The
counter changes from gold to red at `allocated >= totalPoints` and remains red
above it.

## Category icons

GGG labels the unallocatable cluster medallions with `isMastery`, but Atlas has
no mastery-effect selection. The loader maps them to
`AtlasNodeType.ClusterIcon`. Tapping a category icon finds every category icon
with the same logical icon path and highlights those clusters for 1.25 seconds.
This transient timer is view-owned; it does not change the Atlas spec.

## Search and aggregation

Search is case-insensitive across Atlas passive names, types, and stat lines.
Start/category decorations are excluded. Matches receive the same red-ring
visual language as character-tree search while remaining Atlas view-model state.

The aggregation sidebar classifies each modifier line independently by the
mechanic it references, combining effects of the same type across the tree. A
generic Scarabs-found modifier is therefore grouped under Scarabs even when its
node sits inside a Harvest cluster, while an isolated notable mentioning
Abysses is grouped under Abyss. Specific mechanic references take priority,
followed by Scarabs and Maps; the node's category icon is used only when the
line itself has no unambiguous type. General, Notables, and Keystones are the
final fallbacks. Within each type, stat lines are grouped by textual shape. Numeric
modifier positions are summed independently, preserving an explicit leading
`+`. When a line contains percentages, only percentage values are summed so
fixed qualifiers such as `Tier 1-15` and `1 tier higher` stay unchanged.
Non-percentage numeric modifiers are summed except explicit tier qualifiers;
non-numeric duplicate lines are deduplicated. Gateway navigation text is
excluded because it is connectivity metadata, not an Atlas modifier.

Each JSON `stats` entry remains one semantic modifier even when GGG embeds a
newline for display wrapping. The aggregation UI wraps that combined text
visually instead of creating multiple modifier rows. While search is active,
each aggregated row is matched against its mechanic, modifier text, and source
node names; non-matching rows remain visible at reduced opacity so their group
context is preserved.

## Version and diff placement

Character tree version/diff controls live inside the Passive Tree tab rather
than the application header. Atlas owns a separate version/diff selector inside
the Atlas Tree tab. `GameDefinition.AtlasTreeVersions` lists only versions with
bundled Atlas data; adding 3.28 consists of adding its bundle and registering
the version. Atlas version migration retains still-present connected node IDs,
and `AtlasTreeDiff` compares Atlas nodes without passing through character-tree
diff types.
