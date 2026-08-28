# Moneysink: Unterhalt, Hofhaltung und Wegfall des Geldansehens — Umsetzungsplan

> **Für agentische Bearbeiter:** ERFORDERLICHE SUB-SKILL: `superpowers:subagent-driven-development`
> (empfohlen) oder `superpowers:executing-plans`, um diesen Plan Aufgabe für Aufgabe umzusetzen.
> Die Schritte nutzen Checkbox-Syntax (`- [ ]`) zur Fortschrittsverfolgung.

**Ziel:** Die Spätspielbremse hängt nicht mehr am Kontostand, sondern an Besitz, Auslastung und
Repräsentation — und gutes Wirtschaften verdient Geltung, statt sie nur zu erhalten.

**Architektur:** Drei ineinandergreifende Bausteine in der Lib, danach eine dünne Godot-Ansicht. Neuer
Abrechnungsposten `Unterhalt` (progressiv je besessener Werkstätte), die Hofhaltung wird eine vom Spieler
gewählte Stufe mit `PermaAnsehen` als Gegenleistung, und der Geldanteil des Ansehens entfällt ersatzlos.
Genau ein neues serialisiertes Feld, kodiert als Abweichung, damit die `0` aus alten Spielständen das
bisherige Verhalten bedeutet.

**Tech-Stack:** Conspiratio.Lib (netstandard2.0, xUnit-Tests in `Conspiratio.Lib.Tests`, `dotnet test`),
Conspiratio.Godot (Godot 4.7 / .NET 8, Verifikation über den E2E-Treiber und die Wegwerf-Harness).

**Spec:** [`docs/superpowers/specs/2026-08-28-moneysink-unterhalt-hofhaltung-design.md`](../specs/2026-08-28-moneysink-unterhalt-hofhaltung-design.md)

## Globale Randbedingungen

- **Einsteiger sollen nichts merken.** Zwei Betriebe dürfen einen Anfänger nicht spürbar belasten. Diese
  Schranke ist als Test verankert (Aufgabe 1) und begrenzt die Kalibrierung in Aufgabe 5.
- **Spielstände bleiben lesbar.** Serialisierung ist feldbasiert und umgeht Konstruktoren; ein neues
  `int` kommt aus einem alten Stand als `0` an. Deshalb wird die *Abweichung* gespeichert, nicht die Stufe.
- **Titel sinken nie.** `VersuchTitelVerleihen` vergibt nur nach oben; daran wird nicht gerührt.
- **Lib zuerst, Godot danach.** Der Godot-Commit nennt die Lib-Version im Betreff
  (Muster: `… (Conspiratio.Lib 4.5.0)`).
- **Lib-CHANGELOG:** Änderungen sammeln sich unter einem einzigen `## [Unreleased]`, bilingual (DE und
  EN), ohne Versions- oder Datumsüberschrift je Änderung.
- **Commits nur auf Zuruf.** Die Commit-Schritte unten sind vorbereitet, aber der Nutzer entscheidet,
  wann committet wird.

## Dateien

**Conspiratio.Lib**

| Datei | Verantwortung |
|---|---|
| `Conspiratio.Lib/Allgemein/AbrechnungsErgebnis.cs` | neues Feld `Unterhalt` |
| `Conspiratio.Lib/Allgemein/AbrechnungsManager.cs` | Unterhalt berechnen, Hofhaltung nach Stufe, Auslastungsbelohnung |
| `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` | `ZaehleWerkstaetten`, Hofhaltungsabweichung, Wegfall des Geldansehens |
| `Conspiratio.Lib.Tests/UnterhaltTests.cs` | neu — Staffel, Produktionsunabhängigkeit, Einsteigerschranke |
| `Conspiratio.Lib.Tests/HofhaltungTests.cs` | erweitern — Stufen, Ansehenswirkung, Savegame-Default |
| `Conspiratio.Lib.Tests/AnsehenTests.cs` | neu — Geldansehen wirkt nicht mehr, Auslastungsbelohnung |

**Conspiratio.Godot**

| Datei | Verantwortung |
|---|---|
| `Conspiratio.Godot.csproj` | Lib-Version anheben |
| `assets/scripts/AbrechnungDialog.cs` | Posten `Unterhalt` anzeigen |
| `assets/scripts/HofhaltungDialog.cs` + `scenes/dialogs/HofhaltungDialog.tscn` | neu — Stufenwahl |
| `assets/scripts/Schreibstube.cs` + `scenes/Schreibstube.tscn` | Klickflaeche AreaHofhaltung als Einstieg |
| `CHANGELOG.md` | bilingualer Eintrag |

---

### Aufgabe 1: Unterhalt als Abrechnungsposten

**Dateien:**
- Ändern: `Conspiratio.Lib/Allgemein/AbrechnungsErgebnis.cs`
- Ändern: `Conspiratio.Lib/Allgemein/AbrechnungsManager.cs` (Region „Hofhaltung", davor einfügen)
- Ändern: `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` (neue Methode `ZaehleWerkstaetten`)
- Test: `Conspiratio.Lib.Tests/UnterhaltTests.cs` (neu)

**Schnittstellen:**
- Liefert: `HumSpieler.ZaehleWerkstaetten() → int`, `AbrechnungsErgebnis.Unterhalt → int`,
  `AbrechnungsManager.GrundunterhaltProWerkstatt` (public const int)

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

`Conspiratio.Lib.Tests/UnterhaltTests.cs`:

```csharp
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Der Unterhalt ist die erste laufende Ausgabe, die am Besitz haengt statt an der Produktion.
    /// Bisher kostete eine stillgelegte Werkstatt nichts, weil Betriebskosten nur Staetten in
    /// Produktion zaehlen - Horten war gratis, Wirtschaften teuer.
    /// </summary>
    public class UnterhaltTests
    {
        /// <summary>Schaltet die ersten <paramref name="anzahl"/> Werkstattplaetze frei.</summary>
        private static void SetzeWerkstaetten(int anzahl)
        {
            var spieler = SW.Dynamisch.GetAktHum();
            int gesetzt = 0;

            for (int stadtId = 1; stadtId < SW.Statisch.GetMaxStadtID() && gesetzt < anzahl; stadtId++)
                for (int nr = 1; nr <= SW.Statisch.GetMaxWerkstaettenProStadt() && gesetzt < anzahl; nr++)
                {
                    spieler.GetSpielerHatInStadtXWerkstaettenY(nr, stadtId).SetEnabled(true);
                    gesetzt++;
                }
        }

        [Fact]
        public void Der_Unterhalt_steigt_progressiv_mit_der_Betriebszahl()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(20);

            // Nicht gegen die Literalzahl 20 pruefen: PlayerSetupManager schaltet beim Anlegen bereits
            // eine Werkstatt frei, und die liegt in der Heimatstadt - je nach deren ID ausserhalb der
            // ersten 20 durchlaufenen Plaetze. Der Test geht deshalb vom tatsaechlichen Bestand aus.
            int besessen = SW.Dynamisch.GetAktHum().ZaehleWerkstaetten();

            Assert.True(besessen >= 20, "Der Aufbau muss mindestens 20 Betriebe ergeben haben.");

            var ergebnis = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            // Der n-te Betrieb kostet Grundunterhalt * n, die Summe also N*(N+1)/2 Grundeinheiten.
            Assert.Equal(AbrechnungsManager.GrundunterhaltProWerkstatt * besessen * (besessen + 1) / 2,
                         ergebnis.Unterhalt);
        }

        [Fact]
        public void Der_Unterhalt_steckt_in_den_Gesamtkosten()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(5);

            var ergebnis = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.True(ergebnis.Unterhalt > 0, "Fuenf Betriebe muessen Unterhalt kosten.");
            Assert.True(ergebnis.Gesamtkosten >= ergebnis.Unterhalt, "Der Unterhalt muss in den Gesamtkosten stecken.");
        }
    }
}
```

- [ ] **Schritt 2: Test laufen lassen und Fehlschlag bestätigen**

Ausführen: `dotnet test --filter UnterhaltTests`
Erwartet: Compilerfehler — `GrundunterhaltProWerkstatt` und `Unterhalt` existieren nicht.

- [ ] **Schritt 3: Werkstätten zählen**

In `HumSpieler.cs`, direkt hinter `GetSpielerHatInStadtXWerkstaettenY` einfügen:

```csharp
        /// <summary>
        /// Zaehlt die besessenen Werkstaetten ueber alle Staedte. Grundlage des Unterhalts, der bewusst
        /// am Besitz haengt und nicht an der Produktion.
        /// </summary>
        public int ZaehleWerkstaetten()
        {
            int anzahl = 0;

            for (int stadtId = 1; stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
                for (int nr = 1; nr <= SW.Statisch.GetMaxWerkstaettenProStadt(); nr++)
                    if (GetSpielerHatInStadtXWerkstaettenY(nr, stadtId).GetEnabled())
                        anzahl++;

            return anzahl;
        }
```

- [ ] **Schritt 4: Feld im Ergebnis ergänzen**

In `AbrechnungsErgebnis.cs`, direkt vor `Gesamtkosten`:

```csharp
        /// <summary>
        /// Jaehrlicher Unterhalt der besessenen Werkstaetten, progressiv in ihrer Zahl. Haengt bewusst
        /// nicht an der Produktion: Sonst waere Horten gratis und Wirtschaften teuer.
        /// </summary>
        public int Unterhalt { get; internal set; }
```

- [ ] **Schritt 5: Unterhalt berechnen**

In `AbrechnungsManager.cs`, unmittelbar **vor** der Region `#region Hofhaltung`:

```csharp
            #region Unterhalt

            // Progressiv statt linear: Der n-te Betrieb kostet das n-Fache des ersten, die Summe waechst
            // also quadratisch. Bewusst linear-progressiv und nicht geometrisch wie die Kaufpreisstaffel
            // (HandelsManager.SteigerungProzent, 125 % je Betrieb) - ueber zwanzig Stufen waere das
            // Faktor 86 und wuerde Expansion nicht bremsen, sondern verbieten.
            int werkstaetten = spieler.ZaehleWerkstaetten();

            ergebnis.Unterhalt = GrundunterhaltProWerkstatt * werkstaetten * (werkstaetten + 1) / 2;
            ergebnis.Gesamtkosten += ergebnis.Unterhalt;

            #endregion

```

Und als Konstante oben in der Klasse, direkt unter `public class AbrechnungsManager`:

```csharp
        /// <summary>
        /// Unterhalt des ersten Betriebs; der n-te kostet das n-Fache. Kalibriert gegen zwei Schranken:
        /// Zwei Betriebe duerfen einen Einsteiger nicht spuerbar belasten, und der Unterhalt muss klar
        /// unter dem Deckungsbeitrag einer ausgelasteten Werkstatt liegen, sonst lohnt Expansion nie.
        /// </summary>
        public const int GrundunterhaltProWerkstatt = 40;
```

- [ ] **Schritt 6: Tests laufen lassen**

Ausführen: `dotnet test --filter UnterhaltTests`
Erwartet: PASS (beide Tests).

- [ ] **Schritt 7: Einsteigerschranke als Test nachziehen**

An `UnterhaltTests.cs` anhängen:

```csharp
        /// <summary>
        /// Die Randbedingung des Vorhabens als Test: Ein Einsteiger mit zwei Betrieben darf den
        /// Unterhalt nicht spueren. Diese Schranke begrenzt die Kalibrierung von
        /// GrundunterhaltProWerkstatt nach unten.
        /// </summary>
        [Fact]
        public void Ein_Einsteiger_mit_zwei_Betrieben_zahlt_kaum_Unterhalt()
        {
            // Kein SetzeWerkstaetten: Ein frisch angelegter Spieler besitzt genau die eine Werkstatt,
            // die PlayerSetupManager ihm gibt. Genau das ist der Einsteigerfall.
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.GetSpielerHatInStadtXWerkstaettenY(1, 1).SetEnabled(true);

            int besessen = spieler.ZaehleWerkstaetten();

            Assert.Equal(2, besessen);

            var ergebnis = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            // Drei Grundeinheiten (1 + 2). Gemessen am Startkapital eines Spielers ist das Rauschen.
            Assert.Equal(AbrechnungsManager.GrundunterhaltProWerkstatt * 3, ergebnis.Unterhalt);
            Assert.True(ergebnis.Unterhalt <= 500, "Zwei Betriebe duerfen einen Einsteiger nicht spuerbar belasten.");
        }

        /// <summary>
        /// Die eigentliche Umkehrung: Betriebskosten zaehlen nur Staetten in Produktion, der Unterhalt
        /// zaehlt Besitz. Ein stillgelegter Betrieb kostet deshalb genauso viel wie ein laufender.
        /// </summary>
        [Fact]
        public void Der_Unterhalt_haengt_nicht_an_der_Produktion()
        {
            TestSpielwelt.Starte();
            SetzeWerkstaetten(4);

            int ohneProduktion = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler().Unterhalt;

            TestSpielwelt.Starte();
            SetzeWerkstaetten(4);
            SW.Dynamisch.GetAktHum().GetProduktionsslot(1, 0)
              .SetTaetigkeit((int)EnumProduktionsslotAktionsart.Produzieren);

            int mitProduktion = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler().Unterhalt;

            Assert.Equal(ohneProduktion, mitProduktion);
        }
```

Dafür oben ergänzen: `using Conspiratio.Lib.Gameplay.Niederlassung;`

- [ ] **Schritt 8: Tests laufen lassen**

Ausführen: `dotnet test --filter UnterhaltTests`
Erwartet: PASS (vier Tests).

- [ ] **Schritt 9: Committen (auf Zuruf)**

```bash
git add Conspiratio.Lib/Allgemein/AbrechnungsErgebnis.cs Conspiratio.Lib/Allgemein/AbrechnungsManager.cs Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs Conspiratio.Lib.Tests/UnterhaltTests.cs
git commit -m "Unterhalt je besessener Werkstatt, progressiv in ihrer Zahl"
```

---

### Aufgabe 2: Hofhaltung als gewählte Stufe

**Dateien:**
- Ändern: `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` (neues Feld + Zugriffe)
- Ändern: `Conspiratio.Lib/Allgemein/AbrechnungsManager.cs` (Region „Hofhaltung")
- Test: `Conspiratio.Lib.Tests/HofhaltungTests.cs` (erweitern)

**Schnittstellen:**
- Nutzt: `AbrechnungsErgebnis.Unterhalt` aus Aufgabe 1 (nur Nachbarschaft, keine Abhängigkeit)
- Liefert: `HumSpieler.GetHofhaltungAbweichung() → int`, `HumSpieler.SetHofhaltungAbweichung(int)`,
  `AbrechnungsManager.HofhaltungFaktorProzent(int abweichung) → int`

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

An `HofhaltungTests.cs` anhängen:

```csharp
        /// <summary>
        /// Der Spieler waehlt die Stufe selbst. Gespeichert wird die Abweichung von der Mitte, damit die
        /// 0 aus einem alten Spielstand "standesgemaess" bedeutet - siehe den Savegame-Test unten.
        /// </summary>
        [Theory]
        [InlineData(-1, 50)]
        [InlineData(0, 100)]
        [InlineData(1, 200)]
        public void Die_Stufe_skaliert_den_standesgemaessen_Aufwand(int abweichung, int prozent)
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.SetTitel(Herzog);
            spieler.SetTaler(1000000);
            spieler.SetHofhaltungAbweichung(abweichung);

            var ergebnis = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.Equal(50000 * prozent / 100, ergebnis.Hofhaltung);
        }

        /// <summary>
        /// Der Savegame-Test: Ein alter Stand kennt das Feld nicht, es kommt als 0 an - und 0 muss
        /// exakt das bisherige Verhalten bedeuten, sonst aendert sich fuer bestehende Spieler
        /// stillschweigend die Abrechnung.
        /// </summary>
        [Fact]
        public void Ein_alter_Spielstand_haelt_standesgemaess_Hof()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.SetTitel(Herzog);
            spieler.SetTaler(1000000);

            // Kein SetHofhaltungAbweichung: Das Feld steht auf seinem Standardwert, wie nach dem Laden.
            Assert.Equal(0, spieler.GetHofhaltungAbweichung());
            Assert.Equal(50000, new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler().Hofhaltung);
        }
```

- [ ] **Schritt 2: Test laufen lassen und Fehlschlag bestätigen**

Ausführen: `dotnet test --filter HofhaltungTests`
Erwartet: Compilerfehler — `SetHofhaltungAbweichung` existiert nicht.

- [ ] **Schritt 3: Feld und Zugriffe anlegen**

In `HumSpieler.cs` beim übrigen Feldblock ergänzen:

```csharp
        /// <summary>
        /// Abweichung vom standesgemaessen Hofhaltungsaufwand: -1 sparsam, 0 standesgemaess,
        /// +1 aufwendig. Bewusst als Abweichung gespeichert und nicht als Stufe: Serialisierung ist
        /// feldbasiert, ein aelterer Spielstand liefert 0 - und 0 bedeutet so das bisherige Verhalten.
        /// </summary>
        private int _hofhaltungAbweichung;
```

Und die Zugriffe:

```csharp
        public int GetHofhaltungAbweichung()
        {
            return _hofhaltungAbweichung;
        }

        public void SetHofhaltungAbweichung(int wert)
        {
            _hofhaltungAbweichung = System.Math.Max(-1, System.Math.Min(1, wert));
        }
```

- [ ] **Schritt 4: Abrechnung auf die Stufe umstellen**

In `AbrechnungsManager.cs` die Zeile

```csharp
            ergebnis.Hofhaltung = SW.Statisch.GetTitelX(spieler.GetTitel()).GetJahresaufwand();
```

ersetzen durch:

```csharp
            ergebnis.Hofhaltung = SW.Statisch.GetTitelX(spieler.GetTitel()).GetJahresaufwand()
                                  * HofhaltungFaktorProzent(spieler.GetHofhaltungAbweichung()) / 100;
```

Und die Umrechnung als Methode der Klasse:

```csharp
        /// <summary>
        /// Der Aufwand als Prozentsatz des standesgemaessen: sparsam die Haelfte, standesgemaess genau,
        /// aufwendig das Doppelte.
        /// </summary>
        public static int HofhaltungFaktorProzent(int abweichung)
        {
            if (abweichung < 0)
                return 50;

            return abweichung > 0 ? 200 : 100;
        }
```

- [ ] **Schritt 5: Tests laufen lassen**

Ausführen: `dotnet test --filter HofhaltungTests`
Erwartet: PASS — die neuen Tests und die fünf bestehenden.

- [ ] **Schritt 6: Committen (auf Zuruf)**

```bash
git add Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs Conspiratio.Lib/Allgemein/AbrechnungsManager.cs Conspiratio.Lib.Tests/HofhaltungTests.cs
git commit -m "Hofhaltung als gewaehlte Stufe, gespeichert als Abweichung"
```

---

### Aufgabe 3: Geltung aus Hofhaltung und Auslastung

**Dateien:**
- Ändern: `Conspiratio.Lib/Allgemein/AbrechnungsManager.cs`
- Test: `Conspiratio.Lib.Tests/AnsehenTests.cs` (neu)

**Schnittstellen:**
- Nutzt: `HumSpieler.GetHofhaltungAbweichung()` (Aufgabe 2), `HumSpieler.ZaehleWerkstaetten()` (Aufgabe 1)
- Liefert: `AbrechnungsManager.AnsehenJeTalerHofhaltung`, `AbrechnungsManager.AnsehenMaxProJahr`,
  `AbrechnungsManager.AuslastungsschwelleProzent` (alle public const int)

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

`Conspiratio.Lib.Tests/AnsehenTests.cs`:

```csharp
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Geltung laesst sich erwirtschaften oder erkaufen: Wer aufwendig Hof haelt, gewinnt permanentes
    /// Ansehen; wer spart, verliert es. Die Waehrung ist PermaAnsehen - dieselbe, in der Kirchenaustritt,
    /// Kerker und Pranger rechnen.
    /// </summary>
    public class AnsehenTests
    {
        private const int Herzog = 9;

        [Fact]
        public void Aufwendige_Hofhaltung_bringt_permanentes_Ansehen()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.SetTitel(Herzog);
            spieler.SetTaler(1000000);
            spieler.SetHofhaltungAbweichung(1);

            int vorher = spieler.GetPermaAnsehen();
            new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.True(spieler.GetPermaAnsehen() > vorher, "Mehraufwand muss Geltung bringen.");
        }

        [Fact]
        public void Sparsame_Hofhaltung_kostet_permanentes_Ansehen()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.SetTitel(Herzog);
            spieler.SetTaler(1000000);
            spieler.SetHofhaltungAbweichung(-1);

            int vorher = spieler.GetPermaAnsehen();
            new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.True(spieler.GetPermaAnsehen() < vorher, "Wer spart, verliert Geltung.");
        }

        /// <summary>
        /// Der Gewinn ist gedeckelt: Geltung waechst ueber Jahre, nicht in einem Zug. Ohne Deckel liesse
        /// sich mit einem einzigen aufwendigen Jahr jedes Ansehensniveau kaufen.
        /// </summary>
        [Fact]
        public void Der_Ansehensgewinn_ist_auf_das_Jahresmaximum_gedeckelt()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.SetTitel(Herzog);
            spieler.SetTaler(100000000);
            spieler.SetHofhaltungAbweichung(1);

            int vorher = spieler.GetPermaAnsehen();
            new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.True(spieler.GetPermaAnsehen() - vorher <= AbrechnungsManager.AnsehenMaxProJahr,
                        "Der Jahresgewinn darf das Maximum nicht ueberschreiten.");
        }
    }
}
```

- [ ] **Schritt 2: Test laufen lassen und Fehlschlag bestätigen**

Ausführen: `dotnet test --filter AnsehenTests`
Erwartet: Compilerfehler — `AnsehenMaxProJahr` existiert nicht.

- [ ] **Schritt 3: Umrechnung einbauen**

In `AbrechnungsManager.cs` die Konstanten ergänzen:

```csharp
        /// <summary>Taler Mehraufwand je Punkt permanenten Ansehens. Kalibriert in Aufgabe 5.</summary>
        public const int AnsehenJeTalerHofhaltung = 2000;

        /// <summary>
        /// Obergrenze des Ansehensgewinns pro Jahr. Zehnerbetraege, keine Hunderter: Nach dem Wegfall
        /// des Geldansehens liegen KI-Werte bei 15 bis 60, Aemterboni bei 3 bis 12.
        /// </summary>
        public const int AnsehenMaxProJahr = 15;

        /// <summary>Ab dieser mittleren Auslastung gilt ein Betrieb als gut gefuehrt.</summary>
        public const int AuslastungsschwelleProzent = 80;
```

Und in der Region `#region Hofhaltung`, nach der Verbuchung der Hofhaltung:

```csharp
            // Der Mehr- oder Minderaufwand gegenueber dem standesgemaessen Satz schlaegt sich in
            // permanentem Ansehen nieder - der Waehrung, in der auch Kirchenaustritt (-100), Kerker und
            // Pranger rechnen. Gedeckelt, damit Geltung ueber Jahre waechst und nicht in einem Zug.
            int standesgemaess = SW.Statisch.GetTitelX(spieler.GetTitel()).GetJahresaufwand();
            int abweichungTaler = ergebnis.Hofhaltung - standesgemaess;

            if (abweichungTaler != 0)
            {
                int punkte = abweichungTaler / AnsehenJeTalerHofhaltung;

                if (punkte > AnsehenMaxProJahr)
                    punkte = AnsehenMaxProJahr;
                else if (punkte < -AnsehenMaxProJahr)
                    punkte = -AnsehenMaxProJahr;

                // Sparen kostet auch dann Geltung, wenn der Betrag fuer einen vollen Punkt nicht reicht.
                if (punkte == 0)
                    punkte = abweichungTaler > 0 ? 1 : -1;

                spieler.ErhoehePermaAnsehen(punkte);
            }
```

- [ ] **Schritt 4: Tests laufen lassen**

Ausführen: `dotnet test --filter AnsehenTests`
Erwartet: PASS (drei Tests).

- [ ] **Schritt 5: Auslastungsbelohnung als Test schreiben**

An `AnsehenTests.cs` anhängen:

```csharp
        /// <summary>
        /// Die Gegenrichtung zum Unterhalt: Der bestraft nur, wer schlecht auslastet. Damit gutes
        /// Wirtschaften auch gewinnt, bringt hohe Auslastung Geltung - erwirtschaftet statt erkauft.
        /// </summary>
        [Fact]
        public void Gut_ausgelastete_Werke_bringen_Geltung()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            var slot = spieler.GetProduktionsslot(1, 0);
            int rohstoffId = SW.Dynamisch.GetStadtwithID(1).GetSingleRohstoff(1);
            var ware = SW.Dynamisch.GetRohstoffwithID(rohstoffId);
            int proStaette = ware.GetWerkstaetten() > 0 ? ware.GetArbeiter() / ware.GetWerkstaetten() : 1;

            spieler.GetSpielerHatInStadtXWerkstaettenY(1, 1).SetEnabled(true);
            slot.SetTaetigkeit((int)EnumProduktionsslotAktionsart.Produzieren);
            slot.SetProduktionRohstoff(rohstoffId);
            slot.SetProduktionStaetten(1);
            slot.SetProduktionArbeiter(proStaette);

            int vorher = spieler.GetPermaAnsehen();
            new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.True(spieler.GetPermaAnsehen() > vorher, "Volle Auslastung muss Geltung bringen.");
        }

        /// <summary>Ein stillgelegter Betrieb bringt nichts - die Belohnung wirkt nur nach oben.</summary>
        [Fact]
        public void Stillgelegte_Werke_bringen_keine_Geltung()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();
            spieler.GetSpielerHatInStadtXWerkstaettenY(1, 1).SetEnabled(true);

            int vorher = spieler.GetPermaAnsehen();
            new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();

            Assert.Equal(vorher, spieler.GetPermaAnsehen());
        }
```

Dafür oben ergänzen: `using Conspiratio.Lib.Gameplay.Niederlassung;`

- [ ] **Schritt 6: Test laufen lassen und Fehlschlag bestätigen**

Ausführen: `dotnet test --filter AnsehenTests`
Erwartet: `Gut_ausgelastete_Werke_bringen_Geltung` schlägt fehl (kein Ansehensgewinn).

- [ ] **Schritt 7: Auslastung berechnen und belohnen**

In `AbrechnungsManager.cs`, in der Schleife über die Produktionsslots (Region „Arbeiter-, Betriebs- und
Transportkosten"), im `Produzieren`-Zweig hinter den Kostenzeilen die Auslastung mitsammeln. Dafür vor
der Schleife zwei Zähler anlegen:

```csharp
            double auslastungSumme = 0;
            int bewerteteSlots = 0;
```

Im `Produzieren`-Zweig, direkt nach `ergebnis.Betriebskosten += …`:

```csharp
                        // Auslastung = gesetzte zu benoetigten Arbeitern, dasselbe Verhaeltnis, mit dem
                        // Produktionsslot.GetProduktion den Ertrag anteilig senkt.
                        var wareSlot = SW.Dynamisch.GetRohstoffwithID(rohstoffId);
                        int proStaette = wareSlot.GetWerkstaetten() > 0
                            ? wareSlot.GetArbeiter() / wareSlot.GetWerkstaetten()
                            : 1;
                        int benoetigt = produktionsslot.GetProduktionStaetten() * proStaette;

                        if (benoetigt > 0)
                        {
                            double quote = (double)produktionsslot.GetProduktionArbeiter() / benoetigt;

                            auslastungSumme += quote > 1 ? 1 : quote;
                            bewerteteSlots++;
                        }
```

Und in der Region „Hofhaltung", hinter der Ansehensbuchung aus Schritt 3:

```csharp
            // Gut gefuehrte Werke bringen Geltung. Bewusst mit hoher Schwelle: eine Auszeichnung, kein
            // Grundzustand. Nur nach oben wirksam, es gibt also nichts auszunutzen.
            if (bewerteteSlots > 0)
            {
                int auslastungProzent = (int)(100 * auslastungSumme / bewerteteSlots);

                if (auslastungProzent >= AuslastungsschwelleProzent)
                    spieler.ErhoehePermaAnsehen(1);
            }
```

- [ ] **Schritt 8: Tests laufen lassen**

Ausführen: `dotnet test --filter AnsehenTests`
Erwartet: PASS (fünf Tests).

- [ ] **Schritt 9: Committen (auf Zuruf)**

```bash
git add Conspiratio.Lib/Allgemein/AbrechnungsManager.cs Conspiratio.Lib.Tests/AnsehenTests.cs
git commit -m "Geltung aus Hofhaltung und Auslastung in permanentem Ansehen"
```

---

### Aufgabe 4: Das Geldansehen entfällt

**Dateien:**
- Ändern: `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` (`AnsehenAktualisieren`, Zeile 849)
- Test: `Conspiratio.Lib.Tests/AnsehenTests.cs` (erweitern)

**Schnittstellen:**
- Ändert das Verhalten von `HumSpieler.AnsehenAktualisieren()`; die Signatur bleibt.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

An `AnsehenTests.cs` anhängen:

```csharp
        /// <summary>
        /// Ansehen war faktisch der Kontostand: 2 500 Taler ergaben einen Punkt, bei 2,9 Mio. also
        /// 1 160 - gegen 3 bis 12 fuer ein Amt und 15 bis 60 fuer einen KI-Spieler insgesamt. In der
        /// Wahl (AemterManager, Ansehen/10) war das ein Vorsprung, den kein Gegner einholen konnte.
        /// AnsehenAktualisieren gibt es zudem nur auf HumSpieler - es war ein reiner Spielervorteil.
        /// </summary>
        [Fact]
        public void Der_Geldbestand_kauft_kein_Ansehen_mehr()
        {
            TestSpielwelt.Starte();

            var spieler = SW.Dynamisch.GetAktHum();

            spieler.SetTaler(10000);
            spieler.AnsehenAktualisieren();
            int arm = spieler.GetAnsehen();

            spieler.SetTaler(5000000);
            spieler.AnsehenAktualisieren();
            int reich = spieler.GetAnsehen();

            Assert.Equal(arm, reich);
        }
```

- [ ] **Schritt 2: Test laufen lassen und Fehlschlag bestätigen**

Ausführen: `dotnet test --filter Der_Geldbestand_kauft_kein_Ansehen_mehr`
Erwartet: FAIL — `reich` liegt um rund 2 000 Punkte über `arm`.

- [ ] **Schritt 3: Den Geldanteil entfernen**

In `HumSpieler.AnsehenAktualisieren()` diese beiden Zeilen ersatzlos streichen:

```csharp
            // Geldansehen
            ans_plus += Convert.ToInt32(GetTaler() / SW.Statisch.GetAnsehenProTaler());
```

Und darüber den erklärenden Kommentar setzen:

```csharp
            // Kein Geldansehen mehr: Der Kontostand ergab bei 2 500 Talern je Punkt Werte um 1 160,
            // waehrend ein Amt 3 bis 12 und ein ganzer KI-Spieler 15 bis 60 erreicht. Geltung war damit
            // gekauft statt verdient - und weil AnsehenAktualisieren nur auf HumSpieler existiert, war
            // es ein Vorteil, den die KI nie hatte. Ansehen kommt jetzt aus Taten (PermaAnsehen), Amt
            // und Wohnsitzen.
```

- [ ] **Schritt 4: Tests laufen lassen**

Ausführen: `dotnet test`
Erwartet: PASS. **Schlagen andere Tests fehl, ist das ein Befund, kein Versehen** — sie lesen dann
Schwellen, die auf das Geldansehen kalibriert waren. Solche Fehlschläge dokumentieren und mit dem
Nutzer klären, bevor Schwellen angepasst werden.

- [ ] **Schritt 5: Die Schuldenprozess-Schwelle prüfen**

`ZugNachrichtenManager:219` lautet
`spieler.GetTaler() < SW.Statisch.GetMaxSchulden() - spieler.GetAnsehen() * 10`.
Diese Schwelle tolerierte umso mehr Schulden, je höher das Ansehen war — und das kam aus dem Geld. Nach
der Änderung greift der Schuldenprozess früher. Das ist beabsichtigt; belegt wird es in Aufgabe 6 gegen
die E2E-Grundlinie, weil der Schuldturm ein absorbierender Zustand ist.

Keine Codeänderung in diesem Schritt — nur die bewusste Kenntnisnahme und ein Vermerk im CHANGELOG.

- [ ] **Schritt 6: Committen (auf Zuruf)**

```bash
git add Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs Conspiratio.Lib.Tests/AnsehenTests.cs
git commit -m "Geldansehen entfaellt: Geltung wird verdient, nicht besessen"
```

---

### Aufgabe 5: Die vier offenen Zahlen kalibrieren

**Dateien:**
- Ändern: `Conspiratio.Lib/Allgemein/AbrechnungsManager.cs` (nur die Konstanten)
- Werkzeug: die Wegwerf-Harness (siehe unten)

**Schnittstellen:**
- Nutzt alle Konstanten aus den Aufgaben 1 bis 3. Erzeugt keine neuen.

- [ ] **Schritt 1: Lib-Version anheben und ins globale NuGet-Cache bringen**

`<Version>` in `Conspiratio.Lib.csproj` auf `4.5.0` setzen, dann:

```bash
dotnet build
rm -rf ~/.nuget/packages/conspiratio.lib/4.5.0
dotnet restore --source "D:/Projekte/C# Projekte/Conspiratio.Lib/Conspiratio.Lib/bin/Debug"
```

Der Purge-Schritt ist der, der beißt: NuGet extrahiert eine Version nicht neu, die es schon hat, und der
Build nutzt still den alten Code weiter. Prüfen mit `ls ~/.nuget/packages/conspiratio.lib/`.

- [ ] **Schritt 2: Die Harness auf 4.5.0 heben und die Kurve messen**

Die Harness liegt im Scratchpad (`harness/Program.cs`, `PackageReference` auf `Conspiratio.Lib`). Version
auf `4.5.0` setzen, `dotnet run`. Sie baut je Rasterzelle einen frischen Spielzustand auf und gibt die
Jahreslast als Anteil am Gesamtvermögen aus.

Die Zielkurve, gegen die kalibriert wird — vorher gemessen mit Lib 4.4.1:

| Vermögen | 100k | 250k | 500k | 1,0M | 2,0M | 2,9M | 3,1M | 8M |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Jahreslast alt | 8,0 % | 10,0 % | 10,0 % | 5,0 % | 2,5 % | 1,7 % | 16,4 % | 15,5 % |

**Zielbild:** Die Last fällt über die Skala **nicht mehr auf 1,7 %**, ohne dass die Spitze im Frühspiel
über die heutigen 10 % steigt. Die Harness kennt nur Werkstattbesitz, kein Produktionsvolumen — für die
Auslastungsbelohnung ist sie deshalb blind; die prüfen die Tests aus Aufgabe 3.

- [ ] **Schritt 3: `GrundunterhaltProWerkstatt` festlegen**

Zwei Schranken:
- **Untere Schranke (Einsteiger):** Der Test `Ein_Einsteiger_mit_zwei_Betrieben_zahlt_kaum_Unterhalt`
  hält den Betrag für zwei Betriebe bei höchstens 500 Talern.
- **Obere Schranke (Expansion):** Der Unterhalt des *n*-ten Betriebs muss klar unter dem
  Deckungsbeitrag einer ausgelasteten Werkstätte liegen, sonst lohnt Expansion nie. Der Deckungsbeitrag
  ergibt sich aus `Rohstoff.GetWSEinzelpreis`, `GetWSArbeiterpreis` und dem Verkaufspreis der Ware.

Wert eintragen, `dotnet test --filter UnterhaltTests` laufen lassen, Harness erneut messen.

- [ ] **Schritt 4: Ansehenskurs und Deckel festlegen**

`AnsehenJeTalerHofhaltung` und `AnsehenMaxProJahr` so wählen, dass ein Spieler über etwa zehn Jahre
aufwendiger Hofhaltung in die Größenordnung eines KI-Ansehens (15 bis 60) kommt — nicht darüber hinaus in
einem Zug. `AuslastungsschwelleProzent` bleibt bei 80, sofern die Tests aus Aufgabe 3 tragen.

- [ ] **Schritt 5: Gesamtes Testpaket laufen lassen**

Ausführen: `dotnet test`
Erwartet: PASS.

- [ ] **Schritt 6: Lib-CHANGELOG schreiben und committen (auf Zuruf)**

Unter dem bestehenden `## [Unreleased]` ergänzen, bilingual (DE und EN), je ein Punkt für Unterhalt,
Hofhaltungsstufe, Geltung aus Auslastung und Wegfall des Geldansehens — beim letzten ausdrücklich mit
dem Hinweis, dass laufende Spielstände sichtbar Ansehen verlieren und der Schuldenprozess früher greift.

---

### Aufgabe 6: Godot — Anzeige, Bildschirm und Abnahme

**Dateien:**
- Ändern: `Conspiratio.Godot.csproj` (Zeile 15, `PackageReference` auf `4.5.0`)
- Ändern: `assets/scripts/AbrechnungDialog.cs:50` (Umfeld)
- Erstellen: `assets/scripts/HofhaltungDialog.cs`, `scenes/dialogs/HofhaltungDialog.tscn`
- Ändern: `assets/scripts/Kontor.cs` (Einstieg)
- Ändern: `CHANGELOG.md`

**Schnittstellen:**
- Nutzt: `AbrechnungsErgebnis.Unterhalt`, `HumSpieler.GetHofhaltungAbweichung` /
  `SetHofhaltungAbweichung`, `AbrechnungsManager.HofhaltungFaktorProzent`

- [ ] **Schritt 1: Lib-Version anheben und bauen**

In `Conspiratio.Godot.csproj` die Zeile
`<PackageReference Include="Conspiratio.Lib" Version="4.4.1" />` auf `4.5.0` setzen, dann `dotnet build`.
Erwartet: 0 Fehler.

- [ ] **Schritt 2: Den Unterhalt in der Abrechnung ausweisen**

In `AbrechnungDialog.cs`, direkt vor `AddPosition("Hofhaltung", ergebnis.Hofhaltung);`:

```csharp
		AddPosition("Unterhalt", ergebnis.Unterhalt);
```

- [ ] **Schritt 3: Den Hofhaltungs-Dialog anlegen**

`assets/scripts/HofhaltungDialog.cs` nach dem Muster von `ProzentwertFestlegenDialog` und
`GerichtDialog.WaehleOption` (drei `LinkButtonWithSounds` in einer `VBoxContainer`, `DialogBase` als
Basis, `ShowDialog()` liefert `Task`):

```csharp
using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Spieler waehlt, wie aufwendig er Hof haelt: sparsam, standesgemaess oder aufwendig. Der Aufwand
/// wird in der Jahresabrechnung faellig und schlaegt sich in permanentem Ansehen nieder - nach oben wie
/// nach unten. Gespeichert wird die Abweichung von der Mitte (-1 bis +1).
/// </summary>
public partial class HofhaltungDialog : DialogBase
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath VBoxStufenPath { get; set; }

	private Label _labelText;
	private VBoxContainer _vBoxStufen;
	private PackedScene _linkButtonScene;
	private TaskCompletionSource<bool> _abgeschlossen;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_vBoxStufen = GetNode<VBoxContainer>(VBoxStufenPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	// Waehrend die Auswahl offen ist, darf ein Rechtsklick nichts tun - es muss ein Knopf gewaehlt
	// werden, sonst bliebe die Stufe unbestimmt.
	protected override void OnNextOrClose() { }

	public Task ShowDialog()
	{
		var spieler = SW.Dynamisch.GetAktHum();
		int standesgemaess = SW.Statisch.GetTitelX(spieler.GetTitel()).GetJahresaufwand();

		_labelText.Text = "Wie wollt Ihr Hof halten?\nStandesgemaess kostet Euch das "
		                  + standesgemaess.ToStringGeld() + " im Jahr.";

		foreach (Node kind in _vBoxStufen.GetChildren())
		{
			_vBoxStufen.RemoveChild(kind);
			kind.QueueFree();
		}

		ErzeugeStufe("Sparsam", -1, standesgemaess);
		ErzeugeStufe("Standesgemaess", 0, standesgemaess);
		ErzeugeStufe("Aufwendig", 1, standesgemaess);

		_abgeschlossen = new TaskCompletionSource<bool>();
		ShowAndEnableInput();

		return _abgeschlossen.Task;
	}

	private void ErzeugeStufe(string bezeichnung, int abweichung, int standesgemaess)
	{
		int kosten = standesgemaess * AbrechnungsManager.HofhaltungFaktorProzent(abweichung) / 100;

		var knopf = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
		knopf.Text = bezeichnung + " (" + kosten.ToStringGeld() + " im Jahr)";
		knopf.Pressed += () => OnStufeGewaehlt(abweichung);

		_vBoxStufen.AddChild(knopf);
	}

	private void OnStufeGewaehlt(int abweichung)
	{
		SW.Dynamisch.GetAktHum().SetHofhaltungAbweichung(abweichung);
		HideAndDisableInput();
		_abgeschlossen?.TrySetResult(true);
	}
}
```

Die Szene `scenes/dialogs/HofhaltungDialog.tscn` nach dem Pergament-Muster: `NinePatchRect` namens
`Rahmen` mit `BackgroundDialog.png` (`uid://cq1th2hp46uxa`), `patch_margin` 15 auf allen Seiten,
Textfarbe `Color(0.16, 0.11, 0.05)`, gemeinsames Theme (`uid://bkne0d4c2vxts`), Gruppe `Dialogs`.
Kinder: `Label` (`LabelText`) und `VBoxContainer` (`VBoxStufen`). Am einfachsten durch Kopieren einer
bestehenden Pergament-Szene wie `StatistikDialog.tscn`.

Als öffentliches Feld `public HofhaltungDialog HofhaltungDialog;` an `Main` — `Main.VerdrahteKnoten`
verdrahtet jedes öffentliche `Node`-Feld über den Typnamen, ein gleichnamiger Knoten in `Main.tscn`
genügt.

- [ ] **Schritt 4: Einstieg in der Schreibstube**

Der Aufwand ist eine Standesfrage, kein Handel — der Einstieg gehört zu den Verwaltungspunkten der
Schreibstube. Die arbeitet über ein festes Array unsichtbarer Klickflächen mit Signalhandlern, die in
der `.tscn` verbunden sind.

In `assets/scripts/Schreibstube.cs` das Array (Zeile 15) erweitern:

```csharp
	private static readonly string[] AreaNamen = { "AreaBewerbung", "AreaGeldleiher", "AreaGesetze", "AreaKreditbuch", "AreaPrivilegien", "AreaKontrahenten", "AreaHofhaltung" };
```

Und den Handler nach dem Muster von `_on_area_kontrahenten_pressed` ergänzen:

```csharp
	private async void _on_area_hofhaltung_pressed()
	{
		SetProcessInput(false);

		await _main.HofhaltungDialog.ShowDialog();

		if (Visible)
			SetProcessInput(true);
	}
```

In `scenes/Schreibstube.tscn` einen `Button` namens `AreaHofhaltung` anlegen (die übrigen Flächen als
Vorlage: unsichtbar, die Beschriftung erscheint nur bei MouseOver) und sein `pressed`-Signal auf
`_on_area_hofhaltung_pressed` verbinden. Ohne die Verbindung in der Szene passiert beim Klick nichts —
die Handler werden in diesem Projekt nicht im Code verdrahtet.

- [ ] **Schritt 5: Headless-Rauchtest**

```bash
"C:/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . --import
"C:/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . --quit "res://scenes/Main.tscn"
```

Erwartet: saubere Ausgabe. Danach `git diff -w --stat CLAUDE.md` prüfen — `--import` schreibt in dieser
Datei Einrückungen um; ist die Ausgabe leer, mit `git checkout -- CLAUDE.md` verwerfen.

- [ ] **Schritt 6: E2E-Abnahme gegen die Grundlinie**

```bash
godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=15 --spieler=1 --ohne-aktionen --seed=1234
```

Über zehn Seeds wiederholen und gegen Schicht 5 in [`docs/e2e-messwerte.md`](../../e2e-messwerte.md)
halten: Band **+3 900 bis +53 000**, Median 22 480, keiner negativ. Dann ein 40-Jahre-Lauf
(`--jahre=40`), Band +38 000 bis +178 000.

**Das entscheidende Kriterium:** Der Schuldenprozess darf nicht häufiger auslösen als vorher. Die
Schwelle hängt am Ansehen (`GetMaxSchulden() - Ansehen * 10`), und das ist durch Aufgabe 4 gefallen. Der
Schuldturm ist ein absorbierender Zustand — kippen Läufe dorthin, ist das der Grund, und dann muss
`GetMaxSchulden()` oder der Ansehensfaktor nachgezogen werden.

Ein neues Band in `docs/e2e-messwerte.md` als Schicht 6 nachtragen; die Änderung formt den Zufallsstrom
um, Schicht 5 ist danach als Vergleichsband entwertet.

- [ ] **Schritt 7: CHANGELOG und Commit (auf Zuruf)**

`CHANGELOG.md` bilingual ergänzen (DE und EN). Commit-Betreff mit Lib-Bezug:

```bash
git add Conspiratio.Godot.csproj assets/scripts/AbrechnungDialog.cs assets/scripts/HofhaltungDialog.cs scenes/dialogs/HofhaltungDialog.tscn assets/scripts/Schreibstube.cs scenes/Main.tscn CHANGELOG.md docs/e2e-messwerte.md
git commit -m "Unterhalt, Hofhaltungsstufe und verdiente Geltung (Conspiratio.Lib 4.5.0)"
```
