# Runtime — Update System

## Manifest

`manifest.xml` at repo root declares every shipped file + its SHA1 and part of the release metadata.

```xml
<Manifest>
  <Version number="2.51.0"/>
  <Source part="program" url="https://.../program/"/>
  <Source part="data"    url="https://.../data/"/>
  <File name="Launch.lua"        part="program" sha1="..."/>
  <File name="Data/Gems.lua"     part="data"    sha1="..."/>
  ...
</Manifest>
```

`manifest.cfg` chooses the active update branch (stable / dev / fork).

## Check Flow (`src/UpdateCheck.lua`)

1. Download latest `manifest.xml` from the configured update URL.
2. Parse both remote and local manifests.
3. Diff files by SHA1:
   - **added** — present remotely, missing locally → download.
   - **changed** — SHA differs → download.
   - **removed** — present locally but not remotely → queue for deletion.
4. Stream downloads of changed/added files into a temp folder next to the install.
5. Write a "move list" ops file describing which temp files → install paths and which files to delete.
6. Hand off to `UpdateApply`.

## Apply Flow (`src/UpdateApply.lua`)

1. Host relaunches into update-apply mode with `-updateApply <tempFolder>` or equivalent.
2. `UpdateApply` reads the ops file.
3. Executes moves / deletes atomically where possible.
4. Relaunches the main application.

## Fresh Install (`src/LaunchInstall.lua`)

Bootstrap variant used when the install is missing or corrupt:
- Downloads a full copy via cURL.
- Reconstructs manifest.
- Hands off to normal launch.

## Error Handling

- SHA mismatch after download → retry up to N times, then abort update.
- Partial write failure → next launch detects stale temp folder and retries.
- `ToastNotification` surfaces update status to the user without blocking the main app.
