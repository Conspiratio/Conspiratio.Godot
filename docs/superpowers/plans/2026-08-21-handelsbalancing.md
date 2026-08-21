# Handelsbalancing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den unbegrenzten Skalierungsvorteil mehrerer Kontore bremsen — über einen sättigungsabhängigen Warenpreis und einen nach Besitzstand gestaffelten Werkstatt-Kaufpreis.

**Architecture:** Zwei unabhängige Formeländerungen in bestehenden Lib-Methoden, beide reine Funktionen über bereits vorhandenem Zustand. `Stadt.GetRohstoffPreisVonIDX` misst den Mengenabschlag künftig in Jahren lokalen Bedarfs statt in fixen Talern; `HandelsManager.GetWerkstattKaufpreis` staffelt nach reichsweit besessenen Betrieben. Es kommt kein serialisiertes Feld hinzu, also keine Savegame-Migration.

**Tech Stack:** C# / netstandard2.0 (`Conspiratio.Lib`), xUnit (`Conspiratio.Lib.Tests`), Godot 4.7 / .NET 8 für die E2E-Messung.

**Spec:** `docs/superpowers/specs/2026-08-21-handelsbalancing-design.md` (im Godot-Repo)

## Global Constraints

- **netstandard2.0** in der Lib — keine modernen BCL-APIs.
- **Ganzzahlarithmetik** in beiden Formeln, damit E2E-Läufe seed-reproduzierbar bleiben. Kein `Math.Pow`, kein `double` in den neuen Pfaden.
- **Kein neues serialisiertes Feld.** Die Serialisierung ist feldbasiert; jedes neue Feld käme aus Altständen als `null`/`0`. Beide Änderungen lesen ausschließlich vorhandenen Zustand.
- **Deutsche Domänenbenennung** für Konstanten, Methoden und Kommentare.
- **Balancing-Konstanten als `public const int`** auf der jeweiligen Klasse — etabliertes Muster der Lib (`ErpressungManager.BeziehungsverlustBeiMisserfolg`, `KatastrophenManager.PreisanstiegMin`).
- **Lib-CHANGELOG:** bilinguale Stichpunkte (DE und EN) unter dem einen `## [Unreleased]`-Block, **kein** Versionsheader und **kein** Datum pro Change.
- **Commit-Reihenfolge:** erst Lib, dann Godot. Der Godot-Commit nennt die Lib-Version im Betreff.
- **Commit-Nachrichten per POSIX-Heredoc** (`git commit -F - <<'EOF' … EOF`) — die Bash-Tool-Shell ist Git Bash, ein PowerShell-Here-String hängt ein `@` an die Betreffzeile.
- **Beide Repos stehen auf Branch `feature/ki-aggression-sabotage-anschwaerzen`.** Nicht mergen, nicht pushen.
- **Startwerte sind keine Endwerte.** Die vier Balancing-Zahlen werden in Task 4 gegen echte Läufe kalibriert.

**Repo-Pfade:**
- Lib: `D:\Projekte\C# Projekte\Conspiratio.Lib`
- Godot: `C:\Projekte\Godot\Conspiratio.Godot`

**Domänenwissen, das sonst Zeit kostet:**
- `SW.Statisch.GetMaxStadtID()` ist **15**, gültige Stadt-IDs sind aber **1–14** („in Wirklichkeit 1 weniger, da Arrayverschub"). `GetMaxWerkstaettenProStadt()` ist **6** → 84 mögliche Betriebe.
- `HumSpieler.GetSpielerHatInStadtXWerkstaettenY(werkstaettenNr, stadtID)` hat **vertauschte Parameter** gegenüber dem Feldzugriff und erwartet `werkstaettenNr` **1-basiert**; intern ist es `_spielerHatInStadtXWerkstaettenY[stadtID, werkstaettenNr - 1]`.
- Werkstatt-Basispreise nach Rohstoffstufe: Stufe 1 = **2 000**, Stufe 2 = **10 000**, Stufe 3 = **40 000**.
- Stadt 1 („Frozen Castle") hat **5 000** Einwohner, Stadt 2 („Icepike") **2 500**. Der Jahresbedarf ist `Einwohner / 10`.
- Stadt 1, Werkstattplatz 1 produziert Rohstoff **5 (Holz, Stufe 1)** → Basispreis 2 000.
- Korn (Rohstoff 1): `preisMin` 7, `preisStd` 8, `preisMax` 20. `SetRohstoffPreisVonIDXToY` klemmt auf `[preisMin, preisMax]`.
- Die Tests laufen **nacheinander** (`AssemblyInfo.cs`), weil der Spielzustand global in `SW` liegt. Jeder Test beginnt mit eigenem `TestSpielwelt.Starte()`.

---

### Task 1: Sättigungsabhängiger Warenpreis

**Files:**
- Modify: `Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs` (Methode `GetRohstoffPreisVonIDX`, Zeile 108–117)
- Test: `Conspiratio.Lib.Tests/HandelsbalancingTests.cs` (neu)

**Interfaces:**
- Consumes: nichts aus früheren Tasks.
- Produces: `public const int Stadt.AbschlagJeBedarfsjahrProzent` (Wert 10), `public const int Stadt.MaxAbschlagProzent` (Wert 50). Task 4 justiert diese beiden Werte.

**Kontext:** Heute lautet die Formel `preis − vorrat/1000`, hart geklemmt bei `preisMin`. Für Korn sind das maximal 1 Taler Abschlag (8 → 7), erreicht nach ~1 000 Stück Vorrat — das produziert ein einzelner Betrieb im ersten Jahr. Danach ist beliebiges Abladen kostenlos.

**Zentrale Verhaltensänderung:** Der Preis darf künftig **unter `preisMin` fallen**. Bliebe `preisMin` der Boden, wäre die neue Formel exakt so wirkungslos wie die alte (für Korn liegt `preisMin` nur 12,5 % unter `preisStd`). `preisMin`/`preisMax` binden weiterhin den *Basispreis* `_rohstoffPreis[X]` — durchgesetzt in `SetRohstoffPreisVonIDXToY` und `ErhoeheRohstoffPreisVonIDXByY`, die **unverändert** bleiben.

- [ ] **Step 1: Testdatei mit den fünf fehlschlagenden Tests anlegen**

Neue Datei `Conspiratio.Lib.Tests/HandelsbalancingTests.cs`:

```csharp
using Conspiratio.Lib.Gameplay.Gebiete;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Balancing des Warenhandels: Der Preis soll auf Übersättigung eines Marktes reagieren, damit
    /// mehrere Kontore nicht beliebig skalieren. Gemessen wird der Abschlag in Jahren lokalen Bedarfs
    /// (<c>Einwohner / 10</c>), nicht in festen Talern – so skaliert er mit der Stadtgröße.
    /// </summary>
    public class HandelsbalancingTests
    {
        private const int Korn = 1;

        /// <summary>Stadt 1 („Frozen Castle") hat 5 000 Einwohner, also 500 Stück Jahresbedarf.</summary>
        private const int GrosseStadt = 1;

        /// <summary>Stadt 2 („Icepike") hat 2 500 Einwohner, also 250 Stück Jahresbedarf.</summary>
        private const int KleineStadt = 2;

        /// <summary>Basispreis, den die Tests setzen – innerhalb von Korns Korridor [7, 20].</summary>
        private const int Basispreis = 8;

        private static Stadt BereiteStadtVor(int stadtId, int vorrat)
        {
            var stadt = SW.Dynamisch.GetStadtwithID(stadtId);
            stadt.SetRohstoffPreisVonIDXToY(Korn, Basispreis);
            stadt.SetRohstoffVorratWithIDXToY(Korn, vorrat);
            return stadt;
        }

        [Fact]
        public void Ohne_Vorrat_bleibt_der_Basispreis_unveraendert()
        {
            TestSpielwelt.Starte();

            Assert.Equal(Basispreis, BereiteStadtVor(GrosseStadt, 0).GetRohstoffPreisVonIDX(Korn));
        }

        [Fact]
        public void Ein_Jahresbedarf_Vorrat_kostet_genau_einen_Abschlagsschritt()
        {
            TestSpielwelt.Starte();

            // 500 Stück = ein Jahresbedarf von Stadt 1 => 10 % Abschlag => 8 * 90 / 100 = 7.
            Assert.Equal(7, BereiteStadtVor(GrosseStadt, 500).GetRohstoffPreisVonIDX(Korn));
        }

        [Fact]
        public void Der_Abschlag_ist_gedeckelt()
        {
            TestSpielwelt.Starte();

            // Selbst bei absurdem Vorrat greift nur MaxAbschlagProzent => 8 * 50 / 100 = 4.
            Assert.Equal(4, BereiteStadtVor(GrosseStadt, 1_000_000).GetRohstoffPreisVonIDX(Korn));
        }

        /// <summary>
        /// Die bewusste Verhaltensänderung: Früher klemmte der Getter bei <c>preisMin</c> und machte den
        /// Mengenabschlag damit wirkungslos. Jetzt begrenzt nur noch <c>MaxAbschlagProzent</c>.
        /// </summary>
        [Fact]
        public void Bei_Uebersaettigung_faellt_der_Preis_unter_den_Mindestpreis()
        {
            TestSpielwelt.Starte();

            int preis = BereiteStadtVor(GrosseStadt, 1_000_000).GetRohstoffPreisVonIDX(Korn);

            Assert.True(preis < SW.Dynamisch.GetRohstoffwithID(Korn).GetPreisMin(),
                        "Der Mengenabschlag muss unter preisMin wirken dürfen, sonst bleibt er folgenlos.");
        }

        [Fact]
        public void Eine_groessere_Stadt_verkraftet_denselben_Vorrat_besser()
        {
            TestSpielwelt.Starte();

            int gross = BereiteStadtVor(GrosseStadt, 500).GetRohstoffPreisVonIDX(Korn);
            int klein = BereiteStadtVor(KleineStadt, 500).GetRohstoffPreisVonIDX(Korn);

            // Gleicher Vorrat, halber Bedarf => doppelter Abschlag: 10 % gegen 20 %.
            Assert.Equal(7, gross);
            Assert.Equal(6, klein);
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~HandelsbalancingTests"
```

Die Tests referenzieren die neuen Konstanten bewusst **nicht** direkt, sondern prüfen Ergebniswerte. Sie kompilieren daher bereits und schlagen **inhaltlich** fehl. Erwartete Lage — 1 grün, 4 rot:

| Test | Erwartet | Heute tatsächlich | Status |
|---|---|---|---|
| `Ohne_Vorrat_bleibt_der_Basispreis_unveraendert` | 8 | 8 | grün (Regressionsschutz) |
| `Ein_Jahresbedarf_Vorrat_kostet_genau_einen_Abschlagsschritt` | 7 | 8 (`500/1000` = 0 bei Ganzzahldivision) | **rot** |
| `Der_Abschlag_ist_gedeckelt` | 4 | 7 (alte `preisMin`-Klemme) | **rot** |
| `Bei_Uebersaettigung_faellt_der_Preis_unter_den_Mindestpreis` | < 7 | genau 7 | **rot** |
| `Eine_groessere_Stadt_verkraftet_denselben_Vorrat_besser` | 7 / 6 | 8 / 8 | **rot** |

Schlägt einer der vier roten Tests **nicht** fehl, stimmt eine Annahme über die Testwelt nicht — das klären, bevor implementiert wird.

- [ ] **Step 3: Die Formel ersetzen**

In `Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs` die Konstanten oben in der Klasse ergänzen (zu den übrigen Feldern, vor dem Konstruktor):

```csharp
        /// <summary>
        /// Preisabschlag in Prozent je Jahresbedarf, der als Überhang im Stadtlager liegt.
        /// Balancing-Startwert – siehe Spec „Handelsbalancing".
        /// </summary>
        public const int AbschlagJeBedarfsjahrProzent = 10;

        /// <summary>
        /// Obergrenze des Mengenabschlags. Sie ersetzt den früheren <c>preisMin</c>-Boden: Dieser lag
        /// je nach Ware nur ~12 % unter dem Standardpreis und machte den Abschlag damit folgenlos.
        /// </summary>
        public const int MaxAbschlagProzent = 50;
```

Und `GetRohstoffPreisVonIDX` (Zeile 108–117) vollständig ersetzen durch:

```csharp
        /// <summary>
        /// Der Marktpreis der Ware in dieser Stadt: Basispreis abzüglich eines Mengenabschlags, der sich
        /// danach richtet, wie viele Jahre lokalen Bedarfs bereits als Überhang im Stadtlager liegen.
        ///
        /// Der Bezug auf <c>Einwohner / 10</c> (denselben Verbrauch, den
        /// <c>DynamischeSpieldaten.RohBedarfAktRundenEnde</c> jährlich abzieht) lässt den Abschlag mit der
        /// Stadtgröße skalieren: Ein großer Markt verkraftet mehr Absatz als ein kleiner.
        ///
        /// Bewusst **ohne** <c>preisMin</c>-Klemme – die begrenzt den Basispreis in den Settern. Als Boden
        /// des Abschlags dient <see cref="MaxAbschlagProzent"/>.
        /// </summary>
        public int GetRohstoffPreisVonIDX(int X)
        {
            int jahresbedarf = Math.Max(1, _einwohner / 10);

            int abschlagProzent = Math.Min(MaxAbschlagProzent,
                                           (_rohstoffVorrat[X] * AbschlagJeBedarfsjahrProzent) / jahresbedarf);

            return (_rohstoffPreis[X] * (100 - abschlagProzent)) / 100;
        }
```

Prüfen, dass `using System;` in der Datei vorhanden ist (für `Math`); `Convert` wurde bereits genutzt, also ist es sehr wahrscheinlich da — sonst ergänzen.

- [ ] **Step 4: Tests laufen lassen und Erfolg bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~HandelsbalancingTests"
```

Erwartet: 5 Tests, alle grün.

- [ ] **Step 5: Vollständige Testsuite laufen lassen**

```bash
dotnet test
```

Erwartet: alles grün. Falls ein Test zu Preisen, Steuern oder Vermögen kippt, ist das ein **echter** Befund (der Preis reagiert nun auf Überhang) — melden statt den Test stumm anzupassen.

- [ ] **Step 6: Commit**

```bash
git add Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs Conspiratio.Lib.Tests/HandelsbalancingTests.cs
git commit -F - <<'EOF'
Warenpreis reagiert auf Marktsaettigung statt auf einen Fixbetrag

Der Mengenabschlag betrug bisher 1 Taler je 1000 Stueck Vorrat und war bei
preisMin geklemmt - fuer Stufe-1-Waren also hoechstens 12,5 %, erreicht schon
nach ~1000 Stueck. Ein einzelner Betrieb produziert das im ersten Jahr, danach
war beliebiges Abladen kostenlos.

Der Abschlag bemisst sich jetzt in Jahren lokalen Bedarfs (Einwohner/10) und
ist bei MaxAbschlagProzent gedeckelt. Damit skaliert er mit der Stadtgroesse
und saettigt nicht mehr sofort. preisMin begrenzt weiterhin den Basispreis in
den Settern, nicht mehr den Abschlag.
EOF
```

---

### Task 2: Progressiver Werkstatt-Kaufpreis

**Files:**
- Modify: `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` (neue Methode `ZaehleWerkstaetten`, einzufügen bei den übrigen Werkstatt-Zugriffen um Zeile 280–295)
- Modify: `Conspiratio.Lib/Allgemein/HandelsManager.cs` (Methode `GetWerkstattKaufpreis`, Zeile 64–67)
- Test: `Conspiratio.Lib.Tests/HandelsbalancingTests.cs` (erweitern)

**Interfaces:**
- Consumes: fachlich nichts aus Task 1 (die beiden Formeln sind unabhängig). **Aber:** Die Tests erweitern die in Task 1 angelegte Datei `HandelsbalancingTests.cs` und nutzen deren bereits vorhandene private Konstanten `GrosseStadt` (= 1) und `KleineStadt` (= 2) mit. Diese Datei zuerst lesen.
- Produces: `public int HumSpieler.ZaehleWerkstaetten()`, `public const int HandelsManager.SteigerungProzent` (Wert 125), `public const int HandelsManager.MaxSteigerungsstufen` (Wert 20). Task 4 justiert `SteigerungProzent`.

**Kontext:** `GetWerkstattKaufpreis` gibt heute schlicht `Rohstoff.GetWSKaufpreis()` zurück, unabhängig vom Besitzstand. Bei 84 möglichen Betrieben zu je 2 000 Talern und ~3 700 Talern Jahresgewinn liegt die Amortisation unter einem Jahr.

**Überlaufschutz, nicht Balancing:** `MaxSteigerungsstufen` ist zwingend. Ohne Deckel überschreitet `2000 × 1,25ⁿ` etwa ab dem 62. Betrieb `int.MaxValue`, und 84 sind besitzbar.

- [ ] **Step 1: Die fehlschlagenden Tests ergänzen**

An `Conspiratio.Lib.Tests/HandelsbalancingTests.cs` innerhalb der Klasse anfügen (die `using`-Zeile `using Conspiratio.Lib.Allgemein;` oben ergänzen):

```csharp
        /// <summary>
        /// Setzt genau <paramref name="anzahl"/> Werkstätten des aktiven Spielers auf aktiv und alle
        /// übrigen auf inaktiv. Gültige Stadt-IDs sind 1 bis <c>GetMaxStadtID() - 1</c>.
        /// </summary>
        private static void SetzeWerkstaetten(int anzahl)
        {
            var spieler = SW.Dynamisch.GetAktHum();
            int gesetzt = 0;

            for (int stadtId = 1; stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
            {
                for (int nr = 1; nr <= SW.Statisch.GetMaxWerkstaettenProStadt(); nr++)
                {
                    bool aktiv = gesetzt < anzahl;
                    spieler.GetSpielerHatInStadtXWerkstaettenY(nr, stadtId).SetEnabled(aktiv);

                    if (aktiv)
                        gesetzt++;
                }
            }
        }

        [Fact]
        public void Der_erste_Betrieb_kostet_unveraendert_den_Grundpreis()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(0);

            // Stadt 1, Platz 1 produziert Holz (Stufe 1) => Grundpreis 2000.
            Assert.Equal(2000, new HandelsManager().GetWerkstattKaufpreis(GrosseStadt, 1));
        }

        /// <summary>
        /// Die Staffelung rechnet in Ganzzahlen und schneidet dabei je Schritt ab; das Ergebnis liegt
        /// daher etwas unter <c>2000 * 1,25^n</c>. Geprüft wird gegen die tatsächliche Schrittfolge.
        /// </summary>
        [Theory]
        [InlineData(0, 2000)]
        [InlineData(4, 4882)]
        [InlineData(9, 14895)]
        [InlineData(14, 45452)]
        public void Jeder_weitere_Betrieb_wird_teurer(int besessen, int erwarteterPreis)
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(besessen);

            Assert.Equal(erwarteterPreis, new HandelsManager().GetWerkstattKaufpreis(GrosseStadt, 1));
        }

        /// <summary>
        /// Ohne Deckel liefe <c>int</c> ab etwa dem 62. Betrieb über – besitzbar sind 84.
        /// </summary>
        [Fact]
        public void Jenseits_des_Deckels_bleibt_der_Preis_endlich()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(84);

            int preis = new HandelsManager().GetWerkstattKaufpreis(GrosseStadt, 1);

            Assert.Equal(173382, preis);
        }

        [Fact]
        public void Der_Zaehler_erfasst_Betriebe_ueber_Stadtgrenzen_hinweg()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(0);

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.GetSpielerHatInStadtXWerkstaettenY(1, GrosseStadt).SetEnabled(true);
            spieler.GetSpielerHatInStadtXWerkstaettenY(3, KleineStadt).SetEnabled(true);

            Assert.Equal(2, spieler.ZaehleWerkstaetten());
        }

        [Fact]
        public void Deaktivierte_Plaetze_zaehlen_nicht_mit()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(5);

            SW.Dynamisch.GetAktHum().GetSpielerHatInStadtXWerkstaettenY(1, GrosseStadt).SetEnabled(false);

            Assert.Equal(4, SW.Dynamisch.GetAktHum().ZaehleWerkstaetten());
        }

        /// <summary>
        /// Der Verkaufspreis bleibt an den Kaufpreis gekoppelt (¾) und erbt die Staffelung damit
        /// automatisch. Beabsichtigt: Wer verkauft, bekommt anteilig zurück, was er bezahlt hat. Eine
        /// Rückkauf-Arbitrage entsteht nicht, weil ¾ kleiner als 1 ist.
        /// </summary>
        [Fact]
        public void Der_Verkaufspreis_folgt_der_Staffelung()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(4);

            var handel = new HandelsManager();

            Assert.Equal(4882, handel.GetWerkstattKaufpreis(GrosseStadt, 1));
            Assert.Equal(4882 * 3 / 4, handel.GetWerkstattVerkaufspreis(GrosseStadt, 1));
        }
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~HandelsbalancingTests"
```

Erwartet: Kompilierfehler `CS1061` — `HumSpieler` enthält keine Definition für `ZaehleWerkstaetten`.

- [ ] **Step 3: Den Zähler auf `HumSpieler` ergänzen**

In `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` direkt nach `ErmittleLagerplatzInStadt` (endet um Zeile 293) einfügen:

```csharp
        /// <summary>
        /// Zahl der aktiven Werkstätten dieses Spielers über alle Städte. Grundlage der Kaufpreis-
        /// staffelung in <c>HandelsManager.GetWerkstattKaufpreis</c>.
        ///
        /// Das Feld ist als <c>[stadtID, werkstattIndex]</c> mit 0-basiertem Werkstattindex angelegt –
        /// anders als der öffentliche Zugriff, der 1-basiert zählt und die Parameter vertauscht.
        /// </summary>
        public int ZaehleWerkstaetten()
        {
            int anzahl = 0;

            for (int stadtId = 1; stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
            {
                for (int i = 0; i < SW.Statisch.GetMaxWerkstaettenProStadt(); i++)
                {
                    if (_spielerHatInStadtXWerkstaettenY[stadtId, i].GetEnabled())
                        anzahl++;
                }
            }

            return anzahl;
        }
```

- [ ] **Step 4: Die Staffelung in `HandelsManager` einbauen**

In `Conspiratio.Lib/Allgemein/HandelsManager.cs` die Konstanten oben in der Klasse ergänzen:

```csharp
        /// <summary>
        /// Aufschlag je bereits besessenem Betrieb, in Prozent des bisherigen Preises (125 = +25 %).
        /// Balancing-Startwert – siehe Spec „Handelsbalancing".
        /// </summary>
        public const int SteigerungProzent = 125;

        /// <summary>
        /// Obergrenze der Staffelstufen. Zwingend als Überlaufschutz: Ohne Deckel überschreitet
        /// <c>2000 * 1,25^n</c> ab etwa dem 62. Betrieb <c>int.MaxValue</c>, und 84 sind besitzbar.
        /// Beim Kalibrieren nicht ersatzlos anheben.
        /// </summary>
        public const int MaxSteigerungsstufen = 20;
```

Und `GetWerkstattKaufpreis` (Zeile 64–67) ersetzen durch:

```csharp
        /// <summary>
        /// Kaufpreis des Werkstattplatzes, gestaffelt nach der Zahl der reichsweit bereits besessenen
        /// Betriebe. Der erste bleibt beim Grundpreis der Ware; jeder weitere kostet
        /// <see cref="SteigerungProzent"/> des vorherigen.
        ///
        /// Bewusst als Ganzzahlschleife statt <c>Math.Pow</c>: exakt, deterministisch und ohne
        /// Gleitkomma im Kern der Ökonomie – E2E-Läufe müssen seed-reproduzierbar bleiben.
        /// </summary>
        [PublicAPI]
        public int GetWerkstattKaufpreis(int stadtId, int werkstattNr)
        {
            int preis = SW.Dynamisch.GetRohstoffwithID(RohstoffIdAnPlatz(stadtId, werkstattNr)).GetWSKaufpreis();
            int besessen = Math.Min(SW.Dynamisch.GetAktHum().ZaehleWerkstaetten(), MaxSteigerungsstufen);

            for (int i = 0; i < besessen; i++)
                preis = (preis * SteigerungProzent) / 100;

            return preis;
        }
```

Prüfen, dass `using System;` in der Datei vorhanden ist (für `Math`); sonst ergänzen.

- [ ] **Step 5: Tests laufen lassen und Erfolg bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~HandelsbalancingTests"
```

Erwartet: alle Tests grün — 5 aus Task 1 und 9 aus Task 2 (die `Theory` zählt als vier Fälle).

- [ ] **Step 6: Vollständige Testsuite laufen lassen**

```bash
dotnet test
```

Erwartet: alles grün.

- [ ] **Step 7: Commit**

```bash
git add Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs Conspiratio.Lib/Allgemein/HandelsManager.cs Conspiratio.Lib.Tests/HandelsbalancingTests.cs
git commit -F - <<'EOF'
Werkstatt-Kaufpreis steigt mit dem Besitzstand

Der Preis war mit fix 2000 Talern (Stufe 1) unabhaengig davon, wie viele
Betriebe der Spieler schon fuehrt. Bei 84 moeglichen Werkstaetten und rund
3700 Talern Jahresgewinn je Betrieb lag die Amortisation unter einem Jahr,
womit Expansion strikt dominant war.

Jeder weitere Betrieb kostet nun SteigerungProzent des vorherigen, gedeckelt
durch MaxSteigerungsstufen - der Deckel ist Ueberlaufschutz, nicht Balancing:
ohne ihn liefe int ab etwa dem 62. Betrieb ueber.
EOF
```

---

### Task 3: Lib-Version, CHANGELOG und lokale Paketbereitstellung

**Files:**
- Modify: `Conspiratio.Lib/Conspiratio.Lib.csproj` (Zeile 42, `<Version>`)
- Modify: `CHANGELOG.md` (im Lib-Repo)
- Modify: `Conspiratio.Godot.csproj` (Zeile 15, `PackageReference`)

**Interfaces:**
- Consumes: die fertigen Formeln aus Task 1 und 2.
- Produces: eine im lokalen NuGet-Cache verfügbare Lib-Version `4.1.0`, gegen die Task 4 messen kann.

**Kontext:** Der Godot-Client konsumiert die Lib als NuGet-Paket. Für Task 4 muss die unveröffentlichte Version im globalen Cache liegen. **Schritt 2 ist der, der erfahrungsgemäß übersehen wird:** NuGet extrahiert eine Version nicht neu, die es schon kennt — ohne Löschen baut Godot still gegen den alten Code weiter.

- [ ] **Step 1: Lib-Version anheben**

In `Conspiratio.Lib/Conspiratio.Lib.csproj` Zeile 42:

```xml
    <Version>4.1.0</Version>
```

- [ ] **Step 2: CHANGELOG der Lib ergänzen**

Unter dem bestehenden `## [Unreleased]`-Block (kein neuer Versionsheader, kein Datum) je einen Stichpunkt in beiden Sprachabschnitten ergänzen:

Deutsch:
```markdown
- Warenpreise reagieren auf Marktsättigung: Der Mengenabschlag bemisst sich jetzt in Jahren lokalen Bedarfs statt in festen Talern und skaliert damit mit der Stadtgröße.
- Der Werkstatt-Kaufpreis steigt mit jedem bereits besessenen Betrieb, sodass Expansion einen sinkenden Grenznutzen hat.
```

Englisch:
```markdown
- Goods prices now respond to market saturation: the volume discount is measured in years of local demand instead of a fixed amount, so it scales with city size.
- Workshop purchase price rises with each workshop already owned, giving expansion a diminishing return.
```

- [ ] **Step 3: Lib bauen (erzeugt das .nupkg)**

```bash
dotnet build
```

Erwartet: Build erfolgreich; `Conspiratio.Lib/bin/Debug/Conspiratio.Lib.4.1.0.nupkg` existiert (`GeneratePackageOnBuild`).

- [ ] **Step 4: Zwischengespeicherte Kopie löschen**

```bash
rm -rf ~/.nuget/packages/conspiratio.lib/4.1.0
```

Ohne diesen Schritt verwendet der Godot-Build stillschweigend den alten Code weiter.

- [ ] **Step 5: Globalen Cache aus dem lokalen Feed füllen**

Das Godot-Repo pinnt `nuget.config` auf nuget.org, darf den lokalen Feed also nicht nutzen. Deshalb über das Testprojekt der Lib, das den lokalen Feed verwenden darf:

```bash
dotnet restore --source "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib/bin/Debug"
```

- [ ] **Step 6: Godot auf die neue Version umstellen**

In `C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj` Zeile 15:

```xml
    <PackageReference Include="Conspiratio.Lib" Version="4.1.0" />
```

- [ ] **Step 7: Godot bauen und Smoke-Test**

```bash
dotnet build
```

Erwartet: 0 Fehler.

```bash
"/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . --quit "res://scenes/Main.tscn"
```

Erwartet: sauberer Durchlauf ohne Stacktrace. Fehler gehen auf **stderr**.

- [ ] **Step 8: Commit in beiden Repos**

Zuerst Lib:

```bash
git add Conspiratio.Lib/Conspiratio.Lib.csproj CHANGELOG.md
git commit -F - <<'EOF'
Version 4.1.0: Handelsbalancing
EOF
```

Dann Godot:

```bash
git add Conspiratio.Godot.csproj
git commit -F - <<'EOF'
Lib-Version 4.1.0 uebernehmen (Handelsbalancing)
EOF
```

---

### Task 4: Kalibrierung gegen E2E-Läufe und Dokumentation

**Files:**
- Modify (nur falls die Messung es verlangt): `Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs`, `Conspiratio.Lib/Allgemein/HandelsManager.cs` (die vier Balancing-Konstanten)
- Modify: `CLAUDE.md` (im Godot-Repo, Abschnitt zur Handelsrunde)

**Interfaces:**
- Consumes: die lauffähige Godot-Version aus Task 3.
- Produces: festgeschriebene Balancing-Werte und aktualisierte Referenzzahlen in `CLAUDE.md`.

**Kontext:** Die vier Zahlen sind begründete Startwerte, keine Endwerte. Der Zielkorridor lautet: spürbar unter den heute gemessenen **+64 175 bis +80 088**, aber **deutlich positiv** — Expansion soll sich weiter lohnen, nur mit sinkendem Grenznutzen.

**Messmethodik:** `--ohne-aktionen --spieler=1` isoliert den Handel von den zufälligen Kontor-Aktionen, deren Streuung mehrere Zehntausend Taler beträgt und den Effekt sonst überdeckt. **Nicht** `--ohne-bereiche` verwenden — das überspringt den Heimatstadtbesuch und meldet null Handel.

- [ ] **Step 1: Referenzlauf über drei Seeds**

Aus `C:\Projekte\Godot\Conspiratio.Godot`:

```bash
for seed in 4711 1234 999; do "/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=15 --spieler=1 --seed=$seed --ohne-aktionen > /tmp/kalib_$seed.txt 2>&1; echo "--- seed $seed (exit $?) ---"; grep -E "Gespielte Jahre|Verkaufte Waren|Spieler 1:|E2E-Durchlauf" /tmp/kalib_$seed.txt; done
```

Vergleichswerte **vor** dieser Änderung: Seed 4711 = +80 088, Seed 1234 = +64 175.

- [ ] **Step 2: Ergebnis bewerten und Parameter entscheiden**

Bewertungsregel:
- Endvermögen **negativ oder nahe null** → zu hart. `MaxAbschlagProzent` auf 40 senken, erneut messen.
- Endvermögen **weiterhin über ~50 000** → zu lasch. `AbschlagJeBedarfsjahrProzent` auf 15 anheben, erneut messen.
- Endvermögen **spürbar niedriger, aber klar positiv** → Werte bleiben.

Bei Änderung: Konstante anpassen, Task-3-Schritte 3–5 (bauen, Cache löschen, restore) wiederholen, dann erneut messen. `MaxSteigerungsstufen` dabei **nicht** anheben (Überlaufschutz).

Werden Konstanten geändert, müssen die Erwartungswerte in `HandelsbalancingTests.cs` mitgezogen werden — `dotnet test` im Lib-Repo laufen lassen.

- [ ] **Step 3: Vollen E2E-Lauf zur Regressionssicherung**

```bash
"/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=15 --spieler=2 --seed=4711 --verbose > /tmp/voll.txt 2>&1; echo "exit=$?"; grep -E "Gespielte Jahre|E2E-Durchlauf" /tmp/voll.txt
```

Erwartet: exit 0, „E2E-Durchlauf bestanden", 15 von 15 Jahren. Ein negatives Endvermögen ist hier **kein** Fehlschlag — die Zufallsaktionen dominieren das Ergebnis (dokumentiert in `CLAUDE.md`).

- [ ] **Step 4: `CLAUDE.md` aktualisieren**

Im Abschnitt zur Handelsrunde (Punkt 4, „Automated play-through") sind die Zahlen **+33 311**, **+64 175 bis +80 088** und **+4 979** durch diese Änderung überholt. Zu ergänzen ist außerdem die neue Mechanik, weil sie das Verhalten des Treibers erklärt:

```markdown
   **Selling into one city no longer scales.** `Stadt.GetRohstoffPreisVonIDX` discounts the price by how
   many years of local demand (`Einwohner / 10`) sit unsold in that city's stock, capped at
   `Stadt.MaxAbschlagProzent`. Sustainable volume is therefore bounded by the population you actually
   supply, and the discount now reaches **below** `preisMin` — that floor only bounds the base price in
   the setters. Workshops also get progressively more expensive
   (`HandelsManager.SteigerungProzent` per workshop already owned, capped at `MaxSteigerungsstufen`,
   which is overflow protection rather than balancing).
```

Die konkreten gemessenen Endvermögen aus Schritt 1 an die Stelle der alten Zahlen setzen.

- [ ] **Step 5: Commit**

Falls Konstanten geändert wurden, zuerst im Lib-Repo:

```bash
git add Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs Conspiratio.Lib/Allgemein/HandelsManager.cs Conspiratio.Lib.Tests/HandelsbalancingTests.cs
git commit -F - <<'EOF'
Handelsbalancing gegen E2E-Laeufe kalibriert
EOF
```

Dann im Godot-Repo:

```bash
git add CLAUDE.md
git commit -F - <<'EOF'
CLAUDE.md: Handelszahlen nach dem Balancing neu vermessen

Der Saettigungspreis und der gestaffelte Werkstatt-Kaufpreis machen die
bisherigen Referenzwerte ungueltig.
EOF
```
