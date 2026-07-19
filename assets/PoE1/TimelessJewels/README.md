# PoE1 timeless-jewel data

These are generated application assets for seed-accurate timeless-jewel passive
tree transformations. Their source of truth is Path of Building commit
`f3fd0194d5289957169e0eb033901ab77407ddfe`:

- `Data/TimelessJewelData/LegionPassives.lua`
- `Data/TimelessJewelData/NodeIndexMapping.lua`
- `Data/TimelessJewelData/*.zip[.partN]`
- `TreeData/legion/`

Regenerate them from the sibling checkout with:

```sh
lua tools/export_timeless_jewels.lua \
  ../PathOfBuilding/src/Data/TimelessJewelData \
  ../PathOfBuilding/src/TreeData/legion \
  assets/PoE1/TimelessJewels
```

The `.z` files retain PoB's zlib-compressed lookup bytes. `definitions.json`
and `mapping.json` are compact JSON conversions of the two Lua data modules;
`sprites.json` and `sprites/` contain the timeless replacement-node art.
