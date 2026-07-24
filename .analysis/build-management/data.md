# Build Management — Data

## XML Schema

```xml
<PathOfBuilding targetVersion="3.24.0"
                viewMode="TREE"
                level="85"
                characterLevelAutoMode="false">
  <Build level="85"
         className="Witch"
         ascendClassName="Occultist"
         mainSkillIndex="1"
         bandit="None"
         pantheonMajorGod="None"
         pantheonMinorGod="None">

    <Spectre id="Metadata/Monsters/..."/>
    <Timeless jewelTypeId="X" conquerorTypeId="X"
              devotionVariant1="1" socketFilter="true"
              searchList="..."/>

    <!-- One <Tree>, <Skills>, <Items>, <Calcs>, <Config>,
         <Notes>, <Party>, <ImportExport> node per tab saver -->

    <PlayerStat    stat="Health"  value="500"/>
    <FullDPSSkill  stat="DPS"     value="1000"
                   skillPart="1"  source="Fireball"/>
    <MinionStat    stat="Life"    value="12000"/>
  </Build>
</PathOfBuilding>
```

## Version Migration

- `targetVersion` stored on the root; compared against `liveTargetVersion` on load.
- Mismatch triggers user-confirmed conversion popup; no in-place silent migration.
- `legacyLoaders` table handles obsolete section formats.

## Preview Metadata (for 3rd-party tools)

- `<PlayerStat>` rows: Health, Mana, resistances, charges, armour, evasion, ES, EHP.
- `<FullDPSSkill>` per active skill: DPS, skillPart, source trigger.
- `<MinionStat>` for summoned actors.
- `extraSaveStats`: `PowerCharges`, `FrenzyCharges`, `EnduranceCharges`, `ActiveTotemLimit`, `ActiveMinionLimit`.

## Folder Storage

`main.buildPath` (user-configurable) → subfolders → `<buildName>.xml`. Managed by `BuildListControl` + `PathControl`.

## Shared State

Shared item pool and shared item-set pool live under `main.sharedItemList` / `main.sharedItemSetList`, persisted to a companion XML next to build files.

## PathOfAvalonia saved-build envelope

Local saves live under `<config>/PathOfAvalonia/builds/<guid>.json` as a
versioned envelope. Each record contains its stable id, display name, game,
character-tree version, `ImportedBuild` snapshot, optional Atlas-tree version,
Atlas node ids, and last-updated time. Filenames use only generated ids, so user
build names never participate in path construction. Writes use a same-directory
temporary file followed by replacement and compact JSON; unreadable or corrupt
entries are ignored when listing without being deleted. Listing deserializes a
summary projection of the envelope and skips the character/Atlas payload, so
its memory and object-materialization cost does not grow with snapshot
complexity.

The parsed `ImportedItem.Text` projection is omitted from JSON and rebuilt from
the persisted raw item text on load. This avoids duplicating derived item data.
`settings.json` stores `lastBuildId` so the last saved workspace can be restored.
