# Build Code Format

PoB builds are shared as a single URL-safe string that round-trips to the full build XML.

## Encode

```
  XML text
→ zlib Deflate      -- compressed bytes
→ base64 encode     -- ASCII
→ URL-safe swap     -- '+' → '-', '/' → '_'
→ share string
```

## Decode

```
  share string
→ swap back         -- '-' → '+', '_' → '/'
→ base64 decode     -- compressed bytes
→ zlib Inflate      -- XML text
→ LoadDB(xml)       -- restore build state
```

## Implementation

- `ImportTab.lua` line 190 — encode path.
- `ImportTab.lua` line 294 — decode path.
- `common.base64` + `common.zlib` (or host bindings) provide primitives.
- Resulting string is safe to paste into Pastebin or pobb.in. Third-party services often wrap it with a short prefix/suffix that must be stripped before decode.

## XML Root

```xml
<PathOfBuilding targetVersion="..." viewMode="..." level="...">
  <Build className="..." ascendClassName="..." ...>
    ...  <!-- per-tab save blocks: Spec, Skills, Items, Calcs, Config, Notes, Party -->
  </Build>
</PathOfBuilding>
```

See `build-management/data.md` for full schema.

## Length Bounds

- Typical build: 3–20 KB uncompressed XML → ~1–5 KB compressed → ~1.3–7 KB base64.
- No hard upper limit enforced by PoB; services like pobb.in may truncate if very large.
