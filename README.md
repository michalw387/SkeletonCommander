# Skeleton Commander

A Valheim mod (BepInEx 5) for tracking and commanding the skeletons summoned with the
**Dead Raiser** staff. The mod is client-side only and affects only the skeletons you summoned.

**Client-side:** only the players who want to use the mod need to install it. It is not required
on dedicated servers or by other players, who also see the weapon chosen for your skeletons.

## Features

- **Minimap pins** – each of your skeletons gets a map pin with the skeleton trophy icon, updated
  every 0.5 s. On the large map the pin label shows its state: following or staying.
- **HUD counter** – below the minimap: how many skeletons you have and how many are following you
  or staying. Optionally a detailed, numbered list: a panel per skeleton with its name, weapon,
  distance and state, and an HP bar colored by health (or one text line each with `HUD.Style = Text`).
  A skeleton protected by the Staff of Protection also gets a blue bar with the barrier left and its
  time. Skeletons are sorted newest first, so the oldest one is at the bottom;
  the numbers match the chat list. Each part of the HUD can be hidden in the config.
  The HUD is part of the game's interface, so the inventory and menus cover it like the minimap.
- **Health bars** – always visible above your skeletons' heads (up to 40 m by default), colored by
  HP: green above 60%, yellow above 30%, red below.
- **Glow** – a subtle light around your skeletons so they are easy to tell apart.
- **Death notifications** – an on-screen message when one of your skeletons dies.
- **Team loadout** – choose your team in `Summon.TeamSlot1`–`TeamSlot4` (Random, SwordAndShield or
  Bow each). With a summon limit of N (1–4, depending on the Dead Raiser's upgrade level) your team is
  slots 1 to N, and each new summon gets the weapon the team is missing – e.g. when an archer dies, the
  next skeleton is an archer. The HUD shows the team status, e.g. `Team: sword & shield 2/2 · bow 1/2`.
  Turn it off with `Summon.TeamEnabled` for random weapons as in the base game.
  A `SwordAndShield` skeleton always gets a shield, never just a sword. The weapon stays the same after
  reloading and is the same for other players.
- **Summon limit fix** – skeletons told to stay count toward the Dead Raiser's summon limit, so you
  cannot exceed it by leaving skeletons behind (the oldest one is dismissed instead, as in vanilla).
  Skeletons more than 100 m away (configurable) or outside the area the game simulates around you are
  dismissed, also when you use a portal or die far from them, because the game stops tracking skeletons
  in areas it unloads. If you still come back to more skeletons than your limit, the oldest are dismissed.
- **Commands and hotkeys** – follow, stay and dismiss for all skeletons at once, or only for the one
  you are aiming at (the hover text shows the hotkeys).

## Requirements

- Valheim (PC)
- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (BepInEx 5)

## Installation

### With a mod manager (recommended)

Install **SkeletonCommander** from [Thunderstore](https://thunderstore.io/c/valheim/) with
r2modman or the Thunderstore Mod Manager; BepInExPack Valheim is installed with it.

### Manually

1. Install BepInExPack Valheim following its instructions and start the game once so BepInEx
   creates its folders.
2. Copy `SkeletonCommander.dll` to:
   ```
   <Valheim folder>/BepInEx/plugins/SkeletonCommander/
   ```
3. Start the game. On first launch the config file
   `BepInEx/config/xmordax.valheim.skeletoncommander.cfg` is created.

To uninstall, delete the `BepInEx/plugins/SkeletonCommander/` folder (and optionally the `.cfg` file).

## Controls

### Hotkeys

| Hotkey (default) | Action |
|---|---|
| Left Alt + F | skeletons follow you |
| Left Alt + G | skeletons stay in place |
| Left Alt + H | dismiss skeletons |
| Left Alt + J | list skeletons in chat |

When you aim at one of your skeletons, the Follow / Stay / Dismiss hotkeys affect only that skeleton
(the key hints appear in its hover text). Hotkeys are ignored while typing in chat and in menus,
the inventory and the large map.

Hotkeys also work while you hold other keys, e.g. while running. While a hotkey's modifier is held
(Left Alt by default), the game ignores that hotkey's key, so Left Alt + F / G do not also trigger
the Forsaken power or the emote wheel.

### Chat commands

Type in chat (Enter), or in the F5 console without the slash:

| Command | Action |
|---|---|
| `/sk` or `/sk list` | numbered list of skeletons (newest first): name, weapon, HP, distance, state |
| `/sk follow` | all skeletons follow you |
| `/sk stay` | all skeletons stay in place |
| `/sk dismiss` | dismiss all skeletons |

The commands do not require `devcommands`.

> Commands affect skeletons in the area loaded by the game (near the player).

## Configuration

File: `BepInEx/config/xmordax.valheim.skeletoncommander.cfg`. Changes to the file take effect after
restarting the game; with [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
(F1 by default) you can change them in-game.

| Section | Option | Default | Description |
|---|---|---|---|
| `Hotkeys` | `Follow` | `F + LeftAlt` | follow hotkey |
| `Hotkeys` | `Stay` | `G + LeftAlt` | stay hotkey |
| `Hotkeys` | `Dismiss` | `H + LeftAlt` | dismiss hotkey |
| `Hotkeys` | `List` | `J + LeftAlt` | list-in-chat hotkey |
| `Summon` | `TeamEnabled` | `true` | use the team slots; off = random weapons as in the base game |
| `Summon` | `TeamSlot1`–`TeamSlot4` | `SwordAndShield`, `Bow`, `SwordAndShield`, `Bow` | your team; a summon limit of N uses slots 1 to N (`Random` = any weapon) |
| `Summon` | `DismissBeyondDistance` | `100` | skeletons farther away (or outside the simulated area) are dismissed, also after a teleport or death (20–120 m) |
| `Notifications` | `Death` | `true` | message when a skeleton dies |
| `HUD` | `ShowCounter` | `true` | skeleton counter below the minimap |
| `HUD` | `ShowDetails` | `true` | detailed skeleton list below the counter |
| `HUD` | `ShowStateSummary` | `true` | following / staying counts next to the counter |
| `HUD` | `ShowTeam` | `true` | team status, e.g. `sword & shield 1/2 · bow 2/2` |
| `HUD` | `ShowName` | `true` | skeleton names in the detailed list |
| `HUD` | `ShowWeapon` | `true` | weapons in the detailed list |
| `HUD` | `ShowHealth` | `true` | HP in the detailed list |
| `HUD` | `ShowDistance` | `true` | distance from you in the detailed list |
| `HUD` | `ShowShield` | `true` | barrier from the Staff of Protection (left / total, seconds) in the detailed list |
| `HUD` | `ShowState` | `true` | following / staying in the detailed list |
| `HUD` | `CounterPositionY` | `0.3` | vertical counter position (0 = top, 1 = bottom of the screen) |
| `HUD` | `Style` | `Bars` | detailed list as panels with HP bars (`Bars`) or one text line per skeleton (`Text`) |
| `HUD` | `FontSize` | `18` | HUD font size; the whole box scales with it (10–40) |
| `HealthBars` | `AlwaysShow` | `true` | health bars always visible |
| `HealthBars` | `MaxDistance` | `40` | health bar visibility range (10–100 m) |
| `HealthBars` | `ColorByHealth` | `true` | color health bars by HP |
| `Glow` | `Enabled` | `true` | glow around skeletons |
| `Glow` | `Color` | `66D9FFFF` | glow color (RGBA, hex) |
| `Glow` | `Intensity` | `1.2` | glow brightness (0–5) |
| `Glow` | `Range` | `3` | glow range (0.5–10 m) |

Hotkeys are written as the main key followed by modifiers, e.g. `F + LeftAlt`. Key names are Unity
`KeyCode` values (e.g. `Keypad1`, `F7`, `Mouse3`); `None` disables the hotkey.

## License

[MIT](LICENSE)
