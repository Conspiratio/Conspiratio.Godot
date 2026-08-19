# Aggressive KI: Sabotage + Anschwärzen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** KI-Spieler mit schlechter Beziehung zu einem menschlichen Spieler setzen Saboteure gegen dessen Besitz ein oder schwärzen ihn bei anderen Würdenträgern an — verstärkt, wenn sie Beweise gegen ihn haben.

**Architecture:** Zwei Repos, Lib zuerst. In **Conspiratio.Lib**: eine neue savegame-sichere Datenstruktur auf `HumSpieler` (Spiegelbild der bestehenden Sabotage), eine Entkopplung der bestehenden Anschwärz-Logik von ihrem UI-Zustand, und ein neuer `AggressionManager`, der pro Zugbeginn jede KI unabhängig auf Feindseligkeit prüft und bei Erfolg zwischen Sabotage und Anschwärzen wählt. In **Conspiratio.Godot**: der neue Manager wird an derselben Stelle wie die bestehende KI-Beleidigung aufgerufen (`Kontor.NaechstenSpielerAnkuendigen`), die laufende Sabotage-Wirkung läuft neben der bestehenden Sabotage-Meldung (`ZeigeVerdeckteEreignisse`).

**Tech Stack:** C# / .NET (netstandard2.0 Lib, .NET 8 Godot-Client), xUnit (Lib-Tests), Godot 4.7 headless smoke test.

**Spec:** [docs/ki-aggression-sabotage-anschwaerzen-konzept.md](../ki-aggression-sabotage-anschwaerzen-konzept.md)

## Global Constraints

- Alle neuen UI-Texte und Meldungen sind **Deutsch** (Domänen-Konvention des Projekts).
- Savegame-Kompatibilität: das neue Feld auf `HumSpieler` darf **nicht** im Konstruktor initialisiert werden — nur per Lazy-Init-Accessor, sonst zerreißt es alte Spielstände (siehe CLAUDE.md „Savegame compatibility ist eine Lazy-Init-Konvention").
- Die KI zahlt **keine** laufenden Kosten für eine gegen einen Menschen laufende Sabotage — `AktiveSabotagen.SetKosten` bleibt bei der gegnerischen Sabotage auf 0 (Spec Abschnitt 4, „Abweichung von der Spiegelung").
- `PruefeKiBeleidigtSpieler` (`FechtDuellManager.cs:209`) bleibt in seinem äußeren Verhalten unverändert — nur die Chance-Formel wird in eine wiederverwendbare Methode extrahiert (Task 1).
- Sabotage-Initiierung ist **covert** (keine Meldung); Anschwärzen-Ergebnisse werden **sofort** im Zugbeginn-Block gemeldet, nicht verzögert.
- Lib-Commits vor Godot-Commits; der Godot-Commit referenziert die Lib-Version im Subject (`… (Conspiratio.Lib x.y.z)`).
- Alle Pfade unten sind vollständige Pfade in zwei getrennten Git-Repos:
  - Lib: `D:\Projekte\C# Projekte\Conspiratio.Lib\`
  - Godot: `C:\Projekte\Godot\Conspiratio.Godot\` (dieses Repo)

---

## Task 1: Feindseligkeits-Formel aus `FechtDuellManager` extrahieren

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Allgemein\FechtDuellManager.cs`
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\DuellTests.cs`

**Interfaces:**
- Produces: `public static int FechtDuellManager.BerechneKiFeindseligkeitChance(int beziehungZuTaeter, int bosheit)` — von `AggressionManager` (Task 5) wiederverwendet.

- [ ] **Step 1: Write the failing test**

In `DuellTests.cs`, neue Testklasse am Ende der Datei (vor der schließenden `}` des Namespace) ergänzen:

```csharp
    /// <summary>
    /// Die KI-Feindseligkeits-Formel (Issue: aggressive KI) wird aus PruefeKiBeleidigtSpieler
    /// extrahiert, damit AggressionManager sie für Sabotage/Anschwärzen wiederverwenden kann.
    /// </summary>
    public class KiFeindseligkeitTests
    {
        [Theory]
        [InlineData(50, 0, 0)]    // neutrale Beziehung, keine Bosheit -> keine Chance
        [InlineData(0, 0, 6)]     // maximale Feindseligkeit (50), keine Bosheit: 50*13/100 = 6
        [InlineData(0, 100, 7)]   // wie oben plus volle Bosheit: (650+100)/100 = 7
        [InlineData(80, 100, 1)]  // gute Beziehung, aber hohe Bosheit: (0*13+100)/100 = 1
        public void Folgt_der_Formel(int beziehung, int bosheit, int erwarteteChance)
        {
            Assert.Equal(erwarteteChance, FechtDuellManager.BerechneKiFeindseligkeitChance(beziehung, bosheit));
        }
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiFeindseligkeitTests"`
Expected: Build-Fehler „'FechtDuellManager' does not contain a definition for 'BerechneKiFeindseligkeitChance'".

- [ ] **Step 3: Extract the method and use it in `PruefeKiBeleidigtSpieler`**

In `FechtDuellManager.cs`, `using System;` zur using-Liste am Dateianfang hinzufügen (für `Math.Max`). Dann direkt vor `PruefeKiBeleidigtSpieler` (aktuell Zeile ~209) einfügen:

```csharp
        /// <summary>
        /// Gemeinsame Feindseligkeits-Chance-Formel für spontane feindliche KI-Aktionen gegen einen
        /// Menschen (Beleidigung hier, Sabotage/Anschwärzen in <c>AggressionManager</c>): hängt vor
        /// allem an der Beziehung zum Menschen und steigt erst unterhalb von „neutral" (50); Bosheit
        /// spielt nur eine kleine Rolle.
        /// </summary>
        public static int BerechneKiFeindseligkeitChance(int beziehungZuTaeter, int bosheit)
        {
            int feindseligkeit = Math.Max(0, NeutraleBeziehung - beziehungZuTaeter);
            return (feindseligkeit * KiBeleidigtGewichtBeziehung + bosheit * KiBeleidigtGewichtBosheit) / 100;
        }
```

Dann in `PruefeKiBeleidigtSpieler` die bisherige Inline-Berechnung ersetzen:

```csharp
            // Feindseligkeit zählt erst unterhalb von „neutral"; Bosheit als kleiner Zuschlag.
            var feind = SW.Dynamisch.GetKIwithID(feindKi);
            int chance = BerechneKiFeindseligkeitChance(minBeziehung, feind.GetBosheit());

            return SW.Statisch.Rnd.Next(0, 100) < chance ? feindKi : 0;
```

(Die Zeilen `int feindseligkeit = ...` und die alte `int chance = (...)`-Berechnung entfallen ersatzlos.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~KiFeindseligkeitTests"`
Expected: PASS (4/4).

- [ ] **Step 5: Run the full Duell test suite to confirm no regression**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~DuellTests|FullyQualifiedName~SpielstandKompatibilitaetTests"`
Expected: PASS (alle bestehenden Duell-Tests weiterhin grün).

- [ ] **Step 6: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Allgemein/FechtDuellManager.cs Conspiratio.Lib.Tests/DuellTests.cs
git commit -m "Feindseligkeits-Formel aus PruefeKiBeleidigtSpieler extrahiert

Wird von AggressionManager fuer Sabotage/Anschwaerzen wiederverwendet
(Vorbereitung, Verhalten von PruefeKiBeleidigtSpieler unveraendert)."
```

---

## Task 2: `HumSpieler` — Datenstruktur für gegnerische Sabotage

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Personen\HumSpieler.cs`
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\DuellTests.cs` (Klasse `SpielstandKompatibilitaetTests`)

**Interfaces:**
- Produces: `HumSpieler.GetGegnerischeSabotage(int taeterKiId): AktiveSabotagen`, `HumSpieler.GegnerischeSabotageEntfernen(int taeterKiId): void` — von `ZugNachrichtenManager` (Task 3) und `AggressionManager` (Task 5/6) genutzt.

- [ ] **Step 1: Write the failing test**

In `DuellTests.cs`, Klasse `SpielstandKompatibilitaetTests`, neue Testmethode ergänzen (nach `Ahnentafel_wird_bei_Bedarf_angelegt`):

```csharp
        [Fact]
        public void Gegnerische_Sabotage_wird_bei_Bedarf_angelegt()
        {
            TestSpielwelt.Starte();
            var spieler = WieAusAltemSpielstand();
            int kiId = SW.Statisch.GetMinKIID();

            Assert.Equal(0, spieler.GetGegnerischeSabotage(kiId).GetDauer());

            spieler.GetGegnerischeSabotage(kiId).SetDauer(5);
            Assert.Equal(5, spieler.GetGegnerischeSabotage(kiId).GetDauer());

            spieler.GegnerischeSabotageEntfernen(kiId);
            Assert.Equal(0, spieler.GetGegnerischeSabotage(kiId).GetDauer());
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~Gegnerische_Sabotage_wird_bei_Bedarf_angelegt"`
Expected: Build-Fehler „'HumSpieler' does not contain a definition for 'GetGegnerischeSabotage'".

- [ ] **Step 3: Add the field and accessors**

In `HumSpieler.cs`, direkt nach der bestehenden `#region Sabotage` (Zeile ~312–323, endet mit `AktiveSabotageEntfernen`) einfügen:

```csharp
        #region GegnerischeSabotage
        // Bewusst NICHT im Konstruktor initialisiert (anders als _aktiveSabotagen): das Feld muss bei
        // alten Spielständen als null ankommen und erst bei Bedarf angelegt werden (Lazy-Init-
        // Konvention, siehe CLAUDE.md „Savegame compatibility").
        private AktiveSabotagen[] _gegnerischeSabotagen;

        /// <summary>
        /// Sabotage, die die KI <paramref name="taeterKiId"/> gegen diesen Menschen laufen hat —
        /// Spiegelbild zu <see cref="GetAktiveSabotage"/>.
        /// </summary>
        public AktiveSabotagen GetGegnerischeSabotage(int taeterKiId)
        {
            if (_gegnerischeSabotagen == null)
                _gegnerischeSabotagen = new AktiveSabotagen[SW.Statisch.GetMaxKIID()];

            if (_gegnerischeSabotagen[taeterKiId] == null)
                _gegnerischeSabotagen[taeterKiId] = new AktiveSabotagen(0, 0);

            return _gegnerischeSabotagen[taeterKiId];
        }

        public void GegnerischeSabotageEntfernen(int taeterKiId)
        {
            GetGegnerischeSabotage(taeterKiId).SetDauer(0);
            GetGegnerischeSabotage(taeterKiId).SetKosten(0);
        }
        #endregion
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~SpielstandKompatibilitaetTests"`
Expected: PASS (4/4, inklusive der drei bestehenden Kompatibilitätstests).

- [ ] **Step 5: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs Conspiratio.Lib.Tests/DuellTests.cs
git commit -m "HumSpieler: Datenstruktur fuer gegnerische Sabotage

Spiegelbild zu _aktiveSabotagen (KI gegen Mensch statt Mensch gegen KI),
savegame-sicher per Lazy-Init. Noch ungenutzt, folgt in weiteren Commits."
```

---

## Task 3: Laufende Wirkung der gegnerischen Sabotage

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Allgemein\ZugNachrichtenManager.cs`
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\GegnerischeSabotageTests.cs` (neu)

**Interfaces:**
- Consumes: `HumSpieler.GetGegnerischeSabotage` / `GegnerischeSabotageEntfernen` (Task 2).
- Produces: `ZugNachrichtenManager.ErmittleGegnerischeSabotageNachrichten(): string` — von Godot `Kontor.cs` (Task 9) aufgerufen.

- [ ] **Step 1: Write the failing test**

Neue Datei `Conspiratio.Lib.Tests/GegnerischeSabotageTests.cs`:

```csharp
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>Laufende Wirkung einer Sabotage, die eine KI gegen den Menschen laufen hat.</summary>
    public class GegnerischeSabotageTests
    {
        [Fact]
        public void Ohne_laufende_Sabotage_passiert_nichts()
        {
            TestSpielwelt.Starte();
            Assert.Null(new ZugNachrichtenManager().ErmittleGegnerischeSabotageNachrichten());
        }

        [Fact]
        public void Schaden_trifft_ungefaehr_jedes_zweite_Jahr_und_zehrt_die_Dauer_auf()
        {
            TestSpielwelt.Starte(seed: 7);
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0);
            var mensch = SW.Dynamisch.GetAktHum();
            var manager = new ZugNachrichtenManager();

            int treffer = 0;
            const int versuche = 1000;

            for (int i = 0; i < versuche; i++)
            {
                mensch.GetGegnerischeSabotage(kiId).SetDauer(1);
                string meldung = manager.ErmittleGegnerischeSabotageNachrichten();

                if (meldung != null)
                {
                    treffer++;
                    Assert.Equal(0, mensch.GetGegnerischeSabotage(kiId).GetDauer());
                }
            }

            double rate = (double)treffer / versuche;
            Assert.InRange(rate, 0.40, 0.60); // Chance 1/2, grosse Stichprobe statt Einzelfall
        }

        [Fact]
        public void Verteidigungsprivileg_19_senkt_die_Trefferchance_auf_ein_Viertel()
        {
            TestSpielwelt.Starte(seed: 7);
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0);
            var mensch = SW.Dynamisch.GetAktHum();
            mensch.SetPrivilegX(19, true);
            var manager = new ZugNachrichtenManager();

            int treffer = 0;
            const int versuche = 2000;

            for (int i = 0; i < versuche; i++)
            {
                mensch.GetGegnerischeSabotage(kiId).SetDauer(1);
                if (manager.ErmittleGegnerischeSabotageNachrichten() != null)
                    treffer++;
            }

            double rate = (double)treffer / versuche;
            Assert.InRange(rate, 0.17, 0.33); // Chance 1/4
        }

        [Fact]
        public void Schaden_wird_vom_Vermoegen_des_Menschen_abgezogen()
        {
            TestSpielwelt.Starte(seed: 7);
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0);
            var mensch = SW.Dynamisch.GetAktHum();
            var manager = new ZugNachrichtenManager();

            int talerVorher = mensch.GetTaler();
            string meldung = null;

            for (int i = 0; i < 200 && meldung == null; i++)
            {
                mensch.GetGegnerischeSabotage(kiId).SetDauer(1);
                meldung = manager.ErmittleGegnerischeSabotageNachrichten();
            }

            Assert.NotNull(meldung);
            Assert.True(mensch.GetTaler() < talerVorher);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~GegnerischeSabotageTests"`
Expected: Build-Fehler „'ZugNachrichtenManager' does not contain a definition for 'ErmittleGegnerischeSabotageNachrichten'".

- [ ] **Step 3: Implement the method**

In `ZugNachrichtenManager.cs`, direkt nach `ErmittleSabotageNachrichten` (endet aktuell Zeile ~441 mit `#endregion`-losem `}`) einfügen:

```csharp
        /// <summary>
        /// Wickelt die Sabotagen ab, die KIs gegen den aktiven Menschen laufen haben — Spiegelbild zu
        /// <see cref="ErmittleSabotageNachrichten"/>: dieselbe Chance-/Schadensformel, hier gemildert
        /// durch die Verteidigungsprivilegien des Menschen statt der des KI-Ziels.
        /// </summary>
        /// <returns>Die zusammengefasste Meldung oder null, wenn nichts sabotiert wurde.</returns>
        [PublicAPI]
        public string ErmittleGegnerischeSabotageNachrichten()
        {
            var spieler = SW.Dynamisch.GetAktHum();
            var zeilen = new List<string>();

            for (int i = SW.Statisch.GetMinKIID(); i < SW.Statisch.GetMaxKIID(); i++)
            {
                if (spieler.GetGegnerischeSabotage(i).GetDauer() <= 0)
                    continue;

                int chance = 2;

                if (spieler.CheckPrivilegX(18))
                    chance = 3;
                if (spieler.CheckPrivilegX(19))
                    chance = 4;

                if (SW.Statisch.Rnd.Next(0, chance) != 1)
                    continue;

                int sabMaechtigkeit = SW.Statisch.Rnd.Next(1, 9);
                int schaden = spieler.GetGesamtVermoegen(SW.Dynamisch.GetAktiverSpieler()) * sabMaechtigkeit / 100;

                zeilen.Add("Es gelang " + SW.Dynamisch.GetSpWithID(i).GetKompletterName() +
                           "s Saboteuren, Euren Besitztümern " + SabotageStaerkeText(sabMaechtigkeit) +
                           " Schäden in Höhe von " + schaden + " anzurichten.");

                spieler.ErhoeheTaler(-schaden);

                spieler.GetGegnerischeSabotage(i).ReduziereDauerUmEins();

                if (spieler.GetGegnerischeSabotage(i).GetDauer() <= 0)
                    spieler.GegnerischeSabotageEntfernen(i);
            }

            return zeilen.Count > 0 ? string.Join("\n\n", zeilen) : null;
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~GegnerischeSabotageTests"`
Expected: PASS (4/4). Bei Flakiness durch die statistischen Grenzen (`InRange`) den Toleranzbereich prüfen, nicht den Seed anpassen — die Formel selbst ist deterministisch, nur die Stichprobe streut.

- [ ] **Step 5: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Allgemein/ZugNachrichtenManager.cs Conspiratio.Lib.Tests/GegnerischeSabotageTests.cs
git commit -m "Laufende Wirkung der gegnerischen Sabotage (KI gegen Mensch)

Spiegelbild zu ErmittleSabotageNachrichten: gleiche Chance-/Schadensformel,
Verteidigungsprivilegien 18/19 wirken jetzt auch fuer den Menschen als Opfer."
```

---

## Task 4: Anschwärzen entkoppeln und mit Beweisgewichtung versehen

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Gameplay\Spielwelt\DynamischeSpieldaten.cs:1749-1814` (Region `Anschwaerzen`)
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\AnschwaerzenTests.cs` (neu)

**Interfaces:**
- Produces: `DynamischeSpieldaten.AnschwaerzenAusfuehren(int taeterId, int x, int y): string` (null = folgenlos) — von `AggressionManager` (Task 6) und der bestehenden `Anschwaerzen(int id)` genutzt.
- `Anschwaerzen(int id)` bleibt öffentlich mit unverändeter Signatur (UI-Wrapper, Godot ruft ihn wie bisher).

- [ ] **Step 1: Write the failing tests**

Neue Datei `Conspiratio.Lib.Tests/AnschwaerzenTests.cs`:

```csharp
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Anschwärzen (DynamischeSpieldaten.AnschwaerzenAusfuehren): entkoppelt vom UI-Zustand, damit
    /// sowohl ein menschlicher als auch ein KI-Täter es nutzen können. Beweise senken die
    /// Glaubwürdigkeits-Schwelle (Issue: aggressive KI).
    /// </summary>
    public class AnschwaerzenTests
    {
        [Fact]
        public void Ohne_Beweise_glaubt_der_Adressat_erst_ab_Beziehung_80()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int opfer = TestSpielwelt.SetzeKiGegner(0, 0);
            int adressat = TestSpielwelt.SetzeKiGegner(1, 0);

            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(menschId, 79);
            Assert.Contains("kein Wort", SW.Dynamisch.AnschwaerzenAusfuehren(menschId, opfer, adressat));

            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(menschId, 80);
            Assert.Contains("Glauben", SW.Dynamisch.AnschwaerzenAusfuehren(menschId, opfer, adressat));
        }

        [Fact]
        public void Beweise_senken_die_Schwelle_beim_menschlichen_Taeter()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int opfer = TestSpielwelt.SetzeKiGegner(0, 0);
            int adressat = TestSpielwelt.SetzeKiGegner(1, 0);

            TestSpielwelt.GibBeweise(opfer, 10); // min(10*3,30)=30 -> Schwelle 80-30=50
            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(menschId, 55);

            Assert.Contains("Glauben", SW.Dynamisch.AnschwaerzenAusfuehren(menschId, opfer, adressat));
        }

        [Fact]
        public void KI_Taeter_nutzt_die_Deliktpunkte_des_Opfers_als_Beweis()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int anklaeger = TestSpielwelt.SetzeKiGegner(0, 0);
            int adressat = TestSpielwelt.SetzeKiGegner(1, 0);

            SW.Dynamisch.GetHumWithID(menschId).SetDeliktpunkte(10); // Opfer ist der Mensch
            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(anklaeger, 55);

            Assert.Contains("Glauben", SW.Dynamisch.AnschwaerzenAusfuehren(anklaeger, menschId, adressat));
        }

        [Fact]
        public void Glaubt_der_Adressat_nicht_und_das_Opfer_ist_ein_Mensch_bleibt_es_folgenlos()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int anklaeger = TestSpielwelt.SetzeKiGegner(0, 0);
            int adressat = TestSpielwelt.SetzeKiGegner(1, 0);

            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(anklaeger, 0);

            Assert.Null(SW.Dynamisch.AnschwaerzenAusfuehren(anklaeger, menschId, adressat));
        }

        [Fact]
        public void Glaubt_der_Adressat_nicht_und_das_Opfer_ist_eine_KI_wird_es_dem_Opfer_gemeldet()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int opfer = TestSpielwelt.SetzeKiGegner(0, 0);
            int adressat = TestSpielwelt.SetzeKiGegner(1, 0);

            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(menschId, 0);

            string meldung = SW.Dynamisch.AnschwaerzenAusfuehren(menschId, opfer, adressat);

            Assert.Contains("kein Wort", meldung);
            Assert.True(SW.Dynamisch.GetKIwithID(opfer).GetBeziehungZuKIX(menschId) < 0);
        }

        [Fact]
        public void Man_kann_niemanden_bei_sich_selbst_anschwaerzen()
        {
            TestSpielwelt.Starte();
            int opfer = TestSpielwelt.SetzeKiGegner(0, 0);

            string meldung = SW.Dynamisch.AnschwaerzenAusfuehren(SW.Dynamisch.GetAktiverSpieler(), opfer, opfer);
            Assert.Contains("sich selbst", meldung);
        }

        [Fact]
        public void Der_UI_Wrapper_funktioniert_weiterhin_zweistufig()
        {
            TestSpielwelt.Starte();
            int opfer = TestSpielwelt.SetzeKiGegner(0, 0);
            int adressat = TestSpielwelt.SetzeKiGegner(1, 0);
            SW.Dynamisch.GetKIwithID(adressat).SetBeziehungZuX(SW.Dynamisch.GetAktiverSpieler(), 100);

            Assert.Equal(0, SW.Dynamisch.GetAnschwaerzID());
            SW.Dynamisch.Anschwaerzen(opfer);
            Assert.Equal(opfer, SW.Dynamisch.GetAnschwaerzID());

            SW.Dynamisch.Anschwaerzen(adressat);
            Assert.Equal(0, SW.Dynamisch.GetAnschwaerzID()); // zweiter Schritt setzt zurueck
            Assert.True(SW.Dynamisch.GetKIwithID(adressat).GetBeziehungZuKIX(opfer) < 0); // Wirkung kam an
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~AnschwaerzenTests"`
Expected: Build-Fehler „'DynamischeSpieldaten' does not contain a definition for 'AnschwaerzenAusfuehren'".

- [ ] **Step 3: Refactor `Anschwaerzen` in `DynamischeSpieldaten.cs`**

Den gesamten Inhalt der Region `#region Anschwaerzen` (Zeilen 1749–1814) ersetzen durch:

```csharp
        #region Anschwaerzen
        // Transienter Merker für den zweistufigen Anschwärz-Vorgang ("X bei Y anschwärzen").
        private int _anschwaerzID;

        /// <summary>ID des Spielers, der gerade angeschwärzt werden soll (0 = kein Vorgang aktiv).</summary>
        public int GetAnschwaerzID() => _anschwaerzID;

        /// <summary>Setzt bzw. löscht (0) den laufenden Anschwärz-Vorgang.</summary>
        public void SetAnschwaerzID(int id) => _anschwaerzID = id;

        /// <summary>
        /// Anschwärzen aus dem Hinterzimmer (zweistufig): Der erste Klick wählt den Anzuschwärzenden X,
        /// der zweite den Adressaten Y. Reine UI-Zustandsführung; die Wirkung steckt in
        /// <see cref="AnschwaerzenAusfuehren"/>.
        /// </summary>
        public void Anschwaerzen(int id)
        {
            // Erster Schritt: Anzuschwärzenden merken.
            if (_anschwaerzID == 0)
            {
                BelTextAnzeigen(GetSpWithID(id).GetName() + " anschwärzen bei...");
                _anschwaerzID = id;
                return;
            }

            // Zweiter Schritt: Adressat Y wählen.
            if (id < SW.Statisch.GetMinKIID()) // Bei einem menschlichen Mitspieler kann man nicht anschwärzen.
            {
                BelTextAnzeigen("Ihr könnt nicht bei einem Mitspieler anschwärzen.");
                return;
            }

            string meldung = AnschwaerzenAusfuehren(GetAktiverSpieler(), _anschwaerzID, id);

            if (meldung != null)
                BelTextAnzeigen(meldung);

            _anschwaerzID = 0;
        }

        /// <summary>
        /// Kernlogik des Anschwärzens, UI-frei und mit dem Täter als Parameter (Issue: aggressive KI
        /// nutzt dieselbe Logik wie der Mensch). Glaubt der Adressat Y (Beziehung zum Täter ≥ Schwelle),
        /// verliert das Opfer X 30 Beziehungspunkte bei Y, Y verliert 10 bei sich selbst zum Täter.
        /// Glaubt Y nicht und X ist eine KI, berichtet Y es X (X −50 Beziehung zum Täter, Y −20). Ist X
        /// ein Mensch und Y glaubt nicht, bleibt es folgenlos (bestehende Asymmetrie, unverändert).
        /// Beweise senken die Schwelle (nicht den Schaden): siehe Konzept Abschnitt 5.
        /// </summary>
        /// <returns>Die anzuzeigende Meldung, oder null, wenn nichts weiter passiert.</returns>
        public string AnschwaerzenAusfuehren(int taeterId, int x, int y)
        {
            if (x == y)
                return "Ihr könnt nicht jemanden bei sich selbst anschwärzen.";

            if (GetGesetzX(22) != 0) // Wenn es verboten ist
                GetSpWithID(taeterId).ErhoeheGesetzXUmEins(22);

            if (taeterId < SW.Statisch.GetMinKIID())
                GetHumWithID(taeterId).GetSpielerStatistik().HiAnschwaerzungen++;

            int beweispunkte = taeterId < SW.Statisch.GetMinKIID()
                ? GetHumWithID(taeterId).GetAktiveSpionage(x).GetDelikte()
                : GetSpWithID(x).GetDeliktpunkte();

            int schwelle = Math.Max(50, 80 - Math.Min(beweispunkte * 3, 30));

            bool glaubtAnschuldigung = GetKIwithID(y).GetBeziehungZuKIX(taeterId) >= schwelle;

            if (glaubtAnschuldigung)
            {
                GetKIwithID(y).ErhoeheBeziehungZuX(x, -30);
                GetKIwithID(y).ErhoeheBeziehungZuX(taeterId, -10);
                return GetKIwithID(y).GetKompletterName() + " schenkt Euren Worten Glauben.";
            }

            if (x >= SW.Statisch.GetMinKIID()) // Y glaubt nicht; nur wenn X eine KI ist, berichtet Y ihm davon.
            {
                GetKIwithID(x).ErhoeheBeziehungZuX(taeterId, -50);
                GetKIwithID(y).ErhoeheBeziehungZuX(taeterId, -20);
                return GetSpWithID(y).GetKompletterName() + " glaubt Euch kein Wort und berichtet " +
                       GetSpWithID(x).GetKompletterName() + " von Euren Anschuldigungen.";
            }

            return null; // X ist Mensch und Y glaubt nicht: bleibt wie im Original folgenlos.
        }
        #endregion
```

Prüfen, ob `DynamischeSpieldaten.cs` bereits `using System;` importiert (für `Math.Max`/`Math.Min`) — falls nicht, ergänzen.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~AnschwaerzenTests"`
Expected: PASS (7/7).

- [ ] **Step 5: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs Conspiratio.Lib.Tests/AnschwaerzenTests.cs
git commit -m "Anschwaerzen entkoppelt und um Beweisgewichtung erweitert

AnschwaerzenAusfuehren(taeterId, x, y) ist die UI-freie Kernlogik, Anschwaerzen(id)
bleibt der duenne zweistufige UI-Wrapper darueber. Beweise (Spionage beim
menschlichen, Deliktpunkte beim KI-Taeter) senken jetzt die Glaubwuerdigkeits-
Schwelle von 80 auf minimal 50. Bei 0 Beweispunkten unveraendertes Verhalten."
```

---

## Task 5: `AggressionManager` — Sabotage-Initiierung mit Exklusivität

**Files:**
- Create: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Allgemein\AggressionManager.cs`
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\AggressionManagerTests.cs` (neu)

**Interfaces:**
- Consumes: `FechtDuellManager.BerechneKiFeindseligkeitChance` (Task 1), `HumSpieler.GetGegnerischeSabotage` (Task 2).
- Produces: `AggressionsAktion` (enum), `AggressionsErgebnis` (Klasse mit `TaeterId`, `Aktion`, `Meldung`), `AggressionManager.PruefeKiAggression(int ausgenommenId): List<AggressionsErgebnis>` — in diesem Task nur der Sabotage-Zweig; Anschwärzen folgt in Task 6.

- [ ] **Step 1: Write the failing tests**

Neue Datei `Conspiratio.Lib.Tests/AggressionManagerTests.cs`:

```csharp
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Aggressive KI (Issue): KIs mit schlechter Beziehung zum aktiven Menschen sabotieren oder
    /// schwärzen ihn an. Jede KI feuert unabhängig höchstens eine Aktion pro Zug.
    /// </summary>
    public class AggressionManagerTests
    {
        [Fact]
        public void Extrem_schlechte_Beziehung_loest_eine_Aktion_aus()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);
            SW.Dynamisch.GetKIwithID(kiId).SetBeziehungZuX(menschId, -1000);

            var ergebnisse = new AggressionManager().PruefeKiAggression(0);

            Assert.Single(ergebnisse);
            Assert.Equal(kiId, ergebnisse[0].TaeterId);
        }

        [Fact]
        public void Neutrale_Beziehung_loest_nichts_aus()
        {
            TestSpielwelt.Starte();
            TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);

            Assert.Empty(new AggressionManager().PruefeKiAggression(0));
        }

        [Fact]
        public void Die_ausgenommene_KI_wird_uebersprungen()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);
            SW.Dynamisch.GetKIwithID(kiId).SetBeziehungZuX(menschId, -1000);

            Assert.Empty(new AggressionManager().PruefeKiAggression(kiId));
        }

        [Fact]
        public void Mehrere_feindselige_KIs_koennen_unabhaengig_voneinander_feuern()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int ki1 = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);
            int ki2 = TestSpielwelt.SetzeKiGegner(1, 0, bosheit: 0);
            SW.Dynamisch.GetKIwithID(ki1).SetBeziehungZuX(menschId, -1000);
            SW.Dynamisch.GetKIwithID(ki2).SetBeziehungZuX(menschId, -1000);

            var ergebnisse = new AggressionManager().PruefeKiAggression(0);

            Assert.Equal(2, ergebnisse.Count);
        }

        [Fact]
        public void Laeuft_bereits_eine_Sabotage_derselben_KI_wird_keine_zweite_ausgeloest_aber_angeschwaerzt()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);
            TestSpielwelt.SetzeKiGegner(1, 0, bosheit: 0); // moeglicher Adressat
            SW.Dynamisch.GetKIwithID(kiId).SetBeziehungZuX(menschId, -1000);
            SW.Dynamisch.GetAktHum().GetGegnerischeSabotage(kiId).SetDauer(3);

            var ergebnisse = new AggressionManager().PruefeKiAggression(0);

            Assert.Single(ergebnisse);
            Assert.Equal(AggressionsAktion.Anschwaerzen, ergebnisse[0].Aktion);
        }

        [Fact]
        public void Sabotage_setzt_die_Dauer_ohne_laufende_Kosten()
        {
            TestSpielwelt.Starte(seed: 1);
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            var mensch = SW.Dynamisch.GetAktHum();
            int kiId = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);
            SW.Dynamisch.GetKIwithID(kiId).SetBeziehungZuX(menschId, -1000);

            bool sabotageBeobachtet = false;

            for (int versuch = 0; versuch < 200 && !sabotageBeobachtet; versuch++)
            {
                mensch.GegnerischeSabotageEntfernen(kiId);
                var ergebnisse = new AggressionManager().PruefeKiAggression(0);

                if (ergebnisse.Count == 1 && ergebnisse[0].Aktion == AggressionsAktion.Sabotage)
                {
                    sabotageBeobachtet = true;
                    Assert.Equal(AggressionManager.SabotageDauerJahre, mensch.GetGegnerischeSabotage(kiId).GetDauer());
                    Assert.Equal(0, mensch.GetGegnerischeSabotage(kiId).GetKosten());
                }
            }

            Assert.True(sabotageBeobachtet, "In 200 Versuchen kam kein Sabotage-Ergebnis vor.");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~AggressionManagerTests"`
Expected: Build-Fehler (Klasse `AggressionManager` existiert nicht). Die letzten beiden Tests (Anschwärzen-Bevorzugung, Anschwärzen-Zweig überhaupt) bleiben bis Task 6 rot — in diesem Task genügt es, dass die ersten vier Tests grün werden; Task 6 macht die verbleibenden grün.

- [ ] **Step 3: Implement `AggressionManager` (Sabotage-Zweig)**

Neue Datei `Conspiratio.Lib/Allgemein/AggressionManager.cs`:

```csharp
using System.Collections.Generic;

using Conspiratio.Lib.Gameplay.Spielwelt;

using JetBrains.Annotations;

namespace Conspiratio.Lib.Allgemein
{
    public enum AggressionsAktion { Sabotage, Anschwaerzen }

    /// <summary>Ergebnis einer von einer KI ausgelösten feindlichen Aktion gegen den aktiven Menschen.</summary>
    public class AggressionsErgebnis
    {
        public int TaeterId { get; init; }
        public AggressionsAktion Aktion { get; init; }

        /// <summary>Nur bei <see cref="AggressionsAktion.Anschwaerzen"/> befüllt.</summary>
        public string Meldung { get; init; }
    }

    /// <summary>
    /// Aggressive KI (Issue): KI-Spieler mit schlechter Beziehung zum aktiven Menschen setzen
    /// Saboteure gegen dessen Besitz ein oder schwärzen ihn bei anderen Würdenträgern an – verstärkt,
    /// wenn sie Beweise gegen ihn haben. Nutzt dieselbe Feindseligkeits-Formel wie die KI-Beleidigung
    /// (<see cref="FechtDuellManager.BerechneKiFeindseligkeitChance"/>), aber über alle KIs statt nur
    /// Amtsträger, da beide Aktionen kein Amt voraussetzen.
    /// </summary>
    public class AggressionManager
    {
        public const int SabotageDauerJahre = 5;

        /// <summary>
        /// Prüft für jede KI außer <paramref name="ausgenommenId"/> unabhängig, ob sie in diesem Zug
        /// den aktiven Menschen angreift. Jede KI führt höchstens eine Aktion aus.
        /// </summary>
        /// <param name="ausgenommenId">
        /// KI, die diese Runde bereits über <see cref="FechtDuellManager.PruefeKiBeleidigtSpieler"/>
        /// beleidigt hat (oder 0) – bleibt außen vor, damit keine KI zwei feindliche Aktionen im
        /// selben Zug gegen denselben Menschen ausführt.
        /// </param>
        [PublicAPI]
        public List<AggressionsErgebnis> PruefeKiAggression(int ausgenommenId)
        {
            var ergebnisse = new List<AggressionsErgebnis>();
            var mensch = SW.Dynamisch.GetAktHum();
            int menschId = SW.Dynamisch.GetAktiverSpieler();

            for (int i = SW.Statisch.GetMinKIID(); i < SW.Statisch.GetMaxKIID(); i++)
            {
                if (i == ausgenommenId)
                    continue;

                var ki = SW.Dynamisch.GetKIwithID(i);
                int chance = FechtDuellManager.BerechneKiFeindseligkeitChance(ki.GetBeziehungZuKIX(menschId), ki.GetBosheit());

                if (SW.Statisch.Rnd.Next(0, 100) >= chance)
                    continue;

                bool laeuftSchonSabotage = mensch.GetGegnerischeSabotage(i).GetDauer() > 0;

                if (laeuftSchonSabotage)
                {
                    // Anschwärzen-Zweig folgt in Task 6.
                    continue;
                }

                mensch.GetGegnerischeSabotage(i).SetDauer(SabotageDauerJahre);
                ergebnisse.Add(new AggressionsErgebnis { TaeterId = i, Aktion = AggressionsAktion.Sabotage });
            }

            return ergebnisse;
        }
    }
}
```

- [ ] **Step 4: Run the first four tests to verify they pass**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~Extrem_schlechte_Beziehung|FullyQualifiedName~Neutrale_Beziehung|FullyQualifiedName~ausgenommene_KI|FullyQualifiedName~Mehrere_feindselige|FullyQualifiedName~Sabotage_setzt_die_Dauer"`
Expected: PASS (5/5). `Laeuft_bereits_eine_Sabotage...` bleibt erwartungsgemäß rot (`Assert.Single` schlägt fehl, da der Zweig aktuell `continue` statt Anschwärzen macht) — das wird in Task 6 behoben.

- [ ] **Step 5: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Allgemein/AggressionManager.cs Conspiratio.Lib.Tests/AggressionManagerTests.cs
git commit -m "AggressionManager: Sabotage-Initiierung mit Exklusivitaet

Jede KI (nicht nur Amtstraeger) prueft unabhaengig ihre Feindseligkeit zum
aktiven Menschen; bei Erfolg startet sie eine Sabotage. Anschwaerzen-Zweig
folgt im naechsten Commit - Laeuft_bereits_eine_Sabotage-Test bleibt bis
dahin bewusst rot."
```

---

## Task 6: `AggressionManager` — Anschwärzen-Zweig ergänzen

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Allgemein\AggressionManager.cs`
- Test: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.Tests\AggressionManagerTests.cs`

**Interfaces:**
- Consumes: `DynamischeSpieldaten.AnschwaerzenAusfuehren` (Task 4).

- [ ] **Step 1: Write the failing test**

In `AggressionManagerTests.cs` ergänzen:

```csharp
        [Fact]
        public void Anschwaerzen_waehlt_die_KI_mit_der_besten_Beziehung_zum_Anklaeger_als_Adressat()
        {
            TestSpielwelt.Starte();
            int menschId = SW.Dynamisch.GetAktiverSpieler();
            int anklaeger = TestSpielwelt.SetzeKiGegner(0, 0, bosheit: 0);
            int schlechterAdressat = TestSpielwelt.SetzeKiGegner(1, 0, bosheit: 0);
            int besterAdressat = TestSpielwelt.SetzeKiGegner(2, 0, bosheit: 0);

            SW.Dynamisch.GetKIwithID(anklaeger).SetBeziehungZuX(menschId, -1000);
            SW.Dynamisch.GetAktHum().GetGegnerischeSabotage(anklaeger).SetDauer(3); // erzwingt Anschwaerzen
            SW.Dynamisch.GetKIwithID(schlechterAdressat).SetBeziehungZuX(anklaeger, 10);
            SW.Dynamisch.GetKIwithID(besterAdressat).SetBeziehungZuX(anklaeger, 90);

            var ergebnisse = new AggressionManager().PruefeKiAggression(0);

            Assert.Single(ergebnisse);
            Assert.Contains(SW.Dynamisch.GetSpWithID(besterAdressat).GetKompletterName(), ergebnisse[0].Meldung);
        }
```

- [ ] **Step 2: Run tests to verify the new one and `Laeuft_bereits_eine_Sabotage...` fail**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~AggressionManagerTests"`
Expected: `Anschwaerzen_waehlt_die_KI...` und `Laeuft_bereits_eine_Sabotage...` FAIL, Rest PASS.

- [ ] **Step 3: Complete the dispatcher**

In `AggressionManager.cs` den `if (laeuftSchonSabotage) { continue; }`-Block ersetzen durch:

```csharp
                bool waehleAnschwaerzen = laeuftSchonSabotage || SW.Statisch.Rnd.Next(0, 2) == 0;

                if (waehleAnschwaerzen)
                {
                    int adressat = WaehleAnschwaerzenAdressat(i, menschId);

                    if (adressat == 0)
                        continue; // keine weitere KI vorhanden (Minimalspiel)

                    string meldung = SW.Dynamisch.AnschwaerzenAusfuehren(i, menschId, adressat);

                    if (meldung != null)
                        ergebnisse.Add(new AggressionsErgebnis { TaeterId = i, Aktion = AggressionsAktion.Anschwaerzen, Meldung = meldung });

                    continue;
                }

                mensch.GetGegnerischeSabotage(i).SetDauer(SabotageDauerJahre);
                ergebnisse.Add(new AggressionsErgebnis { TaeterId = i, Aktion = AggressionsAktion.Sabotage });
```

(Die beiden nun redundanten Zeilen `mensch.GetGegnerischeSabotage(i).SetDauer(...)` / `ergebnisse.Add(...)`, die vorher direkt nach dem `if (laeuftSchonSabotage)`-Block standen, entfernen — sie sind jetzt Teil des obigen Ersatzblocks.)

Direkt darunter, als private Methode der Klasse, ergänzen:

```csharp
        /// <summary>Die KI mit der besten Beziehung zum Ankläger, außer diesem und dem Opfer selbst.</summary>
        private static int WaehleAnschwaerzenAdressat(int anklaegerId, int opferId)
        {
            int besterAdressat = 0;
            int besteBeziehung = int.MinValue;

            for (int i = SW.Statisch.GetMinKIID(); i < SW.Statisch.GetMaxKIID(); i++)
            {
                if (i == anklaegerId || i == opferId)
                    continue;

                int beziehungZumAnklaeger = SW.Dynamisch.GetKIwithID(i).GetBeziehungZuKIX(anklaegerId);

                if (beziehungZumAnklaeger > besteBeziehung)
                {
                    besteBeziehung = beziehungZumAnklaeger;
                    besterAdressat = i;
                }
            }

            return besterAdressat;
        }
```

- [ ] **Step 4: Run all AggressionManager tests to verify they pass**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln" --filter "FullyQualifiedName~AggressionManagerTests"`
Expected: PASS (7/7).

- [ ] **Step 5: Run the full Lib test suite**

Run: `dotnet test "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln"`
Expected: PASS, keine Regression in anderen Testklassen.

- [ ] **Step 6: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add Conspiratio.Lib/Allgemein/AggressionManager.cs Conspiratio.Lib.Tests/AggressionManagerTests.cs
git commit -m "AggressionManager: Anschwaerzen-Zweig ergaenzt

Bei Erfolg waehlt die KI 50/50 zwischen Sabotage und Anschwaerzen -
ausser sie hat bereits eine laufende Sabotage gegen den Menschen, dann
immer Anschwaerzen. Adressat ist die KI mit der besten Beziehung zum
Anklaeger. Feature Lib-seitig vollstaendig."
```

---

## Task 7: Lib-CHANGELOG und Versionsbump

**Files:**
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\CHANGELOG.md`
- Modify: `D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib\Conspiratio.Lib.csproj:42`

**Interfaces:** keine (Dokumentation/Metadaten).

- [ ] **Step 1: CHANGELOG-Einträge ergänzen**

In `CHANGELOG.md` unter der bestehenden Überschrift `## [Unreleased]`, an die bestehenden `**[DE]**`/`**[EN]**`-Absätze jeweils einen weiteren Bullet-Punkt anhängen (nicht als neuer Abschnitt — ein einziger `[Unreleased]`-Block, siehe CLAUDE.md):

Unter `**[DE]**`:
```
- Aggressive KI (Sabotage + Anschwärzen): KI-Spieler mit schlechter Beziehung zum aktiven Menschen setzen jetzt selbst Saboteure gegen dessen Besitz ein oder schwärzen ihn bei anderen Würdenträgern an — verstärkt, wenn sie Beweise gegen ihn haben. Beide Mechaniken spiegeln bestehende (bisher nur Mensch→KI nutzbare) Mechanismen: Sabotage nutzt dieselbe Chance-/Schadensformel wie bisher (`ZugNachrichtenManager.ErmittleGegnerischeSabotageNachrichten`, neues Feld `HumSpieler._gegnerischeSabotagen`), Anschwärzen wurde dafür von seinem UI-Zustand entkoppelt (`DynamischeSpieldaten.AnschwaerzenAusfuehren`) und um eine Beweisgewichtung erweitert: Beweise senken die Glaubwürdigkeits-Schwelle des Adressaten von 80 auf minimal 50 (Quelle beim menschlichen Täter die bestehende Spionage, bei der KI ihre Ground-Truth-Deliktpunkte des Opfers) — davon profitiert auch der bestehende Mensch→KI-Pfad. Neuer `AggressionManager` prüft dafür am selben Zugbeginn-Zeitpunkt wie die bestehende KI-Beleidigung (`PruefeKiBeleidigtSpieler`, dessen Feindseligkeits-Formel jetzt als `BerechneKiFeindseligkeitChance` wiederverwendbar ist) jede KI unabhängig; jede KI führt höchstens eine der drei feindlichen Aktionen pro Zug gegen denselben Menschen aus.
```

Unter `**[EN]**`:
```
- Aggressive AI (sabotage + slander): AI players with a bad relationship to the active human now unleash saboteurs against their possessions or slander them to other officeholders — strengthened when they hold evidence against them. Both mechanics mirror existing (previously human→AI-only) mechanisms: sabotage reuses the same chance/damage formula as before (`ZugNachrichtenManager.ErmittleGegnerischeSabotageNachrichten`, new `HumSpieler._gegnerischeSabotagen` field), slander was decoupled from its UI state for this (`DynamischeSpieldaten.AnschwaerzenAusfuehren`) and gained evidence weighting: evidence lowers the target's credibility threshold from 80 down to a minimum of 50 (sourced from existing espionage for a human accuser, from the victim's ground-truth guilt score for an AI accuser) — the existing human→AI path benefits from this too. A new `AggressionManager` checks every AI independently at the same turn-start point as the existing AI insult (`PruefeKiBeleidigtSpieler`, whose hostility formula is now reusable as `BerechneKiFeindseligkeitChance`); each AI performs at most one of the three hostile actions per turn against the same human.
```

- [ ] **Step 2: Version bumpen**

In `Conspiratio.Lib.csproj:42`:
```xml
    <Version>3.99.0</Version>
```
(vorher `3.98.0`).

- [ ] **Step 3: Build zur Kontrolle**

Run: `dotnet build "D:\Projekte\C# Projekte\Conspiratio.Lib\Conspiratio.Lib.sln"`
Expected: Build erfolgreich, `.nupkg` mit Version 3.99.0 in `Conspiratio.Lib\bin\Debug`.

- [ ] **Step 4: Commit**

```bash
cd "D:/Projekte/C# Projekte/Conspiratio.Lib"
git add CHANGELOG.md Conspiratio.Lib/Conspiratio.Lib.csproj
git commit -m "Version 3.99.0: Aggressive KI (Sabotage + Anschwaerzen)"
```

---

## Task 8: Lokale Lib-Version für die Godot-Entwicklung einbinden

Bevor die Godot-Seite gegen die neuen Typen (`AggressionManager`, `AnschwaerzenAusfuehren`, `ErmittleGegnerischeSabotageNachrichten`) kompiliert werden kann, muss Version 3.99.0 lokal verfügbar sein — sie ist noch nicht auf nuget.org veröffentlicht (das passiert erst mit einem echten GitHub-Release, außerhalb dieses Plans). Nutzt den in CLAUDE.md dokumentierten Cache-Trick.

**Files:** keine Code-Änderungen in diesem Task, nur lokale Umgebung.

- [ ] **Step 1: Alte gecachte Version (falls vorhanden) entfernen**

```bash
rm -rf ~/.nuget/packages/conspiratio.lib/3.99.0
```

- [ ] **Step 2: Globalen Cache aus dem lokalen Lib-Build befüllen**

Ein beliebiges Projekt, das aus dem lokalen Feed restored, füllt den globalen Cache. Am einfachsten das Test-Projekt selbst:

```bash
dotnet restore "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib.Tests/Conspiratio.Lib.Tests.csproj" --source "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib/bin/Debug"
```

Expected: Restore erfolgreich, `~/.nuget/packages/conspiratio.lib/3.99.0` existiert danach.

- [ ] **Step 3: Verifizieren**

```bash
ls ~/.nuget/packages/conspiratio.lib/3.99.0
```

Expected: Verzeichnis mit dem `.nupkg`-Inhalt existiert.

Kein Commit in diesem Task (reine Umgebungsvorbereitung).

---

## Task 9: Godot — Sabotage-/Anschwärzen-Initiierung in `Kontor.cs`

**Files:**
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj:15`
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\assets\scripts\Kontor.cs:267-296` (Bereich des bestehenden KI-Beleidigungs-Blocks)

**Interfaces:**
- Consumes: `AggressionManager.PruefeKiAggression(int): List<AggressionsErgebnis>`, `AggressionsErgebnis.{TaeterId,Aktion,Meldung}`, `AggressionsAktion` (aus Conspiratio.Lib.Allgemein, Task 5/6).

- [ ] **Step 1: PackageReference auf 3.99.0 anheben**

In `Conspiratio.Godot.csproj:15`:
```xml
    <PackageReference Include="Conspiratio.Lib" Version="3.99.0" />
```

- [ ] **Step 2: Restore und Build zur Kontrolle**

Run: `dotnet restore "C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj"`
Run: `dotnet build "C:\Projekte\Godot\Conspiratio.Godot\Conspiratio.Godot.csproj"`
Expected: Beide erfolgreich (Restore findet 3.99.0 im lokalen NuGet-Cache aus Task 8).

- [ ] **Step 3: Aufruf in `NaechstenSpielerAnkuendigen` ergänzen**

In `Kontor.cs`, direkt nach dem bestehenden KI-Beleidigungs-Block (endet aktuell um Zeile ~294–296 mit `UpdateHud();` nach dem `if (beleidiger != 0)`-Block, vor dem nächsten Abschnitt), folgenden Code einfügen:

```csharp
			// Aggressive KI (Sabotage + Anschwärzen): jede KI außer der oben ggf. beleidigenden prüft
			// unabhängig ihre Feindseligkeit; Sabotage bleibt covert (Meldung erst bei tatsächlichem
			// Schaden, siehe ZeigeVerdeckteEreignisse), Anschwärzen hat ein sofortiges Ergebnis.
			var aggression = new AggressionManager();

			foreach (var ergebnis in aggression.PruefeKiAggression(beleidiger))
			{
				if (ergebnis.Aktion == AggressionsAktion.Anschwaerzen)
				{
					UpdateHud();
					await _main.RundenNachrichtenDialog.ShowDialog("Intrigen\n\n" + ergebnis.Meldung);
				}
			}
```

`beleidiger` ist die bereits im bestehenden Block deklarierte Variable (0, wenn keine KI beleidigt hat) — kein zusätzlicher Import nötig, `Conspiratio.Lib.Allgemein` ist in `Kontor.cs` bereits eingebunden (`FechtDuellManager`, `WortgefechtManager` stammen von dort).

- [ ] **Step 4: Headless-Smoke-Test**

```powershell
$godot = "C:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
& $godot --headless --path "C:\Projekte\Godot\Conspiratio.Godot" --import
Start-Process $godot -ArgumentList '--headless','--path','C:\Projekte\Godot\Conspiratio.Godot','--quit','res://scenes/Main.tscn' -Wait -PassThru -RedirectStandardOutput out.log -RedirectStandardError err.log
Get-Content err.log
```

Expected: sauberer Output, keine Stack Traces in `err.log` — bestätigt, dass die Szene weiterhin lädt und `_Ready()` fehlerfrei durchläuft.

- [ ] **Step 5: Commit**

```bash
cd "C:/Projekte/Godot/Conspiratio.Godot"
git add Conspiratio.Godot.csproj assets/scripts/Kontor.cs
git commit -m "Aggressive KI: Sabotage-/Anschwaerzen-Initiierung am Zugbeginn (Conspiratio.Lib 3.99.0)

Gleicher Hook wie die bestehende KI-Beleidigung. Sabotage bleibt covert,
Anschwaerzen-Ergebnisse werden sofort als Intrigen-Meldung angezeigt."
```

---

## Task 10: Godot — laufende Sabotage-Wirkung in `ZeigeVerdeckteEreignisse`

**Files:**
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\assets\scripts\Kontor.cs:767-814` (Methode `ZeigeVerdeckteEreignisse`)

**Interfaces:**
- Consumes: `ZugNachrichtenManager.ErmittleGegnerischeSabotageNachrichten(): string` (Task 3).

- [ ] **Step 1: Neuen Meldungsblock ergänzen**

In `Kontor.cs`, Methode `ZeigeVerdeckteEreignisse`, direkt nach dem bestehenden Sabotage-Block:

```csharp
		string sabotage = zugNachrichten.ErmittleSabotageNachrichten();

		if (sabotage != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Sabotage\n\n" + sabotage);
```

folgenden neuen Block einfügen:

```csharp
		string gegnerischeSabotage = zugNachrichten.ErmittleGegnerischeSabotageNachrichten();

		if (gegnerischeSabotage != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Sabotage gegen Euch\n\n" + gegnerischeSabotage);
```

- [ ] **Step 2: Headless-Smoke-Test**

```powershell
$godot = "C:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
Start-Process $godot -ArgumentList '--headless','--path','C:\Projekte\Godot\Conspiratio.Godot','--quit','res://scenes/Main.tscn' -Wait -PassThru -RedirectStandardOutput out.log -RedirectStandardError err.log
Get-Content err.log
```

Expected: sauberer Output.

- [ ] **Step 3: E2E-Testlauf (bestätigt, dass ein 10-Jahres-Spiel mit dem geänderten `Kontor.cs` weiterhin durchläuft)**

```bash
godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=10 --spieler=2 --verbose
```

Expected: Exit-Code 0, kein Hänger. Da die neuen Blöcke nur `await`-Dialoge mit dem etablierten `RundenNachrichtenDialog`-Muster verwenden (Gruppe `Dialogs`, siehe CLAUDE.md), sollte der Treiber sie ohne Anpassung mitnehmen — sollte der Lauf dennoch hängen, zuerst den Statusbericht des Treibers (offene Dialoge, sichtbare Screens) lesen, bevor spekuliert wird.

- [ ] **Step 4: Commit**

```bash
cd "C:/Projekte/Godot/Conspiratio.Godot"
git add assets/scripts/Kontor.cs
git commit -m "Aggressive KI: Meldung der laufenden Sabotage gegen den Menschen (Conspiratio.Lib 3.99.0)

Neben der bestehenden Sabotage-Meldung in ZeigeVerdeckteEreignisse."
```

---

## Task 11: Godot-CHANGELOG

**Files:**
- Modify: `C:\Projekte\Godot\Conspiratio.Godot\CHANGELOG.md`

**Interfaces:** keine (Dokumentation).

- [ ] **Step 1: DE/EN-Einträge ergänzen**

Unter `## 1.0.0-godot` → `_Unreleased_` → `### [DE]` → `#### Geändert` (bzw. den passenden Unterabschnitt, an bestehende Einträge oben anhängen wie im Muster der übrigen Zeilen mit „Benötigt Conspiratio.Lib X.Y.Z"):

```
- Aggressive KI (Sabotage + Anschwärzen): KI-Spieler mit schlechter Beziehung setzen jetzt selbst Saboteure gegen den Besitz des Menschen ein oder schwärzen ihn bei anderen Würdenträgern an — verstärkt bei vorliegenden Beweisen. Sabotage bleibt verdeckt, bis sie tatsächlich Schaden anrichtet (eigener Meldungsblock „Sabotage gegen Euch" neben der bestehenden Sabotage-Meldung); Anschwärzen-Ergebnisse erscheinen sofort als „Intrigen"-Meldung am Zugbeginn, am selben Punkt wie die bestehende KI-Beleidigung. Benötigt Conspiratio.Lib 3.99.0
```

Im entsprechenden `### [EN]`-Abschnitt:

```
- Aggressive AI (sabotage + slander): AI players with a bad relationship now unleash saboteurs against the human's possessions or slander them to other officeholders — strengthened by evidence. Sabotage stays covert until it actually deals damage (new "Sabotage against you" message block next to the existing sabotage message); slander results appear immediately as an "Intrigues" message at turn start, at the same point as the existing AI insult. Requires Conspiratio.Lib 3.99.0
```

- [ ] **Step 2: Commit**

```bash
cd "C:/Projekte/Godot/Conspiratio.Godot"
git add CHANGELOG.md
git commit -m "CHANGELOG: Aggressive KI (Sabotage + Anschwaerzen)"
```

---

## Nach Abschluss

Die tatsächliche Veröffentlichung von Conspiratio.Lib 3.99.0 auf nuget.org (GitHub-Release) ist ein manueller Schritt außerhalb dieses Plans — bis dahin schlägt die CI-Pipeline dieses Repos (`build.yml`) wie dokumentiert fehl, weil sie die referenzierte Version nicht auf nuget.org findet. Das ist erwartetes Verhalten (siehe CLAUDE.md) und kein Grund, an diesem Plan etwas zu ändern.
