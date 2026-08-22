---
name: lib-version-weitergeben
description: Hebt die Conspiratio.Lib auf eine neue Version, baut sie, schiebt sie durch den globalen NuGet-Cache und stellt den Godot-Client darauf um - inklusive Nachweis, dass der Client wirklich die neue Fassung nutzt.
disable-model-invocation: true
---

# Lib-Version weitergeben

Bringt eine Änderung aus `Conspiratio.Lib` in den Godot-Client. Der Ablauf ist mechanisch, aber
**lautlos fehleranfällig**: NuGet extrahiert eine Version, die es schon kennt, nicht neu — wird der
Cache-Eintrag nicht gelöscht, baut der Client stillschweigend weiter gegen den alten Code. Ohne
Fehlermeldung, ohne Warnung. Genau deshalb gibt es dieses Skill.

**Argument:** die Zielversion, z. B. `4.2.1`. Ohne Argument die aktuelle Version aus
`Conspiratio.Lib/Conspiratio.Lib.csproj` lesen und die Patch-Stelle um eins erhöhen.

## Repos

- Lib: `D:\Projekte\C# Projekte\Conspiratio.Lib`
- Client: `C:\Projekte\Godot\Conspiratio.Godot`

## Ablauf

Führe `scripts/weitergeben.sh <version>` aus diesem Skill-Verzeichnis aus. Das Skript macht die
Schritte 1–5 und bricht ab, sobald einer fehlschlägt.

1. `<Version>` in `Conspiratio.Lib/Conspiratio.Lib.csproj` setzen
2. Lib bauen (`dotnet build` erzeugt das `.nupkg` selbst, `GeneratePackageOnBuild`)
3. `~/.nuget/packages/conspiratio.lib/<version>` löschen — **der Schritt, der übersprungen wird**
4. Cache über ein Wegwerf-Konsolenprojekt mit echter `PackageReference` füllen
5. `PackageReference` im Client setzen, bauen, Smoke-Test

Danach von Hand, weil es Urteil braucht:

6. **CHANGELOG der Lib**: Stichpunkte zweisprachig (DE **und** EN) unter den **einen** bestehenden
   `## [Unreleased]`-Block. Kein Versionsheader, kein Datum.
7. Commits: **erst Lib, dann Client**; der Client-Commit nennt die Lib-Version im Betreff.
   Nachricht per POSIX-Heredoc (`git commit -F - <<'EOF' … EOF`) — ein PowerShell-Here-String hängt
   ein `@` an die Betreffzeile.
8. **Kein `git add -A`.** Beide Arbeitsbäume tragen untrackte Fremddateien. (Ein Hook blockiert das
   inzwischen, verlass dich aber nicht darauf.)

## Warum ein Wegwerfprojekt und nicht das Testprojekt

`Conspiratio.Lib.Tests` hängt per **`ProjectReference`** an der Lib, nicht per `PackageReference`. Ein
`dotnet restore` dort läuft sauber durch und lässt den globalen Cache **unberührt** — ein stiller
No-op. Das stand einmal falsch in einem Plan und kostete eine Runde Fehlersuche.

## Der Nachweis ist Teil der Aufgabe

Nimm nicht an, dass die neue Fassung angekommen ist — **belege es**. Das Skript prüft zwei Dinge:

- `.godot/mono/temp/obj/project.assets.json` löst `Conspiratio.Lib/<version>` auf
- die SHA-256 der in den Build-Output kopierten `Conspiratio.Lib.dll` stimmt mit der frisch gebauten
  überein

Schlägt eine der Prüfungen fehl, ist Schritt 3 oder 4 nicht durchgelaufen. **Nicht weitermachen** und
nicht committen.

## Was danach rot bleibt

Der CI-Build des Clients schlägt fehl, solange die Version nicht auf nuget.org veröffentlicht ist —
`nuget.config` ist bewusst auf nuget.org festgelegt. Das ist erwartetes Verhalten während der
Feature-Arbeit, kein Fehler.
