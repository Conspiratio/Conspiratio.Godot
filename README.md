<a href="https://discord.gg/dxkC5DPgRY"><img src='https://discordapp.com/api/guilds/779755166017519666/widget.png?style=shield'></a>

_🇩🇪 Deutsch | [🇬🇧 English](README-en.md)_

# Conspiratio.Godot

Godot-Client (C# / .NET 8) für **Conspiratio**, ein freies, quelloffenes 2D-Mittelalter-Strategiespiel.

Dieses Projekt ist die **Migration des bisherigen [WinForms-Clients](https://github.com/Conspiratio/Conspiratio.WinForms) nach Godot** und befindet sich in aktiver Entwicklung. Die gesamte Spiellogik lebt weiterhin in der geteilten Bibliothek `Conspiratio.Lib`; dieses Repository enthält ausschließlich den Godot-Client (Szenen, UI, Steuerung). Neue Bildschirme werden Stück für Stück originalgetreu aus dem WinForms-Client übernommen.

## Mitmachen

Ihr wollt Euch an diesem Projekt beteiligen? Großartig! Tretet einfach mit uns über [Discord](https://discord.gg/dxkC5DPgRY) oder oldschool per <a href="&#109;&#97;&#105;&#108;&#116;&#111;&#58;%6D%61%69%6C%40%63%6F%6E%73%70%69%72%61%74%69%6F%2E%6E%65%74">E-Mail</a> in Kontakt und wir klären die Details.  
_Jegliche Hilfe ist willkommen – Grafik, Programmierung, Musik/Sounds, Konzeption._

## Voraussetzungen

- **Godot 4.7 (.NET/Mono-Variante)** – zum Spielen und Bearbeiten der Szenen (`.tscn`) sowie der `project.godot` wird der Godot-Editor benötigt.
- **.NET 8 SDK** – zum Kompilieren der C#-Assembly.
- **`Conspiratio.Lib`** – die geteilte Spiellogik (siehe unten).

## Bauen & Starten

Die C#-Assembly kompilieren (im Repository-Wurzelverzeichnis):

```powershell
dotnet build
```

Gespielt bzw. bearbeitet wird über den Godot-Editor. Das Hauptszene ist `res://scenes/Title.tscn` (Intro) → Rechtsklick/Esc → `res://scenes/Main.tscn`.

Ein schneller Rauchtest ohne Editor (lädt die Hauptszene headless und führt die `_Ready()`-Verdrahtung aller Dialoge aus – saubere Ausgabe bedeutet Erfolg):

```powershell
& $godot --headless --path . --import                          # Ressourcen (neu) importieren, Szenen-UIDs registrieren
& $godot --headless --path . --quit "res://scenes/Main.tscn"   # Szene instanziieren, _Ready() laufen lassen, beenden
```

Automatisierte Tests gibt es in diesem Repository nicht.

### Abhängigkeit Conspiratio.Lib

Die gesamte Spiellogik befindet sich in `Conspiratio.Lib` (netstandard2.0) und wird hier als NuGet-Paket eingebunden. Das Paket wird über einen **lokalen NuGet-Feed** bezogen (`Conspiratio.Lib/bin/Debug`, konfiguriert in der benutzerweiten `NuGet.Config`; die Lib baut mit `GeneratePackageOnBuild`). Zum Ändern der Spiellogik:

1. In `Conspiratio.Lib` bearbeiten, `<Version>` in `Conspiratio.Lib.csproj` erhöhen und bauen – dabei entsteht ein neues `.nupkg` im Feed-Ordner.
2. Die `PackageReference`-Version von `Conspiratio.Lib` in `Conspiratio.Godot.csproj` anpassen.

Grundsatz: **Spiellogik gehört in die Lib, nicht in den Client.** Das etablierte Muster ist, die Logik aus dem WinForms-Client in eine Lib-Manager-Klasse zu extrahieren und darauf eine dünne Godot-Ansicht zu bauen.

## Git-Workflow

**Wichtig: Wir committen und pushen nie direkt in den `main`-Branch!**  
Der Grund ist Transparenz und das 4-Augen-Prinzip bzw. die Kontrolle durch mindestens einen weiteren Entwickler.

Für jede Änderung wird daher ein neuer, persönlicher Branch erstellt. Der Branchname beginnt mit einem der folgenden Präfixe, gefolgt von einem Schrägstrich:

- `improvement` (Verbesserung von Code oder einer Funktion im Spiel, auch Refaktorisierungen)
- `fix` (Korrektur)
- `feature` (neue Funktion des Spiels)

_Beispiel:_ `fix/absturz-bei-ueberfall`

Umlaute, Sonderzeichen und Leerzeichen bitte im Branchnamen vermeiden (stattdessen Bindestriche verwenden). Ist der Branch stabil und vollständig, wird per Pull Request der Merge nach `main` beantragt und einem anderen Entwickler zur Prüfung zugewiesen.

Betrifft eine Änderung sowohl die Lib als auch den Client, wird **zuerst die Lib, dann der Godot-Client** committet; der Godot-Commit nennt in der Betreffzeile die zugehörige Lib-Version (z. B. `… (Conspiratio.Lib 3.46.0)`).

## Code-Richtlinien

Für C# orientieren wir uns an den [Microsoft C# Coding Conventions](https://learn.microsoft.com/de-de/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Sprache:** Kommentare und die meisten Bezeichner sind **deutsch**, da die gesamte bestehende Codebasis deutsch aufgebaut ist. Standard-Präfixe wie `Get`/`Set` sind selbstverständlich in Ordnung (`GetUmsatzProSpieler`), rein englische Fachbegriffe (`GetVolumeOfSalesPerPlayer`) sollten vermieden werden. Auch neue UI-Texte und Domänenbegriffe bleiben deutsch.

**Godot-Konventionen:** Szenen liegen unter `scenes/`, Skripte unter `assets/scripts/` (Namespace spiegelt die Ordner). Knoten werden über `[Export] NodePath …` + `GetNode<T>(…)` in `_Ready()` verdrahtet; Signal-Handler tragen die snake_case-Namen aus dem Editor und werden in der `.tscn` verbunden. UI-Steuerelemente nutzen die Sound-Varianten aus `scenes/controls/` (`ButtonWithSounds`, `CheckBoxWithSounds`, …). Die Design-Auflösung ist **1600×900**; die Oberfläche skaliert über den `canvas_items`-Stretch – keine auflösungsabhängige Logik schreiben.

**Design-Treue:** Migrierte Bildschirme sollen das Aussehen und die Bedienung des WinForms-Originals reproduzieren (Hintergründe, Icons, Positionen, Interaktionen). Benötigte Grafiken werden aus dem WinForms-Client (`Conspiratio.WinForms/Conspiratio/Images/`) nach PNG konvertiert.

## Changelog

Der Changelog wird in [CHANGELOG.md](CHANGELOG.md) direkt im Wurzelverzeichnis gepflegt und ist **zweisprachig** (deutscher und englischer Abschnitt je Version). Jede nutzersichtbare Änderung wird dort dokumentiert; wir orientieren uns an [Keep a Changelog](https://keepachangelog.com/de/1.0.0/). Die Client-Version steht in `project.godot` (`application/config/version`).

## Dokumentation

Umfangreiche Features und interessante Klassen/Methoden werden im [GitHub-Wiki](https://github.com/Conspiratio/Conspiratio.Wiki/wiki) dokumentiert.

## Lizenz

Dieses Projekt steht unter der **GNU General Public License v3.0** – siehe [LICENSE](LICENSE). Die gesamte Spiellogik in `Conspiratio.Lib` sowie der WinForms-Client stehen unter derselben Lizenz.

## Musik- und Sound-Credits

Der Godot-Client verwendet dieselben freien Musikstücke und Sounds wie der WinForms-Client. Wir bedanken uns dafür bei:

### Musik

- [Jason Shaw](https://audionautix.com/) (Lizenz: [Creative Commons Attribution 4.0 International License](https://creativecommons.org/licenses/by/4.0)) für
  - Noel, 30 Second Classical, Tempting Fate, The Deadly Year, The Talk, Big Intro #2, Enemy Ships, Epic TV Theme, Hero In Peril, High Tension, Imperial Morning, Night Runner, Remember The Heroes, Stalking Prey, Warfare Bed, Pendulum Waltz, Road To Kilcoo, The Angels Weep, Egyptian Crawl, Temptation March, Wheel Of Karma, Parting Glass, A Tall Ship, Legends Of The River, Renaissance, Sundays Child, The Master, The Mighty Kingdom, Triumphant Return, Troubled Bridges, Turkish Dance, What Child Is This, The Big Decision
- [Strobotone](https://freemusicarchive.org/music/Strobotone/) (Lizenz: [Attribution-NoDerivatives 4.0 International License](https://creativecommons.org/licenses/by-nd/4.0)) für
  - Medieval Theme 02
- [Michael Ziege](https://siggiziege.de/) für
  - Conspiratio Theme - Idee 01

### Sounds

- [csmag](https://freesound.org/people/csmag/) (Lizenz: [Creative Commons 0 License](https://creativecommons.org/publicdomain/zero/1.0/)) – Synth1 Bongo-01.wav, Synth1 Bongo-02.wav
- [BraveFrog](https://freesound.org/people/BraveFrog/) (Lizenz: [Creative Commons 0 License](https://creativecommons.org/publicdomain/zero/1.0/)) – coins.wav
- [sarson](https://freesound.org/people/sarson/) (Lizenz: [Creative Commons 0 License](https://creativecommons.org/publicdomain/zero/1.0/)) – Arras.wav
- [florian_reinke](https://freesound.org/people/florian_reinke/) – click1.wav
- [bevibeldesign](https://freesound.org/people/bevibeldesign/) – Trumpet Fanfare
