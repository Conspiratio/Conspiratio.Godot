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

**Long or possibly hanging Godot runs**: use `Start-Process $godot -ArgumentList … -Wait -PassThru
-RedirectStandardOutput out.log -RedirectStandardError err.log` instead of piping — the pipe form yields no
exit code, and a run that never quits blocks the tool until its timeout. Failures and stack traces go to
**stderr**, so read both files. Kill a stuck one with
`Get-Process -Name "Godot_v4.7.1-stable_mono_win64" | Stop-Process -Force`.

Godot writes UTF-8 and PowerShell renders it as `Ã¶`/`â†’` — filter logs on ASCII-safe fragments
(`bestanden`, `^  - `), not on umlauts or the `→` arrow.

Playing requires the Godot 4.7 (.NET) editor; scenes (`.tscn`) and `project.godot` are edited there. When editing `.tscn` files textually, be careful: signal connections and NodePath exports live there.

**Where the tests are:** this repo has none — the game rules are covered by xUnit tests in the Lib
(`Conspiratio.Lib.Tests`, run with `dotnet test`). Since logic belongs in the Lib anyway, **a new feature
is normally accompanied by tests there**, not here. What stays manual on this side is the presentation:
scene loading, layout and interaction. `.github/workflows/build.yml` guards the mechanical part on every
push — it builds and runs the headless smoke test, which also fails when the referenced
`Conspiratio.Lib` version is not published on nuget.org.

### Verifying a change (the standard loop)

The game needs the editor to play, so changes are verified in three complementary ways:

1. **Rules go into a Lib test** (`Conspiratio.Lib.Tests`) — that is where a new manager's behaviour is
   pinned down for good. For a quick look before the test exists, a throwaway console project referencing
   the same `Conspiratio.Lib` version can exercise the manager against a real game state:
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
   - A windowed run only exits when your script calls `GetTree().Quit()`; if it hangs, **don't pipe its stdout through `head`/`grep`** (that blocks and shows nothing). Read the app's own log instead: `%APPDATA%\Godot\app_userdata\Conspiratio.Godot\logs\conspiratio.log`. Screenshots land next to it in `app_userdata\Conspiratio.Godot\`.
   - To capture a state that needs input, press buttons programmatically: `button.EmitSignal(BaseButton.SignalName.Pressed)`. For multi-step flows, shoot on a fixed interval and press whatever is on screen, then pick the interesting frames.

4. **Automated play-through** (`scenes/E2eTest.tscn`) — plays a whole game headless and fails with exit
   code 1 if the flow stalls or the state goes implausible. Runs in CI on every push:
   ```bash
   godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=10 --spieler=2 --verbose
   ```
   It creates the game **through the menus, like a player does** — main menu → "local game" → name the
   game and pick the player count → per player name, gender, religion and banner (`--ohne-menue` falls
   back to building it straight through the Lib managers). That is the only way those twelve entry
   screens get exercised at all. Then it drives the client: every
   overlay dialog is in the group **`Dialogs`**, so the driver finds whatever is open without knowing
   it — pressing the first visible button, or sending `ui_next_or_close`. New dialogs are covered
   automatically. Per turn it also tours the Kontor areas (`AreaHandel`, `AreaSchreibstube`,
   `AreaKirche`, `AreaHinterzimmer`, `AreaKampf` — *not* `AreaFenster`, which ends the turn), asserts
   that each one actually opens a screen, and presses two of that area's buttons — reaching trade,
   applications, credit, espionage and the rest; the home city is opened from the map. After the last
   year it does a `SpeicherManager` save/load round-trip. A 10-year, 2-player run covers 24–26 distinct
   actions in 26–70 s. `--ohne-bereiche` / `--ohne-aktionen` / `--ohne-speichern` switch those off.
   Four things to know before touching it — the rest is commented at the point in `E2eTreiber.cs`
   where it matters:
   - A dialog counts as *waiting* only when it is visible **and** `IsProcessingInput()` — except menu
     screens, which run on button signals and never enable `_Input`
     (`WarteAufSichtbar(..., brauchtEingabe: false)`). Not every dialog has a button either: where none
     is found the driver fills the first `LineEdit` and emits `TextSubmitted` (`GeburtDialog`).
   - **The CI runner has no `user://client.cfg`**, so every `ClientSettings` default applies there and
     nowhere else — the tips dialog is shown on a fresh machine but was off locally, which is why it only
     ever broke in CI. To reproduce such a case, move the file aside (and put it back: it is the user's
     settings). Out of that dialog leads no button at all, only a right-click, so the driver alternates
     `MaxKlicksProDialog` button presses with one `ui_next_or_close` — always, not just inside an action.
   - Synthetic input needs press **and** release, and never the same dialog every frame — staged
     sequences wait for the button to be *released*. Synthetic **mouse** events don't reach the maps
     headless at all; both maps fall back to the entry point the real click uses (`Stadt.ShowStadt`,
     `SoeldnerRaeuberKarte.WaehleStuetzpunkt`).
   - Budget generously: `MaxSchritte` is 20 000 frames per wait because a hot-seat duel runs over a
     minute and headless counts far more frames per second than the screen does.
   - **Any "it hangs" needs the state report before a fix.** The driver prints open dialogs, visible
     screens, the end-turn button and every player's talers when it gives up; four separate hangs were
     decided from that output alone, and every attempt to guess instead failed. Two of them were the
     driver fighting itself — answering its own "really end the turn?" at random, and an exit counter
     that reset on each dialog *change* while two dialogs ping-ponged 6 060 times.

   Per turn the driver also plays a **trade round** in the home city — set the slot to "produce", pick the
   workshop's resource, staff it with `ArbeiterProRunde` workers on one site, and sell the stock by
   clicking the resource symbol. That is the core loop; without it the run earns nothing and everything
   hanging off wealth (settlement, taxes, credit, debtors' tower, mission goals) runs on unrealistic
   numbers. It runs even with `--ohne-aktionen`, because it is the game's core, not a random action.
   Measured over 15 years, trade only: 8 workers move 2 088 goods and end near break-even, while 25
   workers *lose* money — wages outgrow the proceeds, and selling in the producing city fetches poor
   prices. Exporting to another city is the profitable path and is not yet driven.
   Setting a `NumericButton`'s `Wert` does **not** emit `WertChanged` — only digit entry does; the driver
   emits it explicitly, otherwise the city never learns of the change.

   Runs are reproducible: `--seed=N` replays a run exactly (measured: identical down to the click
   count), `--seed=0` draws one and prints it. The seed is set **twice**, once before setup and once the
   game stands, so a change to the setup path no longer invalidates noted seeds — it cannot equalise the
   two setup paths though, since the starting state comes out of the setup itself. Sound is muted at
   runtime (`--mit-ton` re-enables). "Report a problem" is blocked: it zips a report and opens the system
   mail client, which tests the environment, not the game.

5. **Visual acceptance** — the same play-through with `--screenshots` (or `--bilder=<dir>`) drops a few
   states per view as PNGs, giving a visual record of every screen. This is what catches a shifted layout
   or a clipped label that no assertion notices. **Needs a renderer, so not headless** — the headless
   driver is a dummy that never draws; the driver says so and skips instead of writing black images.
   ```bash
   xvfb-run -a --server-args="-screen 0 1600x900x24" \
     godot --path . --rendering-method gl_compatibility --rendering-driver opengl3 \
     "res://scenes/E2eTest.tscn" -- --jahre=4 --spieler=2 --bilder="$PWD/e2e-bilder"
   ```
   Locally, drop `xvfb-run` and the driver flags. CI runs this on every push and uploads the images as
   the `ansichten` artifact (also on failure — that is when they are most useful).
   - **`gl_compatibility` renders pixel-identical to Vulkan here** (measured: 0 of 160 200 sampled pixels
     differ, max channel delta 1) because the client is pure 2D. So the CI renderer costs nothing
     visually, and Vulkan stays the local default.
   - Not yet established: llvmpipe (software rasterizer) versus a real GPU. Expected identical or ±1 for
     2D blits, but if you ever add reference images for a regression diff, **generate them in CI**, not
     locally — otherwise the comparison is between two rasterizers rather than two code states.
   - `MaxBilderProAnsicht` caps the images per view; without it a news screen with dozens of messages
     would produce dozens of near-identical PNGs.

6. **Weekly long runs** (`.github/workflows/weekly.yml`) — six 40-year games every Sunday, three on fixed
   seeds (regression) and three on random ones (`--seed=0`, which prints the value it drew so a striking
   run can be replayed). Length alone reaches what a 10-year push run never does: measured in one
   40-year game — `SpielerTodDialog` 2×, `KindestodDialog` 3×, `GeburtDialog` 4×, `Kirchgang` 20×,
   `TestamentDialog` 29×. Still not reached even there: `GerichtDialog`, `WahlDialog`,
   `StuetzpunktVerwalten`, `AuftragSiegDialog` — those need a game *state* the driver does not set up
   (an indictment, a running candidacy, an owned base), not more years.
   Runs `--verbose` on purpose: when something fails only after an hour, the log is the only trace and
   a second attempt costs another hour. Logs are uploaded either way, so you can read off which events
   a run actually hit. Weekly rather than nightly for cost: six runs are roughly 40 CI minutes, so daily
   would eat over 1 200 of the 2 000 free minutes a month; weekly leaves room for the push runs.

**Two testing habits that repeatedly paid off:**

- **Pin randomized inputs, then use large samples.** Much game state is randomized per game (KI `Bosheit`, and thus opponent strength). Comparing runs without pinning it produces swings that look like real regressions. Pin the inputs, and for probabilistic behaviour assert on rates over a few thousand runs, not on single outcomes.
- **Regression-compare a refactor against the original.** When extracting logic that already exists (e.g. mirroring the office conditions of `PrivilegienAktualisieren` in a new method), have the harness compare old and new over the whole input space — that turns "hopefully equivalent" into a check that also catches later drift.

### Conspiratio.Lib dependency

The Lib is consumed as the **released NuGet package from nuget.org**. The repo's `nuget.config` pins the source to nuget.org only (`<clear/>` drops any inherited feeds, e.g. a local Lib feed in the user-level `NuGet.Config`), so the build is reproducible for every contributor. To pick up new game logic, bump the `Conspiratio.Lib` `PackageReference` version in `Conspiratio.Godot.csproj` to a version that is published on nuget.org.

To **iterate on the Lib locally before a release**: the Lib builds with `GeneratePackageOnBuild`, dropping a `.nupkg` into `…\Conspiratio.Lib\bin\Debug`. Because this repo's `nuget.config` resolves nuget.org only, the reliable way to let Godot see an unreleased version is to **pre-populate the global NuGet cache** from the console harness, which may use the local feed:

```bash
# 1. bump <Version> in Conspiratio.Lib.csproj, then build (writes the .nupkg)
dotnet build
# 2. purge the cached copy — NuGet will not re-extract a version it already has
rm -rf ~/.nuget/packages/conspiratio.lib/<version>
# 3. restore the harness from the local feed; this fills the global cache
dotnet restore --source "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib/bin/Debug"
# 4. now bump the PackageReference in Conspiratio.Godot.csproj and build normally
```

Step 2 is the one that bites: rebuilding the Lib without purging the cache leaves the old code in place, and the Godot build silently keeps using it. Don't commit a local source into the checked-in `nuget.config`.

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

Godot dialog scripts implement these interfaces and are registered once in `Main.cs` via `SW.UI.Initialisieren(...)`. Implementing a further dialog means: create scene + script implementing the Lib interface, then pass it in `Main.cs`.

**Add new `SW.UI` interfaces as optional trailing parameters** of `UIHelper.Initialisieren` (`IDuellDialog duellDialog = null, IErpressungDialog erpressungDialog = null`). The WinForms client calls the same method and must keep compiling; callers in the Lib therefore have to tolerate a `null` slot and fall back to a plain text message (see `KontrahentenManager` case 14). A dialog that has no visuals of its own can be a bare `Node` in `Main.tscn` that adapts the interface onto an existing dialog — `ErpressungDialog` just forwards to `YesNoDialog`.

### Lib domain model — facts that are easy to get wrong

Discovered the hard way; check these before designing around an assumption:

- **Office and territory belong together.** Use `SetAmt(amtId, gebietId)`, never `SetAmtID` alone. An office without its territory makes `GetAmtNameUndOrt()` and `AmtVonXfreigeben()` dereference a null territory. This is also why a test harness must not "just" re-assign a freed office.
- **Savegame compatibility is a lazy-init convention.** Serialization is field-based and bypasses constructors, so any field added later arrives as `null` (or an array of the old length) from an existing savegame. The pattern is an accessor that creates it on demand — `Spieler.BegingVerbrechenSicher()`, `HumSpieler.GetAhnentafelListe()`, `GetErpressungen()`. Never touch such a field directly.
- **The law table has fixed capacity 100 and is sparsely populated**: Finanz `0–4`, Straf `20–25`, Kirche `40–44`, with the block boundaries in `GetGesetzgrenzeFinanz/Straf/Kirche`. Adding a law therefore costs nothing and needs no savegame migration — take a free index inside the right block, and court (`SammleVorwuerfe`) and `PrivJurist` pick it up automatically via those boundaries.
- **Spy evidence is a point total, not a count.** `AktiveSpionagen.GetDelikte()` accumulates the *strength* of each find (1–4, worded by `BeweisStaerkeText`), while the concrete accusations live per law in `Spieler.GetBegingVerbrechenX(i)`. Use the total for thresholds, the per-law delicts for listing charges.
- **Privileges are gated by office but execute generically.** All office dependence sits in the single `if`-chain of `DynamischeSpieldaten.PrivilegienAktualisieren()`; the 35 `Priv*.cs` act on `SW.Dynamisch.GetAktHum()` and only `PrivEinkommen` reads the office. So granting someone another player's office privileges needs no change to the privileges themselves — see `GetAmtsPrivilegien`.
- **Synthetic privilege entries** (no Lib privilege behind them, the client intercepts the ID) use IDs ≥ 10000, safely above `GetMaxPriv()`: `10000` Ahnentafel, `10001` Mätresse, `10002` Fechtunterricht, `10003` Duell, `10099` „Eigene Privilegien", `10100 + opferId` per blackmail.
- **Person-map targeting modes** are routed by `Weltkarte.ShowDialogModus(mod)`: `0–5` back-room actions (`5` = blackmail), `8` trial, `12` poisoned wine, `13` executioner, `14` duel. A new mode touches four places: the `case` in `KontrahentenManager.PersonWasMachen`, the header in `AemterEbeneManager.GetTitel`, and the "close after a single action" lists in `Weltkarte.NachAemterEbene` and `AemterEbeneDialog`.

### Conventions & patterns

- **Scripts** live in `assets/scripts/` (namespace mirrors folders: `Conspiratio.Godot.assets.scripts[.controls|.managers]`), **scenes** in `scenes/`. Godot signal handlers use the snake_case naming from the editor (`_on_button_start_game_pressed`) and are connected in the `.tscn`, not in code.
- **Node wiring**: `[Export] public NodePath XPath { get; set; }` + `GetNode<T>(XPath)` in `_Ready()`.
- **Async dialogs**: dialogs return `Task<DialogResultGame>` (or `Task`) via a `TaskCompletionSource` that is resolved when a button closes the dialog (see `YesNoDialog.cs`). Callers `await SW.UI.YesNoQuestion.ShowDialogText(...)` or `await _main.SomeDialog.ShowDialog(...)`.
- **Parchment dialog pattern**: most content dialogs are a `NinePatchRect` named `Rahmen` using `BackgroundDialog.png` (`uid://cq1th2hp46uxa`) with `patch_margin` 15 on all sides, dark text `Color(0.16, 0.11, 0.05)`, on the shared theme (`uid://bkne0d4c2vxts`). `ShowDialog()` shows the node + `SetProcessInput(true)` + returns a fresh `TaskCompletionSource.Task`; `_Input` closes on `ui_next_or_close`. Copy an existing one (`StatistikDialog`, `StadtInformationenDialog`) rather than starting fresh. For data-driven icon grids, author static labels in the `.tscn` and add the icons in code from the manager's data, scaling the WinForms Designer coordinates by a constant factor into the parchment.
- **`TextureRect` sizing gotcha**: the default `ExpandMode.KeepSize` forces the texture's native size as the control minimum. To size an icon freely, set `ExpandMode = IgnoreSize` **before** assigning `Size` (object-initializer order matters — the initializer runs before post-construction assignments).
- **Labels don't clip by default.** Without `autowrap_mode`, a `Label` draws straight past its rect instead of wrapping or truncating — side-by-side columns then overprint each other (this was the „Informationen zur Wahl" bug). For any label holding data of unknown length, set `autowrap_mode` and prefer a `VBoxContainer` over absolutely positioned columns. Conversely `clip_text = true` silently cuts long text off — that was the truncated duel message.
- **Controls instantiated in code inherit the parchment theme** (dark brown text), which is invisible on a dark full-screen background. Give such buttons explicit theme overrides (`AddThemeColorOverride("font_color", …)`, `font_outline_color`, `outline_size`) and `SizeFlagsHorizontal = SizeFlags.ShrinkCenter` to centre them in a `VBoxContainer`. See `DuellDialog.StyleAuswahlKnopf`.
- **Choice buttons in a dialog**: reuse the pattern from `GerichtDialog.WaehleOption` — instantiate `LinkButtonWithSounds.tscn` into a `VBoxContainer`, resolve a `TaskCompletionSource<int>` from `Pressed`, then clear the box. While such a choice is open, `ui_next_or_close` must do nothing (a button press is required); guard for it in the dialog's `OnNextOrClose`.
- **Settings**: `ClientSettings` (`assets/scripts/managers/ClientSettings.cs`) wraps a Godot `ConfigFile` at `user://client.cfg` (audio volumes, feature toggles like `StatistikAnzeigen`/`TippsAnzeigen` that gate optional turn events). Audio runs through buses `Musik`/`Effekt`/`Stimmen` defined in `default_bus_layout.tres`; `AudioEinstellungen` applies the saved volumes.
- **Show/hide pattern**: dialogs implement `ShowAndEnableInput()` / `HideAndDisableInput()` (visibility + `SetProcessInput`). The input action `ui_next_or_close` (right mouse button or Esc) closes/cancels the active dialog — every dialog handles it in `_Input`.
- **Sounds**: `SoundManager` is an autoload singleton (`SoundManager.Instance`). UI controls must use the sound-enabled variants in `scenes/controls/` (`ButtonWithSounds`, `CheckBoxWithSounds`, `LinkButtonWithSounds`) instead of plain Godot controls.
- **Theming**: global theme at `assets/resources/standard_theme.tres`. The **design resolution is 1600×900** — all scenes are laid out in these coordinates; `canvas_items` stretch (aspect `keep`) scales the whole UI to the actual window size, so never write resolution-dependent code.

### Versioning / changelog

Version lives in `project.godot` (`application/config/version`, e.g. `1.0.0-godot`) — not bumped per feature. `CHANGELOG.md` is maintained bilingually (DE and EN sections) — update both when adding user-visible changes. The Lib keeps its own `CHANGELOG.md`: new changes are appended (bilingual, DE and EN bullets) under a single `## [Unreleased]` heading — **no** per-change version header or date. The Lib `<Version>` in the csproj may still be bumped per change; the `[Unreleased]` block is only cut into a dated `## <Version>` section when a real GitHub release is made.

A feature that touches both repos is committed **Lib first, then Godot**: the two are separate git repos, each on its own feature branch, and the Godot commit's subject references the Lib version it depends on (e.g. `… (Conspiratio.Lib 3.46.0)`). Commit only when asked.

Two mechanics that have gone wrong before:

- **Write commit messages with a POSIX heredoc**, `git commit -F - <<'EOF' … EOF`. The Bash tool is Git Bash, not PowerShell: a PowerShell here-string (`@'…'@`) is not parsed and ends up prefixing a stray `@` to the subject line.
- **For multi-line or non-ASCII edits use a Python heredoc**, not `sed -i`: `python - <<'PY' … PY` with an
  `assert old in s` before each replace fails loudly when the anchor moved, while `sed` silently does
  nothing and its `s|…|` breaks on `|` or umlauts in the replacement. There is no PyYAML here.
- **Don't reach for `git add -A` blindly.** The Lib working copy carries untracked files that are not part of the current change (e.g. `CONTRIBUTING.md`); stage the paths you touched, or check `git status` before committing and unstage the rest.
