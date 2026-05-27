# Generate PoE2 Sprite Map

This note is historical. PoE2 now loads the official GGG atlas JSON files
directly from `assets/PoE2/<version>/assets/` and no longer requires a generated
`sprites_*.json` file.

If a generated sprite map is needed again in the future, it should read
`assets/PoE2/<version>/data.json` plus the exported atlas manifests under
`assets/PoE2/<version>/assets/`, then emit the same `SpriteMap` JSON shape used by PoE1:

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
