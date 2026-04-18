# Skills & Gems — UI

## SkillsTab

- **Top**: skill-set selector (dropdown) + Manage / New / Copy.
- **Left**: `SkillListControl` — all socket groups.
  - Active-skill indicator.
  - Gem colour dots (R/G/B/W).
  - Right-click → set as main.
  - Ctrl+right → toggle FullDPS inclusion.
  - Ctrl+left → toggle enabled.
- **Right** (when a group is selected): socket-group editor.
  - Label input, Slot dropdown.
  - `Enabled`, `FullDPS`, `Source` note (read-only if granted by item).
  - `Imbued Support` dropdown (one per slot).
- **Below**: per-gem slots (`CreateGemSlot` repeats for each gem in `gemList`):
  - `GemSelectControl` dropdown.
  - `Level`, `Quality`, `Enabled`, `Count` inputs.
  - DPS-impact indicator (+/-/=) coloured.

## GemSelectControl

- Filterable dropdown with tag search syntax: `:fire:-cold:area`.
- Sort by DPS impact OR alphabetic.
- S / A overlay buttons for Support / Active filter.
- Match priority: exact → abbreviation → starts-with → contains.
- Level requirement filter obeys `defaultGemLevel` setting.
- Hover tooltip: full stats, damage diff vs current, requirements.

## Gem Slot Columns

Name — Level — Quality — Enabled — Count. Order is preserved as displayed.

## Skill Set Management

Multiple skill sets per build (mapping / bossing / level-up). Each set owns its full socket-group list. Switching is a reference swap.
