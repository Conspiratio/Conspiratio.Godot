# Eine einzige KI-Aggressivitäts-Einstellung — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aus zwei halbfertigen Einstellungen (einem toten Enum und einem zu eng gefassten Regler) wird eine einzige 0–100-%-Einstellung, die sämtliche KI-Feindseligkeit steuert und sofort im laufenden Spiel wirkt.

**Architecture:** Die Einstellung greift an genau zwei Stellen an: `KISpieler.GetBosheit()` moduliert den gespeicherten Charakterwert beim *Lesen* (dadurch erfassen wir alle acht Bosheit-Verbraucher, ohne deren Code anzufassen, und die Einstellung wirkt rückwirkend auf bestehende KIs), und ein Interpolations-Helfer ersetzt die drei `switch`-Blöcke über den alten `EnumSchwierigkeitsgrad`. Das Feld selbst wird von `KiAktivitaetProzent` in `KiAggressivitaetProzent` umbenannt; der Optionen-Regler schreibt es künftig zusätzlich in den laufenden Spielstand.

**Tech Stack:** C# / .NET (netstandard2.0 Lib, .NET 8 Godot-Client), xUnit (Lib-Tests), Godot 4.7 headless smoke test.

**Spec:** [docs/ki-aggressivitaet-einstellung-konzept.md](../../ki-aggressivitaet-einstellung-konzept.md)

## Global Constraints

- Alle neuen UI-Texte und Kommentare sind **Deutsch** (Domänen-Konvention des Projekts).
- **50 % ist der Regressionsanker:** An *jeder* umgestellten Stelle muss der Wert bei 50 % bit-identisch zum heutigen Verhalten sein. Jede Task, die eine Formel anfasst, pinnt das mit einem Test fest.
- **Die Einstellung wirkt sofort und rückwirkend** — auf bereits existierende KI-Spieler, im laufenden Spiel und in geladenen alten Spielständen. Deshalb wird Bosheit beim *Lesen* moduliert, nicht beim Auswürfeln eingebacken.
- **Alt-Spielstände:** `KiAggressivitaetProzent <= 0` wird überall wie 50 % behandelt (der `Spieleinstellungen`-Standard ist 50, ein Altstand ohne das Feld deserialisiert auf 0 oder 50 — beide Wege führen zum bisherigen Normalverhalten).
- **Der wirksame Wertebereich ist 1–100, nicht 0–100.** Weil 0 „nicht gesetzt" bedeutet und auf 50 zurückfällt, ist 0 % kein erreichbarer Regler-Wert; der Slider hat entsprechend `min_value = 1`. Praktisch macht das keinen Unterschied (bei 1 % beträgt die Bosheit-Verschiebung −49 statt −50), aber **alle Tests müssen 1 statt 0 als unteren Endpunkt verwenden** — ein Test mit `KiAggressivitaetProzent = 0` misst den Fallback, nicht den unteren Anschlag.
- **Die Streuung zwischen KIs bleibt erhalten:** Die Einstellung verschiebt alle Bosheitswerte gemeinsam; der individuelle Charakterwurf jeder KI (`_boese`) bleibt unangetastet und wird weiterhin unverändert serialisiert.
- Lib-Commits vor Godot-Commits; der Godot-Commit referenziert die Lib-Version im Subject (`… (Conspiratio.Lib x.y.z)`).
- Zwei getrennte Git-Repos, beide auf Branch `feature/ki-aggression-sabotage-anschwaerzen`:
  - Lib: `D:\Projekte\C# Projekte\Conspiratio.Lib\`
  - Godot: `C:\Projekte\Godot\Conspiratio.Godot\` (dieses Repo)

---

## Task 1: Feld umbenennen und zentralen Zugriff schaffen

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Einstellungen\Spieleinstellungen.cs`
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Spielwelt\DynamischeSpieldaten.cs` (neue Methode; genaue Stelle siehe Step 3)
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Kampf\Raeuberlager.cs:68-73`
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Kampf\Zollburg.cs:89-94`
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\KiAggressivitaetTests.cs` (neu)

**Interfaces:**
- Produces: `Spieleinstellungen.KiAggressivitaetProzent` (int, Standard 50) — ersetzt `KiAktivitaetProzent`; `DynamischeSpieldaten.GetKiAggressivitaetProzent(): int` (öffentlich, mit `<= 0` → 50 Fallback) — von Task 2 (`KISpieler`), Task 3 (Interpolation) und hier von Räuberlager/Zollburg genutzt.

- [ ] **Step 1: Write the failing test**

Neue Datei `Conspiratio.Lib.Tests/KiAggressivitaetTests.cs`:

```csharp
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Die eine Einstellung für die Aggressivität der KI-Spieler (0–100 %). 50 % muss überall
    /// exakt das bisherige Verhalten reproduzieren.
    /// </summary>
    public class KiAggressivitaetTests
    {
        [Fact]
        public void Der_Standardwert_ist_fuenfzig_Prozent()
        {
            TestSpielwelt.Starte();

            // Bewusst nur über den Accessor geprüft: Der Spielstand entsteht über einen Pfad, der
            // Feldinitialisierer umgeht (FormatterServices.GetUninitializedObject, siehe CLAUDE.md),
            // die rohe Property darf dort also 0 sein. Genau dafür gibt es den Fallback – auf den
            // Rohwert zu assertieren würde der Prämisse dieses Features widersprechen.
            Assert.Equal(50, SW.Dynamisch.GetKiAggressivitaetProzent());
        }

        [Theory]
        [InlineData(0, 50)]    // Alter Spielstand ohne das Feld -> wie 50 %
        [InlineData(-5, 50)]   // Defensiv: negative Werte ebenso
        [InlineData(1, 1)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        public void Nicht_gesetzte_Werte_gelten_als_fuenfzig_Prozent(int gesetzt, int erwartet)
        {
            TestSpielwelt.Starte();
            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = gesetzt;

            Assert.Equal(erwartet, SW.Dynamisch.GetKiAggressivitaetProzent());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiAggressivitaetTests"`
Expected: Build-Fehler — `Spieleinstellungen` kennt `KiAggressivitaetProzent` nicht, `DynamischeSpieldaten` kennt `GetKiAggressivitaetProzent` nicht.

- [ ] **Step 3: Feld umbenennen und Zugriffsmethode ergänzen**

In `Spieleinstellungen.cs` die Property `KiAktivitaetProzent` samt XML-Kommentar (aktuell Zeilen 17–22) ersetzen durch:

```csharp
        /// <summary>
        /// Aggressivität der KI-Spieler als Prozentwert (1–100, Standard 50). Steuert sämtliche
        /// feindseligen und militärischen KI-Aktivitäten: die Bosheit der KI-Charaktere (und damit
        /// Beleidigungen, Duelle, Sabotage, Anschwärzen und KI-Verbrechen), Anklagen, Gerichtsurteile,
        /// Amtsenthebungen sowie Ausbau und Aktionen der Militärstützpunkte. 50 % entspricht dem
        /// bisherigen Normalwert; alte Spielstände (Wert 0) werden wie 50 % behandelt.
        /// </summary>
        public int KiAggressivitaetProzent { get; set; } = 50;
```

In `DynamischeSpieldaten.cs` eine neue Methode ergänzen. Passende Stelle: direkt vor `#region DeliktpunkteBerechnen` (suche nach dieser Zeile, die Zeilennummer kann gedriftet sein), als eigene Region:

```csharp
        #region KiAggressivitaet
        /// <summary>
        /// Die eingestellte Aggressivität der KI-Spieler in Prozent (1–100). Alte Spielstände, in denen
        /// das Feld fehlt (Wert 0 oder kleiner), werden wie der Standardwert 50 % behandelt.
        /// </summary>
        public int GetKiAggressivitaetProzent()
        {
            int prozent = Spielstand.Einstellungen.KiAggressivitaetProzent;

            return prozent <= 0 ? 50 : prozent;
        }
        #endregion
```

In `Raeuberlager.cs` die Zeilen 68–73 (Kommentar + `aktivitaetProzent`-Ermittlung + Fallback + Faktorberechnung) ersetzen durch:

```csharp
            // Aggressivität der KI als Prozentwert (1–100, Standard 50). 50 % entspricht dem
            // Normalfaktor 1.0, 100 % dem Faktor 2.0.
            double kiAktivitaetsfaktor = SW.Dynamisch.GetKiAggressivitaetProzent() / 50d;
```

In `Zollburg.cs` die Zeilen 89–94 (identischer Block) durch denselben Ersatz austauschen.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiAggressivitaetTests"`
Expected: PASS (6/6 — ein `[Fact]` plus fünf `[InlineData]`-Fälle).

- [ ] **Step 5: Full suite to catch remaining references**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln"`
Expected: PASS. Sollte der Build fehlschlagen, weil noch irgendwo `KiAktivitaetProzent` steht: Diese Stelle ebenfalls auf `GetKiAggressivitaetProzent()` umstellen (in der Lib darf danach kein `KiAktivitaetProzent` mehr vorkommen — mit `grep -rn "KiAktivitaetProzent" .` im Lib-Repo gegenprüfen; im Godot-Repo bleibt es bis Task 5 bestehen, das ist erwartet).

Für Räuberlager und Zollburg gibt es keine Tests, und dieser Plan ergänzt auch keine: Die
Faktor-Logik selbst (`… / 50d`) bleibt unverändert, geändert wird nur die Herkunft des
Prozentwerts — und die ist über `Nicht_gesetzte_Werte_gelten_als_fuenfzig_Prozent` (Step 1)
bereits abgedeckt. Beim Ersetzen darauf achten, dass in beiden Dateien der lokale
`if (aktivitaetProzent <= 0)`-Fallback **mit** entfernt wird (er lebt jetzt in
`GetKiAggressivitaetProzent()`) und keine verwaiste Variable zurückbleibt.

- [ ] **Step 6: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Gameplay/Einstellungen/Spieleinstellungen.cs Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs Conspiratio.Lib/Gameplay/Kampf/Raeuberlager.cs Conspiratio.Lib/Gameplay/Kampf/Zollburg.cs Conspiratio.Lib.Tests/KiAggressivitaetTests.cs
git commit -m "KiAktivitaetProzent zu KiAggressivitaetProzent umbenannt

Zentraler Zugriff ueber DynamischeSpieldaten.GetKiAggressivitaetProzent()
inklusive Alt-Spielstand-Fallback; Raeuberlager und Zollburg nutzen ihn
statt ihres jeweils lokal duplizierten Fallbacks."
```

---

## Task 2: Bosheit beim Lesen modulieren

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Personen\KISpieler.cs:68-71` (Methode `GetBosheit`)
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\KiAggressivitaetTests.cs`

**Interfaces:**
- Consumes: `DynamischeSpieldaten.GetKiAggressivitaetProzent()` (Task 1).
- Produces: `KISpieler.GetBosheit()` liefert künftig den modulierten Wert; `KISpieler.SetBosheit(int)` und das Feld `_boese` bleiben unverändert der Rohwert.

Dies ist der Kern des Vorhabens: Über diese eine Methode erfassen wir alle acht Bosheit-Verbraucher (KI-Beleidigung, Satisfaktionsforderung, Duell-Gegnerstärke, Wortgefecht-Gegnerstärke, Sabotage/Anschwärzen, KI-Straftaten-Häufigkeit, anfängliche und jährliche Deliktpunkte), ohne deren Code anzufassen.

- [ ] **Step 1: Write the failing tests**

In `KiAggressivitaetTests.cs` ergänzen (innerhalb der Klasse `KiAggressivitaetTests`):

```csharp
        [Theory]
        [InlineData(50, 60, 60)]    // Standard: unveraendert
        [InlineData(100, 60, 100)]  // +50, an der Obergrenze gekappt
        [InlineData(100, 10, 60)]   // +50
        [InlineData(1, 60, 11)]     // -49 (1 % ist der untere Anschlag, 0 hiesse "nicht gesetzt")
        [InlineData(1, 10, 0)]      // -49, an der Untergrenze gekappt
        [InlineData(75, 20, 45)]    // +25, stufenlos dazwischen
        public void Die_Bosheit_wird_um_die_Aggressivitaet_verschoben(int prozent, int roh, int erwartet)
        {
            TestSpielwelt.Starte();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: roh);
            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = prozent;

            Assert.Equal(erwartet, SW.Dynamisch.GetKIwithID(kiId).GetBosheit());
        }

        [Fact]
        public void Die_Einstellung_wirkt_sofort_auf_bestehende_KIs()
        {
            // Kernzusage des Features: Der Regler wirkt rueckwirkend, ohne neues Spiel.
            TestSpielwelt.Starte();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 40);
            var ki = SW.Dynamisch.GetKIwithID(kiId);

            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = 50;
            int vorher = ki.GetBosheit();

            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = 90;
            int nachher = ki.GetBosheit();

            Assert.Equal(40, vorher);
            Assert.Equal(80, nachher);
        }

        [Fact]
        public void Der_gespeicherte_Charakterwert_bleibt_unveraendert()
        {
            // _boese wird serialisiert; nur die Auswirkung wird moduliert, nicht der Charakter selbst.
            TestSpielwelt.Starte();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 30);
            var ki = SW.Dynamisch.GetKIwithID(kiId);

            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = 100;
            Assert.Equal(80, ki.GetBosheit());

            // Zurueckdrehen liefert exakt den Ausgangswert - der Rohwert wurde nie ueberschrieben.
            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = 50;
            Assert.Equal(30, ki.GetBosheit());
        }

        [Fact]
        public void Die_Streuung_zwischen_den_KIs_bleibt_erhalten()
        {
            TestSpielwelt.Starte();
            TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 10);
            TestSpielwelt.SetzeKiGegner(1, 0, bosheit: 40);
            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = 80;

            int ersteKi = SW.Statisch.GetMinKIID();
            int zweiteKi = ersteKi + 1;

            // Beide um +30 verschoben, der Abstand von 30 Punkten bleibt: die Einstellung
            // verschiebt gemeinsam, sie gleicht die Charaktere nicht an.
            Assert.Equal(40, SW.Dynamisch.GetKIwithID(ersteKi).GetBosheit());
            Assert.Equal(70, SW.Dynamisch.GetKIwithID(zweiteKi).GetBosheit());
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiAggressivitaetTests"`
Expected: Die vier neuen Tests schlagen fehl (`GetBosheit` liefert noch den Rohwert, z. B. erwartet 100, war 60). Die Tests aus Task 1 bleiben grün.

- [ ] **Step 3: `GetBosheit` modulieren**

In `KISpieler.cs` die Methode `GetBosheit` (aktuell Zeilen 68–71) ersetzen durch:

```csharp
        /// <summary>
        /// Die wirksame Bosheit dieser KI: ihr ausgewürfelter Charakterwert (<c>_boese</c>), verschoben
        /// um die eingestellte KI-Aggressivität. Bei 50 % (Standard) ist das exakt der Charakterwert,
        /// bei 100 % um 50 Punkte höher, bei 0 % um 50 niedriger – jeweils auf 0–100 begrenzt. Die
        /// Streuung zwischen den KIs bleibt dabei erhalten: Die Einstellung verschiebt alle Charaktere
        /// gemeinsam, sie gleicht sie nicht an.
        ///
        /// Bewusst hier und nicht beim Auswürfeln: Nur so wirkt die Einstellung auch auf bereits
        /// existierende KIs, im laufenden Spiel und in geladenen Spielständen. Der gespeicherte
        /// Charakterwert bleibt davon unberührt – wer ihn braucht, liest <c>_boese</c> direkt.
        /// </summary>
        public int GetBosheit()
        {
            int verschiebung = SW.Dynamisch.GetKiAggressivitaetProzent() - 50;

            return Math.Max(0, Math.Min(100, _boese + verschiebung));
        }
```

`using System;` steht bereits am Dateianfang (Zeile 1), `Math` ist damit verfügbar.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiAggressivitaetTests"`
Expected: PASS (alle Fälle aus Task 1 und Task 2).

- [ ] **Step 5: Full suite — hier zeigt sich, ob 50 % wirklich neutral ist**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln"`
Expected: PASS. Alle bestehenden Tests laufen mit dem Standardwert 50 %, bei dem `GetBosheit()` den Rohwert liefert — ein Fehlschlag hier bedeutet, dass die Verschiebung bei 50 % doch nicht 0 ist. Dann die Formel prüfen, nicht den Test anpassen.

- [ ] **Step 6: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Gameplay/Personen/KISpieler.cs Conspiratio.Lib.Tests/KiAggressivitaetTests.cs
git commit -m "Bosheit wird um die eingestellte KI-Aggressivitaet verschoben

Moduliert in GetBosheit statt beim Auswuerfeln: So wirkt die Einstellung
auch auf bestehende KIs, im laufenden Spiel und in geladenen Spielstaenden,
und erfasst alle acht Bosheit-Verbraucher ohne Aenderung an deren Code.
Der gespeicherte Charakterwert _boese bleibt unveraendert."
```

---

## Task 3: Interpolations-Helfer und Ablösung der drei `switch`-Blöcke

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Spielwelt\DynamischeSpieldaten.cs` (neue Methode in Region `KiAggressivitaet` aus Task 1; zwei `switch`-Blöcke bei ~2280 und ~2861)
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Justiz\GerichtsverhandlungManager.cs:577-588`
- Delete: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Einstellungen\EnumSchwierigkeitsgrad.cs`
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Einstellungen\Spieleinstellungen.cs` (Property `AggressivitaetKISpieler` entfernen)
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\KiAggressivitaetTests.cs`

**Interfaces:**
- Consumes: `DynamischeSpieldaten.GetKiAggressivitaetProzent()` (Task 1).
- Produces: `DynamischeSpieldaten.InterpoliereNachAggressivitaet(int beiNull, int beiFuenfzig, int beiHundert): int`.

Der alte `EnumSchwierigkeitsgrad` (`Niedrig`/`Mittel`/`Hoch`) verschwindet ersatzlos. Er war tot: Kein UI-Element hat ihn je gesetzt, er stand in jedem Spielstand auf `Mittel` — und `Mittel` ist exakt der Wert, den 50 % reproduziert. Es gibt daher nichts zu migrieren.

- [ ] **Step 1: Write the failing tests**

In `KiAggressivitaetTests.cs` ergänzen:

```csharp
        [Theory]
        [InlineData(1, -2)]     // frueher "Niedrig" (1 % ist der untere Anschlag): -2 + 7*1/50 = -2
        [InlineData(50, 5)]     // frueher "Mittel" - der Regressionsanker
        [InlineData(100, 10)]   // frueher "Hoch"
        [InlineData(25, 1)]     // stufenlos: -2 + (5-(-2))*25/50 = 1,5 -> 1 (Ganzzahl)
        [InlineData(75, 7)]     // stufenlos: 5 + (10-5)*25/50 = 7,5 -> 7
        public void Die_Interpolation_trifft_die_alten_Stufen_und_dazwischen(int prozent, int erwartet)
        {
            TestSpielwelt.Starte();
            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = prozent;

            // Wertetripel des Gerichts-Urteilsfaktors (frueher Niedrig/Mittel/Hoch = -2/+5/+10).
            Assert.Equal(erwartet, SW.Dynamisch.InterpoliereNachAggressivitaet(-2, 5, 10));
        }

        [Fact]
        public void Die_Interpolation_ist_bei_fuenfzig_Prozent_exakt_der_Mittelwert()
        {
            // Gilt fuer alle drei realen Wertetripel: bei 50 % aendert sich gegenueber heute nichts.
            TestSpielwelt.Starte();
            SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = 50;

            Assert.Equal(5, SW.Dynamisch.InterpoliereNachAggressivitaet(-2, 5, 10));    // Urteilsfaktor
            Assert.Equal(10, SW.Dynamisch.InterpoliereNachAggressivitaet(-2, 10, 20));  // Absetz-Sympathie
            Assert.Equal(5, SW.Dynamisch.InterpoliereNachAggressivitaet(2, 5, 12));     // Anklage-Faktor
        }

        [Fact]
        public void Die_Interpolation_steigt_monoton()
        {
            TestSpielwelt.Starte();
            int vorheriger = int.MinValue;

            for (int prozent = 1; prozent <= 100; prozent++)
            {
                SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = prozent;
                int wert = SW.Dynamisch.InterpoliereNachAggressivitaet(2, 5, 12);

                Assert.True(wert >= vorheriger, $"Bei {prozent} % sank der Wert von {vorheriger} auf {wert}");
                vorheriger = wert;
            }
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiAggressivitaetTests"`
Expected: Build-Fehler — `DynamischeSpieldaten` kennt `InterpoliereNachAggressivitaet` nicht.

- [ ] **Step 3: Helfer ergänzen**

In `DynamischeSpieldaten.cs`, in der in Task 1 angelegten Region `#region KiAggressivitaet`, direkt nach `GetKiAggressivitaetProzent()` einfügen:

```csharp
        /// <summary>
        /// Interpoliert einen Wert stufenlos anhand der eingestellten KI-Aggressivität zwischen drei
        /// Stützpunkten: <paramref name="beiNull"/> bei 0 %, <paramref name="beiFuenfzig"/> bei 50 %
        /// und <paramref name="beiHundert"/> bei 100 %. Die Stützpunkte entsprechen den früheren
        /// Stufen Niedrig/Mittel/Hoch, sodass 50 % das bisherige Verhalten reproduziert.
        /// </summary>
        public int InterpoliereNachAggressivitaet(int beiNull, int beiFuenfzig, int beiHundert)
        {
            int prozent = GetKiAggressivitaetProzent();

            return prozent <= 50
                ? beiNull + (beiFuenfzig - beiNull) * prozent / 50
                : beiFuenfzig + (beiHundert - beiFuenfzig) * (prozent - 50) / 50;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiAggressivitaetTests"`
Expected: PASS.

- [ ] **Step 5: Die drei `switch`-Blöcke ersetzen**

**a)** `GerichtsverhandlungManager.cs`, Zeilen 577–588 (der `switch` über `AggressivitaetKISpieler`, der `faktor` anpasst) ersetzen durch:

```csharp
            faktor += SW.Dynamisch.InterpoliereNachAggressivitaet(-2, 5, 10);
```

**b)** `DynamischeSpieldaten.cs`, der `switch` bei ~Zeile 2280, der `maxAbsetzSympathie` anpasst (suche nach `int maxAbsetzSympathie = SW.Statisch.GetMaxAbsetzSympathie();`). Die Zeile mit der Initialisierung bleibt, der darauffolgende `switch`-Block wird ersetzt durch:

```csharp
                maxAbsetzSympathie += InterpoliereNachAggressivitaet(-2, 10, 20);
```

**c)** `DynamischeSpieldaten.cs`, der `switch` bei ~Zeile 2861, der `faktor` setzt (suche nach `int faktor = 5;` gefolgt vom `switch`). Beide — die Initialisierung `int faktor = 5;` und der `switch` — werden zusammen ersetzt durch:

```csharp
            int faktor = InterpoliereNachAggressivitaet(2, 5, 12);
```

**Die `using`-Zeilen bleiben unangetastet.** Der Namespace `Conspiratio.Lib.Gameplay.Einstellungen` verschwindet *nicht* — `Spieleinstellungen.cs` und `EnumAuftrag.cs` liegen weiterhin darin; gelöscht wird in Step 6 nur die eine Datei `EnumSchwierigkeitsgrad.cs`. `using Conspiratio.Lib.Gameplay.Einstellungen;` in `DynamischeSpieldaten.cs:7` und `GerichtsverhandlungManager.cs:5` bleibt also gültig (in `DynamischeSpieldaten` wird es ohnehin für `EnumAuftrag` gebraucht). Nichts entfernen — falls eine dieser Zeilen danach ungenutzt sein sollte, ist das eine harmlose, warnungsfreie Kleinigkeit, kein Fehler.

- [ ] **Step 6: Enum und totes Feld entfernen**

Property `AggressivitaetKISpieler` samt XML-Kommentar aus `Spieleinstellungen.cs` löschen (aktuell Zeilen 12–15). Datei `Conspiratio.Lib/Gameplay/Einstellungen/EnumSchwierigkeitsgrad.cs` löschen:

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git rm Conspiratio.Lib/Gameplay/Einstellungen/EnumSchwierigkeitsgrad.cs
```

Danach mit `grep -rn "EnumSchwierigkeitsgrad\|AggressivitaetKISpieler" .` im Lib-Repo gegenprüfen, dass keine Referenz übrig ist.

- [ ] **Step 7: Full suite**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln"`
Expected: PASS.

**Wichtig zur Einordnung dieses Schritts:** Für die drei hier umgestellten Mechaniken
(Gerichtsurteil, Amtsenthebungs-Schwelle, Anklage-Häufigkeit) existieren **keine** Tests —
weder vorher noch nachher. Eine grüne Suite beweist hier also *nicht*, dass die Umstellung
korrekt ist; sie zeigt nur, dass nichts anderes kaputtgegangen ist. Die eigentliche
Absicherung besteht aus zwei Teilen:

1. `Die_Interpolation_ist_bei_fuenfzig_Prozent_exakt_der_Mittelwert` (Step 1) pinnt alle
   drei Wertetripel fest: Bei 50 % liefert der Helfer exakt `+5`, `+10` und `5` — die alten
   `Mittel`-Werte.
2. Beim Ersetzen (Step 5) muss **pro Aufrufstelle geprüft werden, dass das übergebene
   Wertetripel dem alten `switch` entspricht** — `Niedrig` → erster Parameter, `Mittel` →
   zweiter, `Hoch` → dritter. Ein vertauschtes Tripel wäre der wahrscheinlichste Fehler
   dieser Task und würde von keinem Test gefangen. Vor dem Commit die drei alten
   `switch`-Blöcke im Diff (`git diff`) gegen die drei neuen Einzeiler halten.

Direkte Tests für diese drei Pfade sind bewusst nicht Teil des Plans: Alle drei stecken tief
in aufwendig aufzusetzenden Abläufen (eine laufende Gerichtsverhandlung, eine Ämterstruktur
mit Untergebenen, freie Gerichtsverhandlungs-Slots plus passenden Kläger), und der jeweils
geänderte Code ist eine einzige Zeile, deren Wert unter Punkt 1 bereits unabhängig
festgenagelt ist. Der Aufwand stünde in keinem Verhältnis.

- [ ] **Step 8: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs Conspiratio.Lib/Gameplay/Justiz/GerichtsverhandlungManager.cs Conspiratio.Lib/Gameplay/Einstellungen/Spieleinstellungen.cs Conspiratio.Lib.Tests/KiAggressivitaetTests.cs
git commit -m "Gerichte, Amtsenthebung und Anklagen folgen der Aggressivitaet stufenlos

InterpoliereNachAggressivitaet ersetzt die drei switch-Bloecke ueber den
EnumSchwierigkeitsgrad; die alten Stufenwerte werden zu Stuetzpunkten bei
0/50/100 %, sodass 50 % exakt dem bisherigen Mittel entspricht. Der Enum
und das nie gesetzte Feld AggressivitaetKISpieler entfallen ersatzlos."
```

---

## Task 4: Lib-CHANGELOG und Versionsbump

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\CHANGELOG.md`
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Conspiratio.Lib.csproj` (Zeile mit `<Version>`)

**Interfaces:** keine (Dokumentation/Metadaten).

- [ ] **Step 1: CHANGELOG-Einträge ergänzen**

In `CHANGELOG.md` unter der bestehenden Überschrift `## [Unreleased]` (ganz oben in der Datei, Zeile 3) an die dortigen `**[DE]**`- und `**[EN]**`-Listen je einen weiteren Bullet-Punkt **anhängen** — kein neuer Versions-Header, kein Datum (Konvention dieses Repos: ein einziger laufender `[Unreleased]`-Block).

Unter `**[DE]**`:
```
- Eine einzige Einstellung für die Aggressivität der KI-Spieler: `Spieleinstellungen.KiAggressivitaetProzent` (1–100 %, Standard 50) steuert jetzt sämtliche feindseligen und militärischen KI-Aktivitäten. Sie ersetzt das nie gesetzte, faktisch tote Feld `AggressivitaetKISpieler` (samt `EnumSchwierigkeitsgrad`, beide entfallen ersatzlos) und die zu eng gefasste `KiAktivitaetProzent`, die nur Räuberlager und Zollburgen betraf. Die Einstellung wirkt an zwei Stellen: `KISpieler.GetBosheit` verschiebt den ausgewürfelten Charakterwert um `Aggressivität − 50` (auf 0–100 begrenzt) und erfasst damit alle davon abhängigen Mechaniken — Beleidigungen, Duelle samt Gegnerstärke, Sabotage, Anschwärzen, KI-Straftaten und Deliktpunkte —, während `DynamischeSpieldaten.InterpoliereNachAggressivitaet` die früheren Stufen Niedrig/Mittel/Hoch bei Gerichtsurteilen, Amtsenthebungen und Anklagen zu einer stufenlosen Skala macht. Bei 50 % verhält sich jede einzelne Stelle exakt wie bisher. Weil die Bosheit beim *Lesen* moduliert wird und nicht beim Auswürfeln, wirkt eine Änderung sofort und rückwirkend – auf bereits existierende KI-Spieler, im laufenden Spiel und in geladenen Spielständen; der gespeicherte Charakterwert bleibt unangetastet, die Streuung zwischen den KIs erhalten. Alte Spielstände (Feld fehlt, Wert 0) werden wie 50 % behandelt.
```

Unter `**[EN]**`:
```
- A single setting for AI aggressiveness: `Spieleinstellungen.KiAggressivitaetProzent` (1–100 %, default 50) now governs all hostile and military AI activity. It replaces the never-assigned, effectively dead `AggressivitaetKISpieler` field (along with `EnumSchwierigkeitsgrad`, both dropped) and the too-narrow `KiAktivitaetProzent`, which only affected robber camps and toll castles. The setting takes effect in two places: `KISpieler.GetBosheit` shifts the AI's rolled character value by `aggressiveness − 50` (clamped to 0–100), thereby covering every mechanic that depends on it — insults, duels including opponent strength, sabotage, slander, AI crimes and guilt points — while `DynamischeSpieldaten.InterpoliereNachAggressivitaet` turns the former Low/Medium/High tiers for court verdicts, removals from office and charges into a continuous scale. At 50 % every single one of these behaves exactly as before. Because malice is modulated when *read* rather than baked in when rolled, a change takes effect immediately and retroactively – on existing AI players, in a running game and in loaded savegames; the stored character value is left untouched and the spread between AIs preserved. Old savegames (field absent, value 0) are treated as 50 %.
```

- [ ] **Step 2: Version bumpen**

In `Conspiratio.Lib.csproj` die `<Version>`-Zeile von `3.99.0` auf `4.0.0` ändern. Der Hauptversionssprung ist angemessen: Mit `AggressivitaetKISpieler` und `KiAktivitaetProzent` entfallen zwei öffentliche Felder ersatzlos, und `GetBosheit()` ändert seine Semantik — für Konsumenten der Bibliothek eine brechende Änderung.

- [ ] **Step 3: Build zur Kontrolle**

Run: `dotnet build "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln"`
Expected: Build erfolgreich, `.nupkg` mit Version 4.0.0 in `Conspiratio.Lib\bin\Debug`.

- [ ] **Step 4: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add CHANGELOG.md Conspiratio.Lib/Conspiratio.Lib.csproj
git commit -m "Version 4.0.0: eine einzige KI-Aggressivitaets-Einstellung"
```

---

## Task 5: Lokale Lib-Version einbinden und Godot-Client umstellen

**Files:**
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj` (Zeile mit `PackageReference Include="Conspiratio.Lib"`)
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\assets\scripts\managers\ClientSettings.cs:91-96`
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\assets\scripts\OptionenDialog.cs` (Felder ~31, `OnReady` ~53, `ShowDialog` ~91-92, Handler ~145-159)
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\scenes\dialogs\OptionenDialog.tscn:138,147`
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\assets\scripts\NewLocalGameMenu.cs:118-119`

**Interfaces:**
- Consumes: `Spieleinstellungen.KiAggressivitaetProzent` (Task 1), Lib-Version 4.0.0 (Task 4).

Version 4.0.0 ist noch nicht auf nuget.org (das passiert erst mit einem echten GitHub-Release, außerhalb dieses Plans). Sie muss daher zuerst lokal in den NuGet-Cache gelegt werden.

- [ ] **Step 1: Lokale Lib-Version in den globalen NuGet-Cache legen**

`dotnet restore` mit `--source` auf den lokalen Debug-Ordner füllt den globalen Cache — aber nur über ein Projekt, das die Lib per `PackageReference` (nicht `ProjectReference`) einbindet. Das Test-Projekt der Lib taugt dafür **nicht**. Daher ein Wegwerf-Projekt anlegen, restoren und wieder löschen:

```bash
mkdir -p /tmp/nuget-cache-harness && cd /tmp/nuget-cache-harness
cat > harness.csproj <<'PROJ'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Conspiratio.Lib" Version="4.0.0" />
  </ItemGroup>
</Project>
PROJ
rm -rf ~/.nuget/packages/conspiratio.lib/4.0.0
dotnet restore harness.csproj --source "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib/bin/Debug"
ls ~/.nuget/packages/conspiratio.lib/4.0.0
cd / && rm -rf /tmp/nuget-cache-harness
```

Expected: `ls` zeigt den entpackten Paketinhalt. Schlägt der Restore fehl, wurde in Task 4 kein `.nupkg` gebaut — dort `dotnet build` nachholen.

- [ ] **Step 2: PackageReference anheben und bauen**

In `Conspiratio.Godot.csproj` die Version des `Conspiratio.Lib`-`PackageReference` auf `4.0.0` setzen.

Run: `dotnet build "C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj"`
Expected: **Build-Fehler** in `ClientSettings.cs`/`NewLocalGameMenu.cs`, weil `Spieleinstellungen.KiAktivitaetProzent` nicht mehr existiert. Genau das wird in Step 3 behoben — der Fehler ist hier die Bestätigung, dass die neue Lib-Version tatsächlich gezogen wurde.

- [ ] **Step 3: Client umstellen**

**a)** `ClientSettings.cs`, Property `KiAktivitaetProzent` samt Kommentar (Zeilen 91–96) ersetzen durch:

```csharp
	/// <summary>Aggressivität der KI-Spieler als Vorgabe für neue Spiele in Prozent (1–100, Standard 50).</summary>
	public static int KiAggressivitaetProzent
	{
		get => GetInt("optionen", "ki_aggressivitaet_prozent", 50);
		set => SetValue("optionen", "ki_aggressivitaet_prozent", value);
	}
```

**b)** `OptionenDialog.tscn`: Node `LabelAgg` behält seinen Namen (er heißt bereits neutral), aber sein `text` (Zeile ~145) wird zu `text = "Aggressivität der KI-Spieler: 50 %"`. Node `SliderKiAktivitaet` (Zeile 147) umbenennen in `SliderKiAggressivitaet`.

**c)** `OptionenDialog.cs`: Feld `_sliderKiAktivitaet` → `_sliderKiAggressivitaet` (Deklaration ~Zeile 31); `GetNode<HSlider>("Rahmen/SliderKiAktivitaet")` → `GetNode<HSlider>("Rahmen/SliderKiAggressivitaet")` (~Zeile 53); Signal-Anbindung (~Zeile 68) auf den neuen Handler-Namen; in `ShowDialog` (~Zeilen 91–92) beide `ClientSettings.KiAktivitaetProzent` → `ClientSettings.KiAggressivitaetProzent` und `AktualisiereKiAktivitaetLabel` → `AktualisiereKiAggressivitaetLabel`. Die beiden Methoden (~Zeilen 145–159) ersetzen durch:

```csharp
	private void OnKiAggressivitaetGeaendert(double wert)
	{
		int prozent = (int)wert;
		AktualisiereKiAggressivitaetLabel(prozent);

		if (_laedt)
			return;

		ClientSettings.KiAggressivitaetProzent = prozent;

		// Zusätzlich in den laufenden Spielstand schreiben, damit der Regler sofort wirkt und nicht
		// erst im nächsten neuen Spiel. Der Dialog ist auch direkt aus dem Hauptmenü erreichbar,
		// wo noch kein Spielstand existiert (er entsteht erst in NeuInitialisieren, aufgerufen beim
		// Anlegen oder Laden eines Spiels) – ohne laufendes Spiel gibt es nichts, worauf die
		// Einstellung sofort wirken könnte, und ClientSettings liefert den Wert bei der
		// Spielerstellung ohnehin.
		if (SW.Dynamisch.Spielstand != null)
			SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = prozent;
	}

	private void AktualisiereKiAggressivitaetLabel(int prozent)
	{
		_labelAgg.Text = "Aggressivität der KI-Spieler: " + prozent + " %";
	}
```

Prüfen, ob `OptionenDialog.cs` bereits `using Conspiratio.Lib.Gameplay.Spielwelt;` importiert (für `SW`); falls nicht, ergänzen.

**d)** `NewLocalGameMenu.cs`, Zeilen 118–119 ersetzen durch:

```csharp
		// Die in den Optionen gewählte KI-Aggressivität (Prozent) als Vorgabe für dieses Spiel übernehmen
		SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = ClientSettings.KiAggressivitaetProzent;
```

- [ ] **Step 4: Build und Smoke-Test**

Run: `dotnet build "C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj"`
Expected: 0 Fehler (eine vorbestehende `CS4014`-Warnung in `Mainmenu.cs` ist erwartet und unabhängig).

```powershell
$godot = "C:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
& $godot --headless --path "C:\Projekte\Godot\Conspiratio.Godot" --import
Start-Process $godot -ArgumentList '--headless','--path','C:\Projekte\Godot\Conspiratio.Godot','--quit','res://scenes/Main.tscn' -Wait -PassThru -RedirectStandardOutput out.log -RedirectStandardError err.log
Get-Content err.log
```

Expected: `err.log` leer bzw. ohne Stack Traces. Ein Fehler wie „Node not found: Rahmen/SliderKiAggressivitaet" bedeutet, dass Szene und Skript auseinanderdriften — dann Step 3b/3c gegenprüfen.

- [ ] **Step 5: E2E-Durchlauf**

```bash
cd "C:/Projekte/Godot/Conspiratio.Godot"
"/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=10 --spieler=2 --verbose
```

Expected: Exit-Code 0, „E2E-Durchlauf bestanden."  Der Durchlauf prüft, dass die Spielerstellung mit dem umbenannten Feld weiterhin funktioniert und der Zugfluss nicht hängt.

- [ ] **Step 6: Commit**

```bash
cd "C:/Projekte/Godot/Conspiratio.Godot"
git add Conspiratio.Godot.csproj assets/scripts/managers/ClientSettings.cs assets/scripts/OptionenDialog.cs assets/scripts/NewLocalGameMenu.cs scenes/dialogs/OptionenDialog.tscn
git commit -m "Regler heisst jetzt Aggressivitaet und wirkt sofort (Conspiratio.Lib 4.0.0)

Der Optionen-Regler steuert nicht mehr nur die Stuetzpunkt-Aktivitaet,
sondern saemtliche KI-Feindseligkeit, und schreibt seinen Wert zusaetzlich
in den laufenden Spielstand - dadurch wirkt eine Aenderung sofort statt
erst im naechsten neuen Spiel."
```

---

## Task 6: Godot-CHANGELOG

**Files:**
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\CHANGELOG.md`

**Interfaces:** keine (Dokumentation).

- [ ] **Step 1: DE/EN-Einträge ergänzen**

In `CHANGELOG.md` unter `## 1.0.0-godot` → `_Unreleased_` → `### [DE]` → `#### Geändert` als ersten Bullet einfügen:

```
- Aggressivität der KI-Spieler: Der Regler in den Optionen hieß „Aktivität der KI-Spieler" und steuerte tatsächlich nur, wie oft Räuberlager und Zollburgen aktiv wurden. Er heißt jetzt „Aggressivität der KI-Spieler" und steuert sämtliche Feindseligkeit der KI: Beleidigungen und Duelle, Sabotage und Anschwärzen, KI-Straftaten, Anklagen, Gerichtsurteile, Amtsenthebungen und weiterhin die Militärstützpunkte. Außerdem wirkt eine Änderung jetzt sofort im laufenden Spiel – bisher wurde der Wert nur beim Anlegen eines neuen Spiels übernommen, sodass Verstellen während einer Partie folgenlos blieb. 50 % entspricht dem bisherigen Verhalten. Benötigt Conspiratio.Lib 4.0.0
```

Im zugehörigen `### [EN]` → `#### Changed` als ersten Bullet:

```
- AI aggressiveness: the options slider used to be called "AI player activity" and in fact only controlled how often robber camps and toll castles acted. It is now called "AI player aggressiveness" and governs all AI hostility: insults and duels, sabotage and slander, AI crimes, charges, court verdicts, removals from office, and still the military bases. A change now also takes effect immediately in a running game – previously the value was only adopted when creating a new game, so adjusting it mid-game did nothing. 50 % matches the previous behaviour. Requires Conspiratio.Lib 4.0.0
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Projekte/Godot/Conspiratio.Godot"
git add CHANGELOG.md
git commit -m "CHANGELOG: KI-Aggressivitaets-Einstellung"
```

---

## Nach Abschluss

Die Veröffentlichung von Conspiratio.Lib 4.0.0 auf nuget.org (GitHub-Release) ist ein manueller Schritt außerhalb dieses Plans. Bis dahin schlägt die CI dieses Repos (`build.yml`) fehl, weil sie die referenzierte Version dort nicht findet — erwartetes Verhalten laut CLAUDE.md, kein Grund, am Plan etwas zu ändern.

Offen für später (bewusst nicht Teil dieses Plans, siehe Spec Abschnitt 9): Ob sich 100 % im Spiel tatsächlich stimmig anfühlt, lässt sich erst an einem langen Durchlauf beurteilen. Die lineare Verschiebung sitzt in einer einzigen Formel (`KISpieler.GetBosheit`) und ist dort leicht nachjustierbar — messen statt schätzen.
