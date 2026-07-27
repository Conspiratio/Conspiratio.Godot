<a href="https://discord.gg/dxkC5DPgRY"><img src='https://discordapp.com/api/guilds/779755166017519666/widget.png?style=shield'></a>

_[🇩🇪 Deutsch](README.md) | 🇬🇧 English_

# Conspiratio.Godot

Godot client (C# / .NET 8) for **Conspiratio**, a free and open-source 2D medieval strategy game.

This project is the **migration of the previous [WinForms client](https://github.com/Conspiratio/Conspiratio.WinForms) to Godot** and is under active development. All game logic still lives in the shared `Conspiratio.Lib` library; this repository contains the Godot client only (scenes, UI, input). New screens are ported faithfully from the WinForms client one at a time.

## Getting involved

Want to contribute to this project? Great! Just get in touch via [Discord](https://discord.gg/dxkC5DPgRY) or, old-school, by <a href="&#109;&#97;&#105;&#108;&#116;&#111;&#58;%6D%61%69%6C%40%63%6F%6E%73%70%69%72%61%74%69%6F%2E%6E%65%74">e-mail</a> and we'll work out the details.  
_Any help is welcome – graphics, programming, music/sounds, design._

## Requirements

- **Godot 4.7 (.NET/Mono edition)** – required to play and to edit the scenes (`.tscn`) and `project.godot`.
- **.NET 8 SDK** – required to compile the C# assembly.
- **`Conspiratio.Lib`** – the shared game logic (see below).

## Build & Run

Compile the C# assembly (from the repository root):

```powershell
dotnet build
```

Playing and editing happens through the Godot editor. The main scene is `res://scenes/Title.tscn` (intro) → right-click/Esc → `res://scenes/Main.tscn`.

A quick smoke test without the editor (loads the main scene headless and runs the `_Ready()` wiring of all dialogs – clean output means success):

```powershell
& $godot --headless --path . --import                          # (re)import resources, register scene UIDs
& $godot --headless --path . --quit "res://scenes/Main.tscn"   # instantiate scene, run _Ready(), quit
```

There are no automated tests in this repository.

### Conspiratio.Lib dependency

All game logic lives in `Conspiratio.Lib` (netstandard2.0) and is consumed here as the **official NuGet package from [nuget.org](https://www.nuget.org/packages/Conspiratio.Lib/)**. The `nuget.config` checked into the repo pins nuget.org as the only package source (`<clear/>` drops any inherited feeds), so the build is reproducible for every contributor. To pick up new game logic, bump the `Conspiratio.Lib` `PackageReference` version in `Conspiratio.Godot.csproj` to a version published on nuget.org.

To **develop the library locally before a release**: the library builds with `GeneratePackageOnBuild` and drops a `.nupkg` into `…/Conspiratio.Lib/bin/Debug`. Add that folder as a package source temporarily while developing (a local `<add>` in `nuget.config` or the user-level `NuGet.Config`) and remove it afterwards – don't commit the local source into the checked-in `nuget.config`.

Principle: **game logic belongs in the library, not in the client.** The established pattern is to extract the logic from the WinForms client into a library manager class and build a thin Godot view on top of it.

## Git workflow

**Important: we never commit or push directly to the `main` branch!**  
The reason is transparency and the four-eyes principle, i.e. review by at least one other developer.

For every change a new, personal branch is created. The branch name starts with one of the following prefixes, followed by a slash:

- `improvement` (improving code or a game feature, including refactorings)
- `fix` (a correction)
- `feature` (a new game feature)

_Example:_ `fix/crash-on-raid`

Please avoid umlauts, special characters and spaces in branch names (use hyphens instead). Once the branch is stable and complete, a pull request to merge into `main` is opened and assigned to another developer for review.

When a change affects both the library and the client, commit **the library first, then the Godot client**; the Godot commit references the corresponding library version in its subject line (e.g. `… (Conspiratio.Lib 3.46.0)`).

## Coding guidelines

For C# we follow the [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Language:** comments and most identifiers are **German**, because the entire existing codebase is built in German. Standard prefixes such as `Get`/`Set` are of course fine (`GetUmsatzProSpieler`), but purely English domain terms (`GetVolumeOfSalesPerPlayer`) should be avoided. New UI texts and domain terms stay German as well.

**Godot conventions:** scenes live under `scenes/`, scripts under `assets/scripts/` (the namespace mirrors the folders). Nodes are wired via `[Export] NodePath …` + `GetNode<T>(…)` in `_Ready()`; signal handlers keep the snake_case names from the editor and are connected in the `.tscn`. UI controls use the sound-enabled variants from `scenes/controls/` (`ButtonWithSounds`, `CheckBoxWithSounds`, …). The design resolution is **1600×900**; the UI scales via the `canvas_items` stretch – do not write resolution-dependent logic.

**Design fidelity:** migrated screens should reproduce the look and feel of the WinForms original (backgrounds, icons, positions, interactions). Required graphics are converted from the WinForms client (`Conspiratio.WinForms/Conspiratio/Images/`) to PNG.

## Changelog

The changelog is maintained in [CHANGELOG.md](CHANGELOG.md) at the repository root and is **bilingual** (a German and an English section per version). Every user-visible change is documented there; we follow [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). The client version lives in `project.godot` (`application/config/version`).

## Documentation

Larger features and noteworthy classes/methods are documented in the [GitHub wiki](https://github.com/Conspiratio/Conspiratio.Wiki/wiki).

## License

This project is licensed under the **GNU General Public License v3.0** – see [LICENSE](LICENSE). The entire game logic in `Conspiratio.Lib` as well as the WinForms client are licensed under the same terms.

## Music and sound credits

The Godot client uses the same free music and sounds as the WinForms client. Our thanks go to:

### Music

- [Jason Shaw](https://audionautix.com/) (license: [Creative Commons Attribution 4.0 International License](https://creativecommons.org/licenses/by/4.0)) for
  - Noel, 30 Second Classical, Tempting Fate, The Deadly Year, The Talk, Big Intro #2, Enemy Ships, Epic TV Theme, Hero In Peril, High Tension, Imperial Morning, Night Runner, Remember The Heroes, Stalking Prey, Warfare Bed, Pendulum Waltz, Road To Kilcoo, The Angels Weep, Egyptian Crawl, Temptation March, Wheel Of Karma, Parting Glass, A Tall Ship, Legends Of The River, Renaissance, Sundays Child, The Master, The Mighty Kingdom, Triumphant Return, Troubled Bridges, Turkish Dance, What Child Is This, The Big Decision
- [Strobotone](https://freemusicarchive.org/music/Strobotone/) (license: [Attribution-NoDerivatives 4.0 International License](https://creativecommons.org/licenses/by-nd/4.0)) for
  - Medieval Theme 02
- [Michael Ziege](https://siggiziege.de/) for
  - Conspiratio Theme - Idee 01

### Sounds

- [csmag](https://freesound.org/people/csmag/) (license: [Creative Commons 0 License](https://creativecommons.org/publicdomain/zero/1.0/)) – Synth1 Bongo-01.wav, Synth1 Bongo-02.wav
- [BraveFrog](https://freesound.org/people/BraveFrog/) (license: [Creative Commons 0 License](https://creativecommons.org/publicdomain/zero/1.0/)) – coins.wav
- [sarson](https://freesound.org/people/sarson/) (license: [Creative Commons 0 License](https://creativecommons.org/publicdomain/zero/1.0/)) – Arras.wav
- [florian_reinke](https://freesound.org/people/florian_reinke/) – click1.wav
- [bevibeldesign](https://freesound.org/people/bevibeldesign/) – Trumpet Fanfare
