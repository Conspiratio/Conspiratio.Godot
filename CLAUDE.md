# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Godot 4.7 (C#/.NET 8) client for **Conspiratio**, a free 2D medieval strategy game. This is a migration of the legacy WinForms client to Godot. UI texts, domain terms, commit messages, and the CHANGELOG are in **German** — keep new UI texts and domain naming German.

Three sibling projects (additional working directories):

- `D:\Projekte\C# Projekte\Conspiratio.Lib` — all game logic (netstandard2.0), consumed here as NuGet package `Conspiratio.Lib`.
- `D:\Projekte\C# Projekte\Conspiratio.WinForms` — the legacy client. **Reference implementation** for anything not yet migrated; do not add features there.
- `D:\Projekte\C# Projekte\Conspiratio.Wiki.wiki` — the game's wiki/documentation.

## Build & Run

```powershell
dotnet build            # compile the C# assembly (from repo root)
```

Headless smoke test (verifies scene loading and all `_Ready()` wiring; clean output = success). Godot lives at `C:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe`:

```powershell
& $godot --headless --path . --import                          # (re)import resources, register new scene UIDs
& $godot --headless --path . --quit "res://scenes/Main.tscn"   # instantiate scene, run _Ready(), quit
```

There are no automated tests in this repo. Playing requires the Godot 4.7 (.NET) editor; scenes (`.tscn`) and `project.godot` are edited there. When editing `.tscn` files textually, be careful: signal connections and NodePath exports live there.

### Conspiratio.Lib dependency

The Lib is pulled from a **local NuGet feed**: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\bin\Debug` (configured in the user-level `NuGet.Config`; the Lib has `GeneratePackageOnBuild`). To change game logic:

1. Edit the Lib, bump `<Version>` in `Conspiratio.Lib.csproj`, build it (`dotnet build` in the Lib solution) — this drops the new `.nupkg` into the feed folder.
2. Update the `Conspiratio.Lib` `PackageReference` version in `Conspiratio.Godot.csproj`.

Game logic belongs in the Lib, not here. The established pattern: extract logic from the WinForms client into a Lib manager class (e.g. `NewGameManager`), then build a thin Godot view on top.

**Design fidelity is mandatory**: migrated screens must reproduce the WinForms original's look & feel — background graphics, icons, control positions (extract them from `Main.Designer.cs` and the "Controls ausrichten"/"Markierungen festlegen" regions in `Main.cs`; coordinates are based on 1366×768 bzw. 1024×768 and scale via `NormB`/`NormH` to our fixed 1600×900) and interaction patterns (NumericButton digit clicking, right-click semantics). Convert needed images from `Conspiratio.WinForms\Conspiratio\Images\` (TIF → PNG); already converted assets live in `assets/images/` and `assets/backgrounds/`.

## Architecture

### Scene flow

`Title.tscn` (main scene, intro) → right-click/Esc → `Main.tscn`. `Main.tscn` hosts `Mainmenu` plus all dialogs/menus as initially hidden sibling nodes (`YesNoDialog`, `LocalGameDialog`, `NewLocalGameMenu`, …). Navigation works by showing/hiding these nodes, not by changing scenes. `Main.cs` looks up the dialog nodes and exposes them as public fields for cross-dialog navigation.

### The SW facade (Lib)

The Lib exposes static game state via `SW` (`Conspiratio.Lib.Gameplay.Spielwelt`):

- `SW.Statisch` — static game data (must be initialized via `SW.Statisch.Initialisieren()` before starting a game)
- `SW.Dynamisch` — mutable game/session state (players, game name, settings)
- `SW.UI` — UI abstraction: the Lib calls dialogs only through interfaces (`IYesNoQuestion`, `IShowText`, `IPolitischeWeltkarteDialog`, …)

Godot dialog scripts implement these interfaces and are registered once in `Main.cs` via `SW.UI.Initialisieren(...)`. Most slots are still `null` (TODO) — implementing a missing dialog means: create scene + script implementing the Lib interface, then pass it in `Main.cs`.

### Conventions & patterns

- **Scripts** live in `assets/scripts/` (namespace mirrors folders: `Conspiratio.Godot.assets.scripts[.controls|.managers]`), **scenes** in `scenes/`. Godot signal handlers use the snake_case naming from the editor (`_on_button_start_game_pressed`) and are connected in the `.tscn`, not in code.
- **Node wiring**: `[Export] public NodePath XPath { get; set; }` + `GetNode<T>(XPath)` in `_Ready()`.
- **Async dialogs**: dialogs return `Task<DialogResultGame>` via a `TaskCompletionSource` that is resolved when a button closes the dialog (see `YesNoDialog.cs`). Callers `await SW.UI.YesNoQuestion.ShowDialogText(...)`.
- **Show/hide pattern**: dialogs implement `ShowAndEnableInput()` / `HideAndDisableInput()` (visibility + `SetProcessInput`). The input action `ui_next_or_close` (right mouse button or Esc) closes/cancels the active dialog — every dialog handles it in `_Input`.
- **Sounds**: `SoundManager` is an autoload singleton (`SoundManager.Instance`). UI controls must use the sound-enabled variants in `scenes/controls/` (`ButtonWithSounds`, `CheckBoxWithSounds`, `LinkButtonWithSounds`) instead of plain Godot controls.
- **Theming**: global theme at `assets/resources/standard_theme.tres`. The **design resolution is 1600×900** — all scenes are laid out in these coordinates; `canvas_items` stretch (aspect `keep`) scales the whole UI to the actual window size, so never write resolution-dependent code.

### Versioning / changelog

Version lives in `project.godot` (`application/config/version`, e.g. `0.1.0.0-godot`). `CHANGELOG.md` is maintained bilingually (DE and EN sections) — update both when adding user-visible changes.
