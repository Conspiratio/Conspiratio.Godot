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

### Verifying a change (the standard loop)

Since there are no tests and the game needs the editor to play, changes are verified in three complementary ways:

1. **Console harness for Lib managers** — before wiring a new Lib manager into a view, verify it in isolation with a throwaway console project that references the same `Conspiratio.Lib` version and exercises the manager against a real game state:
   ```csharp
   SW.Statisch.Initialisieren();
   var ngm = new NewGameManager(@"C:\temp");
   ngm.CreateNewGame("Test", 1, false, true, false, out _);
   var setup = new PlayerSetupManager(); setup.Starte();
   setup.ErstelleSpieler("Alrik", true, 3, SW.Statisch.GetRelKathID(), 5, true, 1); setup.Beende();
   SW.Dynamisch.SetAktiverSpieler(1);
   // ... now construct and print the manager's output
   ```
   (`using Conspiratio.Lib.Allgemein;` for the game-setup managers.)
2. **Headless smoke test** (above) — catches broken scene loading, missing UIDs, and `_Ready()` NodePath wiring errors after every change.
3. **Preview render** for a new dialog's layout — instantiate the dialog scene from a temporary `_preview_*.cs`/`.tscn`, set up a game (as in step 1), call its `ShowDialog(...)`, wait a few `ProcessFrame`s, then `GetViewport().GetTexture().GetImage().SavePng("user://preview_*.png")` and read the PNG back. **Run this one windowed (no `--headless`)** — headless has no rendering server and produces a blank/erroring image. Delete the `_preview_*` files afterward.

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
- **Async dialogs**: dialogs return `Task<DialogResultGame>` (or `Task`) via a `TaskCompletionSource` that is resolved when a button closes the dialog (see `YesNoDialog.cs`). Callers `await SW.UI.YesNoQuestion.ShowDialogText(...)` or `await _main.SomeDialog.ShowDialog(...)`.
- **Parchment dialog pattern**: most content dialogs are a `NinePatchRect` named `Rahmen` using `BackgroundDialog.png` (`uid://cq1th2hp46uxa`) with `patch_margin` 15 on all sides, dark text `Color(0.16, 0.11, 0.05)`, on the shared theme (`uid://bkne0d4c2vxts`). `ShowDialog()` shows the node + `SetProcessInput(true)` + returns a fresh `TaskCompletionSource.Task`; `_Input` closes on `ui_next_or_close`. Copy an existing one (`StatistikDialog`, `StadtInformationenDialog`) rather than starting fresh. For data-driven icon grids, author static labels in the `.tscn` and add the icons in code from the manager's data, scaling the WinForms Designer coordinates by a constant factor into the parchment.
- **`TextureRect` sizing gotcha**: the default `ExpandMode.KeepSize` forces the texture's native size as the control minimum. To size an icon freely, set `ExpandMode = IgnoreSize` **before** assigning `Size` (object-initializer order matters — the initializer runs before post-construction assignments).
- **Settings**: `ClientSettings` (`assets/scripts/managers/ClientSettings.cs`) wraps a Godot `ConfigFile` at `user://client.cfg` (audio volumes, feature toggles like `StatistikAnzeigen`/`TippsAnzeigen` that gate optional turn events). Audio runs through buses `Musik`/`Effekt`/`Stimmen` defined in `default_bus_layout.tres`; `AudioEinstellungen` applies the saved volumes.
- **Show/hide pattern**: dialogs implement `ShowAndEnableInput()` / `HideAndDisableInput()` (visibility + `SetProcessInput`). The input action `ui_next_or_close` (right mouse button or Esc) closes/cancels the active dialog — every dialog handles it in `_Input`.
- **Sounds**: `SoundManager` is an autoload singleton (`SoundManager.Instance`). UI controls must use the sound-enabled variants in `scenes/controls/` (`ButtonWithSounds`, `CheckBoxWithSounds`, `LinkButtonWithSounds`) instead of plain Godot controls.
- **Theming**: global theme at `assets/resources/standard_theme.tres`. The **design resolution is 1600×900** — all scenes are laid out in these coordinates; `canvas_items` stretch (aspect `keep`) scales the whole UI to the actual window size, so never write resolution-dependent code.

### Versioning / changelog

Version lives in `project.godot` (`application/config/version`, e.g. `0.1.0.0-godot`) — not bumped per feature. `CHANGELOG.md` is maintained bilingually (DE and EN sections) — update both when adding user-visible changes. The Lib keeps its own `CHANGELOG.md` (single German+English entry per `<Version>`).

A feature that touches both repos is committed **Lib first, then Godot**: the two are separate git repos on their own feature branches (Lib on `feature/player-setup-manager`, Godot on `feature/new-game`), and the Godot commit's subject references the Lib version it depends on (e.g. `… (Conspiratio.Lib 3.46.0)`). Commit only when asked.
