# PoE2 Sprite Generator

Generates the PoE2 sprite map consumed by `PassiveTreeView`.

Example:

```sh
dotnet run --project tools/poe2-assets/generate-poe2-sprites.csproj -- \
  --tree /home/crogeniks/dev/PathOfBuilding-PoE2/src/TreeData/0_4/tree.json \
  --tree-assets /home/crogeniks/dev/PathOfBuilding-PoE2/src/TreeData/0_4 \
  --ui-assets /home/crogeniks/dev/PathOfBuilding-PoE2/src/Assets \
  --out assets/PoE2 \
  --version 0_4
```

Prerequisites:

- `zstd` for decompressing `.dds.zst`.
- ImageMagick `magick` for converting `.dds` textures to `.png`.

The JSON tree references logical art paths, while `tree.lua` carries the DDS array
indices for those paths. The generator reads both files, decodes BC1 DDS array
slices for passive icons, packs them into `icons/poe2NodeIcons.png`, and emits
real per-icon sprite rects. Missing source art is reported and skipped; the app
keeps fallback rendering for any unresolved key.
