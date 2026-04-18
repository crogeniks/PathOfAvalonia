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
