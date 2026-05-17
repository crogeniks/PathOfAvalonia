# Generate PoE2 Sprite Map

PoE2 milestone 1 ships with a minimal `assets/PoE2/sprites_0_4.json` so the tree can
load and render with programmatic fallback frames.

Future sprite-map generation should read `assets/PoE2/tree_0_4.json`, collect the
passive icon and frame atlas references needed by visible nodes, and emit the same
`SpriteMap` JSON shape used by PoE1:

```json
{
  "atlases": {
    "frame": {
      "file": "frame-poe2.png",
      "w": 1024,
      "h": 1024,
      "coords": {}
    }
  }
}
```

The app should continue to treat missing optional sprite entries as non-fatal.

