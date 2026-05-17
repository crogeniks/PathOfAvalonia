# Convert PoE2 Tree Assets

PoE2 source tree assets are not decoded at runtime by PathOfAvalonia. Convert source
`.dds` or `.dds.zst` files to app-readable `.png` or `.webp` files before embedding
them under `assets/PoE2`.

Milestone 1 currently uses fallback node rendering and does not require the full
asset set. When visual parity work starts, convert only the atlases referenced by
the generated PoE2 sprite map and keep the runtime asset service limited to normal
Avalonia bitmap loading.

