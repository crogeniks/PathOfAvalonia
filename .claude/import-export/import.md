# Import — Logic

## Account-Based Import (`Classes/ImportTab.lua`)

1. User enters account name (may include `#discriminator`).
2. `DownloadCharacterList()` → `https://www.pathofexile.com/character-window/get-characters`.
3. **Auth**: `POESESSID` cookie (from browser or manual entry) — required for private profiles. 401 / 403 → prompt user.
4. Character list filtered by league.
5. Resolve canonical account name from profile URL (GGG API is case-sensitive).

## Character Download

- Tree: `/character-window/get-passive-skills?accountName=<acct>&character=<char>&realm=<realm>`.
- Gear + skills: `/character-window/get-items?accountName=<acct>&character=<char>&realm=<realm>`.
- Realms: `pc` (default), `xbox`, `sony`.

## JSON → Lua

`ProcessJSON(rawJson)` coerces GGG's JSON into Lua tables via a restricted `loadstring()` (braces, commas, strings, numbers, `true`/`false`/`null`).

## Tree Import

- Allocated node hashes bit-split from `hashes` array.
- Mastery effects: `mastery_effects[node] = effect` — deserialised into `masterySelections`.
- Cluster jewel hashes: subgraph nodes resolved against current tree.
- Ascendancy + class inferred from starting node IDs.

## Gear / Skills Import

- Each inventory slot → new `Item` via `Item:ParseRaw()` on raw lines.
- Socketed gems → synthesised socket groups.
- "Import" button offers: append or replace existing items / groups.

## Build Code Paste

`Line 190` (encode), `Line 294` (decode):

```
encode: XML → Deflate (zlib) → base64 → URL-safe ('+'→'-', '/'→'_')
decode: URL-safe → base64 decode → Inflate (zlib) → XML → LoadDB
```

## Tree URL Paste

PoE tree URL format decoded directly by `PassiveSpec:DecodeURL()`. PoEPlanner URLs handled by `DecodePoePlannerURL()`.
