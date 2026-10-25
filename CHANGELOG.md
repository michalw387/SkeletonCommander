# Changelog

## 1.9.0

### Added
- **Dismissing the weakest skeleton** – when a new summon goes over the summon limit, the skeleton with the
  least HP is dismissed instead of the oldest (the oldest on a tie), and the new summon gets the weapon of
  its team slot. The same applies when you come back to more skeletons than your limit. The newest skeleton
  is never dismissed. `Summon.DismissWeakest = false` dismisses the oldest as in the base game.
- **Skeletons stay with other players** – far skeletons are no longer dismissed while another player is near
  them, so they keep fighting alongside your friends, e.g. after you die or use a portal. They don't count
  toward your summon limit while you are away; when you come back to more skeletons than your limit, the
  extra ones are dismissed. `Summon.KeepNearOtherPlayers = false` dismisses them anyway, as in 1.8.0.

## 1.8.0

### Added
- **Skeleton panels in the HUD** – the detailed list shows a panel per skeleton: number and name, weapon,
  distance and state, and an HP bar colored by health with `HP / max` on it. `HUD.Style = Text` brings back
  the one-line list.
- **Staff of Protection barrier** – a protected skeleton gets a blue bar with the barrier left, its
  capacity and the seconds remaining (`HUD.ShowShield`); also shown in the text list and `/sk list`.
- **Team loadout** – pick each team member in `Summon.TeamSlot1`–`TeamSlot4` (Random, SwordAndShield or
  Bow). With a summon limit of N (1–4 by Dead Raiser upgrade level) the team is slots 1 to N, and each new
  summon gets the weapon the team is missing, e.g. a new archer when an archer dies. The HUD shows the team
  status (`HUD.ShowTeam`). `Summon.TeamEnabled = false` gives random weapons as in the base game.
- **Dismissing skeletons left behind** – skeletons more than 100 m away (`Summon.DismissBeyondDistance`,
  20–120 m) or outside the area the game simulates around you are dismissed, also when you use a portal or
  die far from them. Skeletons left in an area the game unloads could otherwise exceed the summon limit.
- If you come back to more skeletons than your summon limit, the oldest are dismissed.
- Each dismissal of a far skeleton is written to the BepInEx log (name, distance, owner).

### Changed
- Larger text in the skeleton panels.
- Plugin GUID changed to `xmordax.valheim.skeletoncommander` (author renamed to xMordax); the previous
  config file is taken over automatically.
- The Thunderstore package now includes this changelog.

### Removed
- `Summon.NextSummonLoadout` and its `Hotkeys.CycleNextSummonLoadout` hotkey (Left Alt + K); the team slots
  replace them (e.g. set all four slots to Bow for an all-archer team).

### Fixed
- Dismissing a skeleton that no player owned (near the edge of the loaded area) did nothing; the mod now
  takes it over first.
- The "LiberationSans SDF Font Asset was not found" warning for the mod's HUD texts.

## 1.7.1

- README: noted that the mod is client-side; removed the build-from-source instructions.

## 1.7.0

- First public release as **Skeleton Commander** (previously Skeleton Tracker).
- Minimap pins, HUD counter and detailed list (every part can be hidden, adjustable font size),
  always-visible health bars colored by HP, glow and death notifications.
- Follow / stay / dismiss for all skeletons or the one you aim at, with hotkeys and `/sk` chat commands;
  hotkeys work while other keys are held, and the game ignores F/G while Alt is held.
- Choice of weapons for the next summons (random, sword & shield, bow), verified on the items actually given.
- Skeletons told to stay count toward the summon limit.
