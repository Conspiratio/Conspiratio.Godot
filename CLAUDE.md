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

**`--import` rewrites this file's indentation.** Measured: every `godot --headless --path . --import`
converts leading spaces to tabs on ~29 lines of `CLAUDE.md` — reproducibly, the same lines each time,
and only this file (other `.md` files, including far more deeply indented ones, are untouched).
`dotnet build` and the smoke test do not do it, and the `.editorconfig` does not prevent it. The change
is **always whitespace-only**, so discard it rather than committing it:

```bash
git diff -w --stat CLAUDE.md   # empty output = content unchanged, safe to discard
git checkout -- CLAUDE.md
```

This matters beyond cosmetics: a leading tab on the continuation line of a list item can be parsed as a
code block and breaks the list. Eight such lines were committed unnoticed before this was traced.

Playing requires the Godot 4.7 (.NET) editor; scenes (`.tscn`) and `project.godot` are edited there. When editing `.tscn` files textually, be careful: signal connections and NodePath exports live there.

**Where the tests are:** this repo has none — the game rules are covered by xUnit tests in the Lib
(`Conspiratio.Lib.Tests`, run with `dotnet test`). Since logic belongs in the Lib anyway, **a new feature
is normally accompanied by tests there**, not here. What stays manual on this side is the presentation:
scene loading, layout and interaction. `.github/workflows/build.yml` guards the mechanical part on every
push — it builds and runs the headless smoke test, which also fails when the referenced
`Conspiratio.Lib` version is not published on nuget.org.

### Verifying a change (the standard loop)

The game needs the editor to play, so changes are verified in several complementary ways:

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
   `--aggressivitaet=N` (1–100) overrides the AI-aggressiveness setting for the run; without it the
   game's default of 50 applies, which is the behaviour every other measurement here assumes. Every
   setting from 1 to 100 completes all years with exit 0 — what rises with the setting is the **dialog
   burden**, not the difficulty of the outcome: final wealth is dominated by per-seed noise. Figures in
   [`docs/e2e-messwerte.md`](docs/e2e-messwerte.md) (measured **before** the trade balancing).
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
   workshop's resource, staff it, and sell the stock by clicking the resource symbol or exporting. That is
   the core loop; without it the run earns nothing and everything hanging off wealth (settlement, taxes,
   credit, debtors' tower, mission goals) runs on unrealistic numbers. It runs even with `--ohne-aktionen`,
   because it is the game's core, not a random action.

   **Site count is derived from whichever of two hard caps binds** (`ErmittleSinnvolleStaetten`), not from
   a fixed constant — the first version used one fixed site and left both ceilings far below capacity:
   - **Storage**: `HumSpieler.ErmittleLagerplatzInStadt` × `Rohstoff.GetLagermengeProQMeter` is the most the
     workshop can hold; production beyond it is still paid for and then lost (`BuchManager`: "was nicht
     eingelagert werden konnte, geht verloren").
   - **Workers**: `HandelsManager.SetzeProduktionsArbeiter` silently clamps at `StatischeSpieldaten.
     GetMaxArbeiterAnzahl()` (99 per slot) — sites beyond what 99 workers can staff lose yield
     proportionally (`Produktionsslot.GetProduktion`'s `benArbeiter` ratio) while costs keep climbing.
   Workers are always set to `sites × (Rohstoff.GetArbeiter() / Rohstoff.GetWerkstaetten())` — the exact
   ratio the goods demand; a fixed worker count is either understaffed or measurably loses money once
   overstaffed. **Storage is expanded from surplus** (`LagerraumManager`, largest offer that leaves
   the reserve standing) but **only while storage is the binding cap** — once the worker
   cap binds instead, extra storage is dead capital: an earlier version that kept buying regardless turned
   a positive 15-year result negative.
   **The reserve has to grow with the business** (`BerechneRuecklage`): it mirrors `AbrechnungsManager`
   — workers × `GetWSArbeiterpreis` plus sites × `GetWSEinzelpreis` — and doubles that, the surcharge
   standing in for the cost blocks that cannot be predicted without booking them (sales tax, tithe,
   tolls, interest, Hofhaltung). `RuecklageMindestens` = 1 500 is only the floor now. As a flat 1 500 it
   was independent of business size, and the driver ended up insolvent nearly every year: the debtor's
   tower is an **absorbing state** — a jail year costs the turn, hence the trade round, hence the income
   that would pay the debt off. Three of ten seeds never escaped. The courtship is gated on twice the
   reserve for the same reason: it was spending exactly the cushion the trade round had left.

   **The measurement history lives in [`docs/e2e-messwerte.md`](docs/e2e-messwerte.md)**, not here — it
   is a chain in which each layer invalidates the one before, and keeping it in this file made the
   current state hard to find. What carries over as method:
   - **Read a band, never a single number.** The seed dominates everything else; cross-seed differences
     below ~10 000 talers mean nothing. The current band (Schicht 5, ten seeds) is **+3 900 to
     +53 000**, mean 24 862, median 22 480, none negative, against a pre-project mean of ~72 000.
   - **Never compare across a change that reshapes the random stream** — re-measure instead. The
     restored AI year-change (`KIAktionenDurchfuehren`) alone adds ~300 000 draws per year (390 AIs ×
     390 relationships), so no seed keeps its old outcome. Paket A/B reshaped it again on top of that:
     seed 1234 went from 68 392 to 41 805 without any trading change, which is why Schicht 4 is a band
     over ten seeds rather than a before/after pair.
   - A run's wealth is **not** comparable per game year: the driver always plays `--jahre` turns, but
     `Gespielte Jahre` can exceed that because a debtor's-tower year costs a turn without playing one.
   - **An E2E run cannot evaluate the late-game balancing, however long it is.** Over 40 years the band
     is +38 000 to +178 000 (ten seeds, median 125 177) and every run flattens into a steady state —
     income covers costs and nothing more. The ceiling is the driver's strategy, not the game: the trade
     round works **one** workshop slot in **one** city, and `GetMaxArbeiterAnzahl()` caps that at 99
     workers. Paket A/B bites far above it — Gesetz #3 starts at 2 to 6 million (16× the ceiling), and
     the workshop surcharge beyond `MaxSteigerungsstufen` needs a 21st business where the driver owns
     one. The gap is wider than "one slot in one city" suggests: `GetMaxProdSlots()` is **2 per
     city**, so the game allows up to 28 production lines across the 14 cities and the driver drives
     one of them. Measuring those mechanics is what the balancing harness (item 7) is for.
   - **Only `--ohne-aktionen` measures the game; the default settings measure the driver.** Same seeds,
     15 years: +49 710 without actions against −19 033 with them (1 player), +15 147 against −11 798
     (2 players) — the sign flips with the switch, in both player counts. With actions the driver burns
     its click budget on random purchases and stops finishing the trade round (local sales collapse from
     5 000–8 500 to 241–300). A default-settings run is a fine regression signal and a worthless
     balancing statement. Player count, by contrast, is real: halving the result is market saturation on
     a shared market.
   - **The driver marries and writes a will**, and both run even with `--ohne-aktionen` — same reason
     as the trade round: dynastic continuity is the game's core, not a random action. Without it no run
     survived past ~25 years, because `FuehreTestamentAus` reads `GetErbeSpielerID()` and **children do
     not inherit by themselves**: a marriage alone still ends the dynasty at the first death. With both,
     40-year runs complete (measured: 73/44/41 played years). Two traps that cost a test round each —
     an heir has to be *designated*, and the will dialog must be closed by its own close button, because
     a right-click only lands a frame later and the generic dialog handling meanwhile pages the freshly
     chosen heir onward, all the way back to "no heir".
   - **A legitimate game over is not a hang.** `ErkenneSpielende()` reads it off the screen — the client
     hides the Kontor in the game-over branch, which uncovers the Mainmenu again — and the run then ends
     cleanly with a "Spiel beendet" line instead of an error. Guarded by `_spielLaeuft`, since game setup
     through the menus shows exactly the same picture.

   Three traps a re-measurement closed, worth not falling into again:
   - **`GetWerkstattVerkaufspreis` must not inherit the purchase-price scaling.** The factor
     `SteigerungProzent` per owned workshop multiplies the *goods'* base price (tier 1 = 2 000,
     tier 2 = 10 000, tier 3 = 40 000) while the counter spans the whole realm, so a scaled resale
     price depended on how many *other* workshops were owned. Selling cheap tier-1 sites, buying a
     tier-3 site at the low counter, buying the cheap ones back and reselling the tier-3 site netted
     **+758 314 per cycle**, repeatable — more than a whole 15-year game earns. Single-tier tests never
     catch it (a same-tier cycle correctly loses 6.25 %); a regression test has to mix tiers.
   - **The export path must book the sold quantity into the destination city.** The WinForms original
     books it into the city of origin, which was harmless only while the Godot client never called
     `RohBedarfAktRundenEnde`. With that call in place, the destination never saturated while export
     volume pushed the price down in the player's own city — where the driver also sells on the spot.
   - **The round-end economy has to run on every path where the Lib advances the year**, not just the
     regular end-of-turn block — a count would go stale the moment another such path turns up, and one
     already has: a debtor's-tower turn and a heirless death both leave `ZeigeZugnachrichten` early
     while the Lib still bumps the year, and leaving the game as the last player of the year through the
     ingame menu (`SpielerEntferntWeiter`) does the same inside `IngameMenuDialog.ShowDialog` — the
     client only finds out afterwards, by comparing the year before and after the dialog, since the
     removal happens inside the Lib call and cannot be reconstructed from client state. All of them
     call `Kontor.FuehreWirtschaftlichesRundenendeDurch` — in a single-player game one skipped call
     froze the whole economy for that year.

   **Selling into one city no longer scales.** `Stadt.GetRohstoffPreisVonIDX` discounts the price by how
   many years of local demand (`Einwohner / 10`) sit unsold in that city's stock, capped at
   `Stadt.MaxAbschlagProzent`. Sustainable volume is therefore bounded by the population you actually
   supply, and the discount now reaches **below** `preisMin` — that floor only bounds the base price in
   the setters. Workshops also get progressively more expensive
   (`HandelsManager.SteigerungProzent` per workshop already owned, capped at `MaxSteigerungsstufen`,
   which is overflow protection rather than balancing).

   **The round-end economy calls were missing entirely** until this change: `RohBedarfAktRundenEnde`
   (sales into city stock, population consumption), `RohPreiseRandomSchwanken` (prices moved not at all)
   and `RundenBestechungenAbwickeln` were in the Lib but called from no Godot script — the WinForms
   original runs them in `RundenEndnachrichtenAnzeigen`. They now run in `Kontor.cs`, together with the
   new `EinwohnerWachstumAktRundenEnde`. **Any measurement taken before this is not comparable**: the
   goods cycle simply did not turn.

   **Cities develop now.** `_einwohner` and `_reichtum` previously only ever fell
   (`KatastrophenManager`), which would have made the saturation discount harsher every year while the
   consumption that drains stock shrank with it. Population growth is driven by `Reichtum` and
   `Kriminalitaet` plus noise, bounded by `MindestEinwohner`/`MaxEinwohner` — the lower bound is
   load-bearing, since multiplicative growth can never lift a city off zero. Wealth rises on a
   trade-volume-driven yearly dice roll (`ReichtumWachstumAktRundenEnde`), which **must run before**
   `RohBedarfAktRundenEnde` — that one consumes and zeroes the per-city sales figures it reads.

   That closes a loop worth knowing about: heavy trade enriches a city, which both makes storage
   expansion there dearer (`LagerraumManager` scales with `Reichtum`) and speeds its population growth,
   which raises the annual demand the saturation discount divides by. Developing one market over years
   is therefore a real alternative to moving on — bounded on both ends by `GetMaxReichtum()` and
   `MaxEinwohner`.

   **A full E2E run (default settings, random actions and AI-aggression sabotage included) can still end
   negative** even with this strategy — that is not a trading regression: per-seed swings from those
   intentionally-random systems reach tens of thousands of talers and dominate the outcome (see the
   `--aggressivitaet` measurements above). Isolating that from trading requires `--ohne-aktionen`, since
   `--ohne-bereiche` also skips the home-city visit and reports zero trade.
   Both ways of selling are driven, alternating by year so they don't collide (the export only ships at
   turn end, while selling on the spot clears the stock immediately). The export's own caravan fee (a base
   charge plus a rate per started 100 units, `BerechneProdKosten`) still eats into its margin at these
   volumes relative to selling on the spot — not re-measured against the new strategy.
   Setting a `NumericButton`'s `Wert` does **not** emit `WertChanged` — only digit entry does; the driver
   emits it explicitly, otherwise the city never learns of the change.

   **Seeds no longer replay a run exactly.** `--seed=N` pins the game's own draws and `--seed=0` draws
   one and prints it, but the outcome still varies between identical invocations: three runs of an
   unmodified working tree (`--jahre=15 --spieler=1 --ohne-aktionen --seed=1234`) gave **1 390 / 1 390
   / 58 520** talers at **209 / 209 / 1 072** dialog clicks. The third diverges at click [122], where
   the courtship dialog offers a different gift although the logs are identical line for line up to
   there — so something consumes randomness outside the recorded action sequence. The cause is not
   diagnosed; it is **not** caused by any recent change (measured on unmodified HEAD).
   **So never read a change off a seed pair.** Measure both states in the same session over several
   seeds with repeated runs each, and compare the distributions — that is what Schicht 6 in
   [`docs/e2e-messwerte.md`](docs/e2e-messwerte.md) does and why it had to re-measure its own
   baseline. The seed is still set **twice**, once before setup and once the game stands, so a change
   to the setup path no longer invalidates noted seeds — it cannot equalise the two setup paths
   though, since the starting state comes out of the setup itself. Sound is muted at
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
   `TestamentDialog` 29×. Four views need a game *state* rather than more years — `GerichtDialog`,
   `WahlDialog`, `StuetzpunktVerwalten`, `AuftragSiegDialog` — and **`--zustaende` now builds those
   states and asserts them**: it arranges an indictment, an office that makes the player an elector, a
   bought base and a mission victory, and fails the run naming any view it did not reach. It is **off by
   default and must never be set in a measurement run** — cheat-built state distorts the wealth band —
   needs >= ~12 years, ends the game early through the mission victory, and skips the save/load probe.
   The weekly workflow runs it in its own job (`abdeckung`, two seeds, 20 years, one player),
   deliberately separate from the measuring runs in the same file.
   Two obstacles it had to work around, both worth knowing: an office whose only elector is the Regent
   gets deposed by one hostile AI every year, and `FuehreAmtsenthebungenDurch` runs immediately before
   `HalteWahlenAb` - the player loses the office before ever voting; pick an office with no electors of
   its own (Regent, Erzbischof, Feldmarschall), whose deposition motions expire unvoted. And a base
   purchase does not fail only on money: `KaufangebotAbgeben` rolls a die, and only the amount *above*
   the valuation feeds the acceptance term.
   `AuftragSiegDialog` is unreachable without the switch at all - the setup menu leaves the difficulty
   at `KeinAuftrag` and `CreateNewGame` never sets one, so a driver game has no mission to win. The
   switch therefore assigns the mission as well as fulfilling it: that proves the evaluation chain
   (threshold, victory screen, highscore entry, clean end), not that the menu stores the choice.
   Runs `--verbose` on purpose: when something fails only after an hour, the log is the only trace and
   a second attempt costs another hour. Logs are uploaded either way, so you can read off which events
   a run actually hit. Weekly rather than nightly for cost: six runs are roughly 40 CI minutes, so daily
   would eat over 1 200 of the 2 000 free minutes a month; weekly leaves room for the push runs.

7. **Balancing harness** (`Conspiratio.Lib.Harness`, in the **Lib** repo — a console project with a
   `ProjectReference`, so it iterates without the NuGet cache dance). It builds a rich player directly
   and *computes* the years instead of playing them. That is the only way into the wealth range where
   the late-game brakes act at all:
   ```bash
   dotnet run --project Conspiratio.Lib.Harness -- --staedte=14 --jahre=40 --seeds=5 --start-taler=500000 --export --tabelle
   ```
What it found, and what came of it: measured that way (14 export lines, five seeds, 40 years) the
   pure trader ended at **1.75 to 2.11 million**, median 1.96 — roughly **16x the driver's median** of
   125 177, growing in a straight line of ~50 000 a year that never flattened, with Unterhalt
   contributing only 10 500 of a ~290 000 cost block. That is what the **Kapazitaetsunterhalt**
   (`AbrechnungsManager.KapazitaetsunterhaltProMille`) was built to brake; with it the same run ends at
   a median of **791 318** and the curve reaches a resting point. See
   [`docs/haendlerbremse-konzept.md`](docs/haendlerbremse-konzept.md) for the calibration.
   Switches: `--staedte`, `--slots` (1-2), `--staetten` (throttle per line, 0 = as many as workers and
   storage allow), `--jahre`, `--seeds` (count or list), `--start-taler`, `--export`, `--marktklug`,
   `--adel` (residences and bases), `--aemter` (take the best office by cheat — an upper bound),
   `--wahlen` (the real political cycle: applications, elections, depositions, AI deaths),
   `--vollstaendig`, `--tabelle`, `--ware=N` (put every line on one given resource instead of the
   city's main production), `--arbeiter=voll|exakt|null` (how the lines are staffed),
   `--fertigkeit=N` (production skill of every resource run) and `--lehrgeld` (pay for that
   skill with real lessons instead of being given it).
   - **Its turn order is its most important property**: book, trade, credits, settlement, turn close,
     turn messages, economic round end - mirrored from `Kontor.cs`. Diverge from it and the numbers
     describe a game that does not exist.
   - **Selling where you produce is the worst price in the game.** A city pays least for its own main
     production (Crowbrigde: beer 8, bricks 7 - the lowest values in its row). The first version sold
     locally and therefore lost money in *every* configuration, even at a 1 % market discount. That was
     a wrong point of sale, not a balancing finding, and it very nearly got reported as one. `--export`
     ships to the dearest city instead, which flips the sign.
   - **Enabling a workshop is not buying one.** `KaufeWerkstatt` also sets the starting storage, and
     because storage expansion is multiplicative (`LagerraumManager`: `AktuellerLagerraum * Faktor`) it
     can never lift off 0 - while `Kaufe` still reports success. `BuchManager` then silently discards
     the whole production. Same shape as `MindestEinwohner` in city growth.
   - **`VersuchTitelVerleihen` only earmarks a title**; `TitelVerleihungManager` grants it. Without that
     second step the rank stays put and Hofhaltung sits at 0 forever.
   - Deliberately omitted: death, family, court, random events, catastrophes. They scatter by tens of
     thousands of talers and would bury what is being measured; `--vollstaendig` adds the AI year change
     back for a cross-check.
   - **The seed has to be set before the world is built, not after.** The first version seeded only
     after `CreateNewGame` and player setup, so the world itself was drawn unseeded. The trader path was
     unaffected (four identical runs), the noble path was not: the same command line produced medians of
     266 441, 367 734 and 187 274 across invocations, and the difference was very nearly attributed to a
     code change. The seed is now set twice, as in the E2E driver.
   - **Two paths, measured against each other** (40 years, five seeds, medians), which is what the
     late-game balancing turns on:

     | | wealth | rank | office | Standesansehen |
     |---|---|---|---|---|
     | pure trader | 791 318 | Ritter | none | 60 |
     | `--adel --wahlen` | 384 239 | Graf-Herzog | Zollmeister to Regent | 182-324 |

     Titles are the gate to offices (`GetMinTitelStadtEbene` 1, `GetMinTitelLandEbene` 3,
     `GetMinTitelReichsEbene` 5), and offices pay 700 (Ratsherr) to 50 000 (Regent) a year. **Do not
     read `--aemter` as the noble path**: it takes the best office by cheat and holds it for forty
     years, which produced a median of 1 235 506 and the false conclusion that the noble path is the
     richer one. Office tenure is the whole question, and only `--wahlen` answers it.
   - **A staffing gap it measured, and what the numbers looked like.** `Produktionsslot`
     computed the worker requirement as `sites * (workers / workshops)` — integer division
     inside the parentheses. Wool, hide and rum are the only three goods with a ratio below one
     (one worker per two workshops), so their requirement came out as 0 and they produced in
     full with **no workers and no wages**. Measured over 14 wool lines, 40 years, export: a
     fully or exactly staffed run gives **the same figures to the taler** before and after the
     fix, and the standard configuration still has its median of 791 318 — so the documented
     balancing is untouched. The gain was entirely in exploiting it: with no workers the old
     code reached **−5 469 570 talers at full sales volume** (47 % discount) against
     −6 822 570 when staffed correctly — 1 353 000 over 40 years, ~34 000 a year, at identical
     output, and the same seed ended as a nobleman rather than a burgher. Two lessons beyond
     the fix: **an instrument that always does the sensible thing cannot see an exploit** (both
     the harness and the E2E driver staff every line, so neither would ever have found this —
     `--arbeiter=null` exists to play the part of someone who knows better), and **a difference
     stays readable in a configuration that is otherwise ruinous**: wool alone across fourteen
     cities ends at zero wealth in every variant, but the gap between the variants is purely
     additive and therefore still means something.
   - **The production skill: a small rule with a large lever.** `--fertigkeit` measured what
     `docs/produktionsfertigkeit-konzept.md` had asked for and nobody could answer, because the
     harness did not know the quantity. Given for free, full skill raises the 40-year median
     from 791 318 to **1 312 390 (+66 %)** — far more than the rule's ~5 % more yield suggests,
     because it lifts a thin margin (~320 000 revenue against ~290 000 costs) while the costs
     stay put. **Saturation does not absorb it**, contrary to what the concept expected: the
     market discount barely moves (10/17/18 % against 10/17/11 %), since 5 % more volume spread
     over fourteen cities shifts no stock. **Paid for, the sink holds**: ~378 000 in lessons
     puts the result back into the noise around the unskilled case (+4,7 % at 500 000 starting
     capital, −4,4 % at 900 000, the latter being the fairer comparison because the fee does
     not double as missing working capital). So the calibration rests entirely on **the price
     of acquisition, not on the rule** — and only the lessons route is measured; books, a
     master's writing, lectures and the household scholar are cheaper per point.
   - **What it still cannot do:** its trader never adapts. It keeps producing at full capacity even
     while a levy ruins it, so every brake measurement **overstates the damage** - a human would shrink
     capacity instead. `--marktklug` only throttles to one city's annual demand, which is a different
     strategy, not an adaptive one.

**Three testing habits that repeatedly paid off:**

- **Pin randomized inputs, then use large samples.** Much game state is randomized per game (KI `Bosheit`, and thus opponent strength). Comparing runs without pinning it produces swings that look like real regressions. Pin the inputs, and for probabilistic behaviour assert on rates over a few thousand runs, not on single outcomes.
- **Regression-compare a refactor against the original.** When extracting logic that already exists (e.g. mirroring the office conditions of `PrivilegienAktualisieren` in a new method), have the harness compare old and new over the whole input space — that turns "hopefully equivalent" into a check that also catches later drift.
- **A change that consumes randomness invalidates same-seed before/after comparisons.** The round-end calls draw from the shared `SW.Statisch.Rnd`, so adding them shifts every later draw and the seed no longer replays the same events — the swing that was read as the change's effect was the same order of magnitude as the known noise. Prove such a change on a deterministic path instead (a console harness with no `Rnd` in it), then use direction-consistency across several seeds for the size. Note also that `Gespielte Jahre` can exceed `--jahre` (a jail year costs a turn without playing one) while the turn count stays at `--jahre` — so never normalise wealth per game year.

### Conspiratio.Lib dependency

The Lib is consumed as the **released NuGet package from nuget.org**. The repo's `nuget.config` pins the source to nuget.org only (`<clear/>` drops any inherited feeds, e.g. a local Lib feed in the user-level `NuGet.Config`), so the build is reproducible for every contributor. To pick up new game logic, bump the `Conspiratio.Lib` `PackageReference` version in `Conspiratio.Godot.csproj` to a version that is published on nuget.org.

To **iterate on the Lib locally before a release**: the Lib builds with `GeneratePackageOnBuild`, dropping a `.nupkg` into `…\Conspiratio.Lib\bin\Debug`. Because this repo's `nuget.config` resolves nuget.org only, the reliable way to let Godot see an unreleased version is to **pre-populate the global NuGet cache** from the console harness, which may use the local feed:

```bash
# 1. bump <Version> in Conspiratio.Lib.csproj, then build (writes the .nupkg)
dotnet build
# 2. purge the cached copy — NuGet will not re-extract a version it already has
rm -rf ~/.nuget/packages/conspiratio.lib/<version>
# 3. restore a throwaway project from the local feed; this fills the global cache
dotnet restore --source "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib/bin/Debug"
# That project must reference the Lib as a <PackageReference> at the exact version. A ProjectReference
# (as in Conspiratio.Lib.Tests) restores fine but leaves the global cache untouched — a silent no-op.
# Verify with `ls ~/.nuget/packages/conspiratio.lib/`.
# 4. now bump the PackageReference in Conspiratio.Godot.csproj and build normally
```

Step 2 is the one that bites: rebuilding the Lib without purging the cache leaves the old code in place, and the Godot build silently keeps using it. Don't commit a local source into the checked-in `nuget.config`.

Game logic belongs in the Lib, not here. The established pattern: extract logic from the WinForms client into a Lib manager class (e.g. `NewGameManager`), then build a thin Godot view on top.

**When a mechanic "just never happens", first check whether the Lib call exists at all** — `grep -rn
"<MethodName>" assets/scripts/`. The migration has dropped whole round-end blocks twice now:
`FuehreWirtschaftlichesRundenendeDurch` (prices, stock, bribes) and `KIAktionenDurchfuehren` (AI ageing
→ deaths → vacant offices → elections → trade certificates, plus AI relationship drift). Both presented
as several unrelated-looking bugs. `Main.cs:6355-6379` in WinForms is the reference list of what the
round end owes.

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
- **Node wiring**: inside a scene, `[Export] public NodePath XPath { get; set; }` + `GetNode<T>(XPath)`
  in `_Ready()`. **Dialogs hanging off `Main` need neither**: `Main.VerdrahteKnoten` wires every public
  `Node` field by type name, so a new dialog is just a public field plus a same-named node in `Main.tscn`.
- **Async dialogs**: dialogs return `Task<DialogResultGame>` (or `Task`) via a `TaskCompletionSource` that is resolved when a button closes the dialog (see `YesNoDialog.cs`). Callers `await SW.UI.YesNoQuestion.ShowDialogText(...)` or `await _main.SomeDialog.ShowDialog(...)`.
- **Parchment dialog pattern**: most content dialogs are a `NinePatchRect` named `Rahmen` using `BackgroundDialog.png` (`uid://cq1th2hp46uxa`) with `patch_margin` 15 on all sides, dark text `Color(0.16, 0.11, 0.05)`, on the shared theme (`uid://bkne0d4c2vxts`). `ShowDialog()` shows the node + `SetProcessInput(true)` + returns a fresh `TaskCompletionSource.Task`; `_Input` closes on `ui_next_or_close`. Copy an existing one (`StatistikDialog`, `StadtInformationenDialog`) rather than starting fresh. For data-driven icon grids, author static labels in the `.tscn` and add the icons in code from the manager's data, scaling the WinForms Designer coordinates by a constant factor into the parchment.
- **`TextureRect` sizing gotcha**: the default `ExpandMode.KeepSize` forces the texture's native size as the control minimum. To size an icon freely, set `ExpandMode = IgnoreSize` **before** assigning `Size` (object-initializer order matters — the initializer runs before post-construction assignments).
- **`Font.GetMultilineStringSize` ignores wrapping**, despite taking a width parameter: it counts only
  explicit `\n`. Measuring a wrapped label with it undercounts badly (231 px for an eight-line text that
  needs ~300). To size a container to wrapped text before the first draw, wrap word by word yourself —
  see `YesNoDialog.ZaehleZeilen`.
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
- **For multi-line or non-ASCII edits use a Python script**, not `sed -i`: an `assert old in s` before
  each replace fails loudly when the anchor moved, while `sed` silently does nothing and its `s|…|`
  breaks on `|` or umlauts in the replacement. There is no PyYAML here (Pillow was installed via pip
  for the TIF→PNG conversions). Three things that cost time here, in order of how much:
  - **Write the script to a file and run it**, rather than piping it in as a heredoc. A quoted heredoc
    (`<<'PY'`) is supposed to pass the body through verbatim, but an apostrophe in the text still broke
    the command (`unexpected EOF while looking for matching ‘`), and **a single backslash is eaten one
    level on the way in** — `'\n'` written in the script body arrives at Python as a real newline, so an
    anchor containing a C# `\n` escape never matches. A file has neither problem.
  - **Preserve CRLF.** Both repos use CRLF. Reading with `io.open(p, encoding='utf-8-sig')` normalises
    line endings to `\n`, so writing back with `newline=''` silently converts the whole file to LF and
    turns a three-line change into a full-file diff. Always write with `newline='\r\n'`; check with
    `git diff --stat` that the diff is the size you expect.
  - **Insert whole blocks, not fragments.** Splicing a method in front of a `public` signature landed it
    between that method’s `<summary>` and its `<param>` tags, splitting the XML doc in two. Anchor on the
    start of the doc comment, not on the signature.
- **Don't reach for `git add -A` blindly.** The Lib working copy carries untracked files that are not part of the current change (e.g. `CONTRIBUTING.md`); stage the paths you touched, or check `git status` before committing and unstage the rest.
