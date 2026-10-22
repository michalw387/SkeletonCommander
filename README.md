# Skeleton Tracker

A Valheim mod (BepInEx 5) that makes it easier to track and command the skeletons summoned with the
**Dead Raiser** staff. The mod is client-side only and affects only the skeletons you summoned.

## Features

- **Minimap pins** – each of your skeletons gets a map pin with the skeleton trophy icon, updated
  every 0.5 s. On the large map the pin label shows its state: following or staying.
- **HUD counter** – below the minimap: how many skeletons you have and how many are following you
  or staying. Optionally a detailed, numbered list: name, weapon (bow or sword & shield), HP (colored
  by health), distance and state. Skeletons are sorted newest first, so the oldest one is at the bottom;
  the numbers match the chat list.
  The HUD is part of the game's interface, so the inventory and menus cover it like the minimap.
- **Health bars** – always visible above your skeletons' heads (up to 40 m by default), colored by
  HP: green above 60%, yellow above 30%, red below.
- **Glow** – a subtle light around your skeletons so they are easy to tell apart.
- **Death notifications** – an on-screen message when one of your skeletons dies.
- **Summon loadout** – choose whether newly summoned skeletons get a sword and shield or a bow
  (random by default), in the config or with a hotkey. The weapon stays the same after reloading and
  is the same for other players.
- **Commands and hotkeys** – follow, stay and dismiss for all skeletons at once, or only for the one
  you are aiming at (the hover text shows the hotkeys).

## Requirements

- Valheim (PC)
- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (BepInEx 5)

## Installation

1. Install BepInExPack Valheim following its instructions and start the game once so BepInEx
   creates its folders.
2. Copy `ValheimSkeletonTracker.dll` to:
   ```
   <Valheim folder>/BepInEx/plugins/SkeletonTracker/
   ```
3. Start the game. On first launch the config file
   `BepInEx/config/michal.valheim.skeletontracker.cfg` is created.

To uninstall, delete the `BepInEx/plugins/SkeletonTracker/` folder (and optionally the `.cfg` file).

## Controls

### Hotkeys

| Hotkey (default) | Action |
|---|---|
| Left Alt + F | skeletons follow you |
| Left Alt + G | skeletons stay in place |
| Left Alt + H | dismiss skeletons |
| Left Alt + J | list skeletons in chat |
| Left Alt + K | switch the weapons of your next summoned skeletons (Random → SwordAndShield → Bow) |

When you aim at one of your skeletons, the Follow / Stay / Dismiss hotkeys affect only that skeleton
(the key hints appear in its hover text). Hotkeys are ignored while typing in chat and in menus,
the inventory and the large map.

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

File: `BepInEx/config/michal.valheim.skeletontracker.cfg`. Changes to the file take effect after
restarting the game; with [ConfigurationManager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
(F1 by default) you can change them in-game.

| Section | Option | Default | Description |
|---|---|---|---|
| `Hotkeys` | `Follow` | `F + LeftAlt` | follow hotkey |
| `Hotkeys` | `Stay` | `G + LeftAlt` | stay hotkey |
| `Hotkeys` | `Dismiss` | `H + LeftAlt` | dismiss hotkey |
| `Hotkeys` | `List` | `J + LeftAlt` | list-in-chat hotkey |
| `Hotkeys` | `CycleNextSummonLoadout` | `K + LeftAlt` | hotkey switching `Summon.NextSummonLoadout` |
| `Summon` | `NextSummonLoadout` | `Random` | weapons of the skeletons you summon next: `Random`, `SwordAndShield` or `Bow` |
| `Notifications` | `Death` | `true` | message when a skeleton dies |
| `HUD` | `ShowCounter` | `true` | skeleton counter below the minimap |
| `HUD` | `ShowDetails` | `true` | detailed skeleton list below the counter |
| `HUD` | `CounterPositionY` | `0.3` | vertical counter position (0 = top, 1 = bottom of the screen) |
| `HealthBars` | `AlwaysShow` | `true` | health bars always visible |
| `HealthBars` | `MaxDistance` | `40` | health bar visibility range (10–100 m) |
| `HealthBars` | `ColorByHealth` | `true` | color health bars by HP |
| `Glow` | `Enabled` | `true` | glow around skeletons |
| `Glow` | `Color` | `66D9FFFF` | glow color (RGBA, hex) |
| `Glow` | `Intensity` | `1.2` | glow brightness (0–5) |
| `Glow` | `Range` | `3` | glow range (0.5–10 m) |

Hotkeys are written as the main key followed by modifiers, e.g. `F + LeftAlt`. Key names are Unity
`KeyCode` values (e.g. `Keypad1`, `F7`, `Mouse3`); `None` disables the hotkey.

## Building from source

Requires the .NET SDK (the project targets `netstandard2.1`).

1. Copy the game and BepInEx libraries into the `lib/` folder (they are not part of the repository):
   - from `<Valheim>/valheim_Data/Managed/`: `assembly_valheim.dll`, `assembly_utils.dll`,
     `assembly_guiutils.dll`, `Splatform.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`,
     `UnityEngine.TextRenderingModule.dll`, `UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`,
     `Unity.TextMeshPro.dll`
   - from `<Valheim>/BepInEx/core/`: `BepInEx.dll`, `0Harmony.dll`
2. Build:
   ```
   dotnet build
   ```
   After compiling, the DLL is copied automatically to the plugins folder set by the
   `ValheimPluginsDir` property in `ValheimSkeletonTracker.csproj`. You can pass a different path
   when building:
   ```
   dotnet build -p:ValheimPluginsDir=/path/to/Valheim/BepInEx/plugins/SkeletonTracker/
   ```
