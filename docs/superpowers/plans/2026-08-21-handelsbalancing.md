# Handelsbalancing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den unbegrenzten Skalierungsvorteil mehrerer Kontore bremsen — über einen sättigungsabhängigen Warenpreis und einen nach Besitzstand gestaffelten Werkstatt-Kaufpreis — und den dafür nötigen Warenkreislauf samt Stadtentwicklung überhaupt erst zum Laufen bringen.

**Architecture:** Vier Lib-Änderungen und eine Verdrahtung im Client. `Stadt.GetRohstoffPreisVonIDX` misst den Mengenabschlag künftig in Jahren lokalen Bedarfs statt in fixen Talern; `HandelsManager.GetWerkstattKaufpreis` staffelt nach reichsweit besessenen Betrieben; `EinwohnerWachstumAktRundenEnde` und `ReichtumWachstumAktRundenEnde` geben den Städten erstmals eine Aufwärtsentwicklung — ohne sie verfielen Einwohnerzahl und Reichtum einbahnig, und mit dem Nenner des Abschlags auch der Handel. Zuletzt zieht der Godot-Client drei Rundenende-Aufrufe nach, die er nie vom Referenzclient übernommen hat — **ohne sie bliebe der Sättigungspreis vollständig folgenlos.** Es kommt kein serialisiertes Feld hinzu, also keine Savegame-Migration.

**Tech Stack:** C# / netstandard2.0 (`Conspiratio.Lib`), xUnit (`Conspiratio.Lib.Tests`), Godot 4.7 / .NET 8 für die E2E-Messung.

**Spec:** `docs/superpowers/specs/2026-08-21-handelsbalancing-design.md` (im Godot-Repo)

## Global Constraints

- **netstandard2.0** in der Lib — keine modernen BCL-APIs.
- **Ganzzahlarithmetik** in allen neuen Formeln, damit E2E-Läufe seed-reproduzierbar bleiben. Kein `Math.Pow`, kein `double` in den neuen Pfaden; das Wachstum rechnet in Promille.
- **Kein neues serialisiertes Feld.** Die Serialisierung ist feldbasiert; jedes neue Feld käme aus Altständen als `null`/`0`. Alle Änderungen lesen und schreiben ausschließlich vorhandene Felder.
- **Deutsche Domänenbenennung** für Konstanten, Methoden und Kommentare.
- **Balancing-Konstanten als `public const int`** auf der jeweiligen Klasse — etabliertes Muster der Lib (`ErpressungManager.BeziehungsverlustBeiMisserfolg`, `KatastrophenManager.PreisanstiegMin`).
- **Lib-CHANGELOG:** bilinguale Stichpunkte (DE und EN) unter dem einen `## [Unreleased]`-Block, **kein** Versionsheader und **kein** Datum pro Change.
- **Commit-Reihenfolge:** erst Lib, dann Godot. Der Godot-Commit nennt die Lib-Version im Betreff.
- **Commit-Nachrichten per POSIX-Heredoc** (`git commit -F - <<'EOF' … EOF`) — die Bash-Tool-Shell ist Git Bash, ein PowerShell-Here-String hängt ein `@` an die Betreffzeile.
- **Beide Repos stehen auf Branch `feature/ki-aggression-sabotage-anschwaerzen`.** Nicht mergen, nicht pushen.
- **Startwerte sind keine Endwerte.** Die Balancing-Zahlen aus Task 1–4 werden in Task 7 gegen echte Läufe kalibriert.
- **Reihenfolge ist bindend:** Erst die gesamte Lib-Arbeit (Task 1–5), dann die Godot-Verdrahtung (Task 6), dann die Messung (Task 7). Task 6 braucht die Lib-Version aus Task 5, und Task 7 braucht die Verdrahtung aus Task 6 — vorher ist nichts messbar.

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
- Die Tests laufen **nacheinander** (`AssemblyInfo.cs`), weil der Spielzustand global in `SW` liegt. Jeder Test beginnt mit eigenem `TestSpielwelt.Starte()`; die Signatur ist `Starte(int menschen = 1, IErpressungDialog erpressungDialog = null, int? seed = null)`.
- **Uneinheitliche Setter-Benennung** auf `Stadt`: `SetReichtumToX` gegen `SetKriminalitaetAufX` gegen `SetEinwohnerAufX` — eine häufige Fehlerquelle.
- `KatastrophenManager` senkt Einwohner **und** Reichtum, hebt beide nie an (`JahresChance` 22 %). Task 1 und 2 sind das Gegengewicht.
- Die Verkaufsmenge je Stadt (`GetEinVerkaeufeInStadtXVonRohstoffIDY`) ist **netto**: `KaufeRohstoff` bucht Einkäufe als `-gekauft` hinein, der Wert kann also negativ sein.
- `Stadt.SetReichtumToX` klemmt **nicht** gegen `GetMaxReichtum()` (= 14) — Aufrufer müssen das selbst tun.

---

### Task 1: Bevölkerungswachstum

**Files:**
- Modify: `Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs` (neue Methode neben `RohBedarfAktRundenEnde`, Zeile 634–651)
- Test: `Conspiratio.Lib.Tests/BevoelkerungTests.cs` (neu)

**Interfaces:**
- Consumes: nichts.
- Produces: `public void DynamischeSpieldaten.EinwohnerWachstumAktRundenEnde()` — wird in Task 6 vom Godot-Client aufgerufen. Konstanten auf `DynamischeSpieldaten`: `GrundwachstumPromille` (10), `ReichtumBonusPromille` (2), `KriminalitaetMalusPromille` (2), `ZufallPromille` (5), `MindestEinwohner` (250), `MaxEinwohner` (12000).

**Kontext:** `_einwohner` wird heute nur an zwei Stellen geschrieben — im Konstruktor und von `KatastrophenManager`, der ihn ausschließlich **senkt** (8–20 % je Ereignis, bei Pest verdoppelt, bis auf 0). Es gibt kein Wachstum. Über 40 Jahre bedeutet das rund 30 % Bevölkerungsverlust ohne Erholung.

**Warum das eine Voraussetzung ist:** Task 3 nutzt `Einwohner / 10` als Nenner. Sinkt die Einwohnerzahl dauerhaft, wird der Mengenabschlag immer steiler *und* der Verbrauch, der den Vorrat abbaut, immer kleiner — der Handel würde über die Spieldauer unrentabel. Historisch ist Wachstum um 1600 ohnehin geboten.

- [ ] **Step 1: Die fehlschlagenden Tests anlegen**

Neue Datei `Conspiratio.Lib.Tests/BevoelkerungTests.cs`:

```csharp
using Conspiratio.Lib.Gameplay.Spielwelt;

using Xunit;

namespace Conspiratio.Lib.Tests
{
    /// <summary>
    /// Bevölkerungswachstum: Bisher konnte die Einwohnerzahl nur durch Katastrophen sinken. Ohne
    /// Gegengewicht schrumpfen alle Städte über die Spieldauer, was den Warenabsatz dauerhaft
    /// unrentabel machen würde – der Jahresbedarf (<c>Einwohner / 10</c>) ist die Bezugsgröße des
    /// Mengenabschlags.
    /// </summary>
    public class BevoelkerungTests
    {
        private const int GrosseStadt = 1;
        private const int KleineStadt = 2;

        [Fact]
        public void Eine_Stadt_waechst_ueber_die_Runden()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            int vorher = stadt.GetEinwohner();

            for (int runde = 0; runde < 10; runde++)
                SW.Dynamisch.EinwohnerWachstumAktRundenEnde();

            Assert.True(stadt.GetEinwohner() > vorher,
                        "Nach zehn Runden muss die Stadt gewachsen sein, sonst fehlt das Gegengewicht zu den Katastrophen.");
        }

        [Fact]
        public void Reichtum_beschleunigt_und_Kriminalitaet_bremst_das_Wachstum()
        {
            TestSpielwelt.Starte(seed: 1);

            var reich = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            var arm = SW.Dynamisch.GetStadtwithID(KleineStadt);

            // Gleiche Ausgangsgröße, damit nur die Standortfaktoren den Unterschied machen.
            reich.SetEinwohnerAufX(4000);
            arm.SetEinwohnerAufX(4000);
            reich.SetReichtumToX(7);
            reich.SetKriminalitaetAufX(1);
            arm.SetReichtumToX(1);
            arm.SetKriminalitaetAufX(5);

            for (int runde = 0; runde < 25; runde++)
                SW.Dynamisch.EinwohnerWachstumAktRundenEnde();

            Assert.True(reich.GetEinwohner() > arm.GetEinwohner(),
                        "Reichtum zieht Menschen an, Kriminalität vertreibt sie – über 25 Runden muss sich das zeigen.");
        }

        [Fact]
        public void Das_Wachstum_ueberschreitet_die_Obergrenze_nie()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            stadt.SetEinwohnerAufX(DynamischeSpieldaten.MaxEinwohner);

            for (int runde = 0; runde < 50; runde++)
                SW.Dynamisch.EinwohnerWachstumAktRundenEnde();

            Assert.Equal(DynamischeSpieldaten.MaxEinwohner, stadt.GetEinwohner());
        }

        /// <summary>
        /// Multiplikatives Wachstum käme aus der Null nie heraus (0 × irgendetwas = 0). Die Untergrenze
        /// macht eine von Katastrophen verwüstete Stadt wieder erholbar.
        /// </summary>
        [Fact]
        public void Eine_entvoelkerte_Stadt_erholt_sich()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            stadt.SetEinwohnerAufX(0);

            SW.Dynamisch.EinwohnerWachstumAktRundenEnde();

            Assert.Equal(DynamischeSpieldaten.MindestEinwohner, stadt.GetEinwohner());
        }
    }
}
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~BevoelkerungTests"
```

Erwartet: Kompilierfehler `CS1061`/`CS0117` — `EinwohnerWachstumAktRundenEnde`, `MaxEinwohner` und `MindestEinwohner` gibt es noch nicht.

Die benötigten Setter gibt es bereits: `Stadt.SetReichtumToX` (`Stadt.cs:169`) und
`Stadt.SetKriminalitaetAufX` (`Stadt.cs:179`) — auf die **uneinheitliche Benennung** achten
(`…ToX` gegen `…AufX`), sie ist eine häufige Fehlerquelle.

- [ ] **Step 3: Konstanten und Methode ergänzen**

In `Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs` die Konstanten zu den übrigen Feldern der Klasse ergänzen:

```csharp
        /// <summary>Grundwachstum der Einwohnerzahl je Runde in Promille.</summary>
        public const int GrundwachstumPromille = 10;

        /// <summary>Zusätzliches Wachstum je Punkt Reichtum – Wohlstand zieht Menschen an.</summary>
        public const int ReichtumBonusPromille = 2;

        /// <summary>Wachstumsverlust je Punkt Kriminalität – Unsicherheit vertreibt Menschen.</summary>
        public const int KriminalitaetMalusPromille = 2;

        /// <summary>Zufällige Schwankung des Wachstums je Runde, plus/minus in Promille.</summary>
        public const int ZufallPromille = 5;

        /// <summary>
        /// Untergrenze der Einwohnerzahl. Zwingend, weil multiplikatives Wachstum aus der Null nie
        /// herauskäme – erst dadurch ist eine von Katastrophen verwüstete Stadt wieder erholbar.
        /// </summary>
        public const int MindestEinwohner = 250;

        /// <summary>
        /// Obergrenze der Einwohnerzahl. Bewusst eine globale Konstante statt einer Schranke relativ zum
        /// Startwert der Stadt: Dieser wird nirgends gespeichert, und ein Feld dafür wäre ein neues
        /// serialisiertes Feld mit Savegame-Folgen.
        /// </summary>
        public const int MaxEinwohner = 12000;
```

Und die Methode direkt nach `RohBedarfAktRundenEnde` (endet Zeile 651) einfügen:

```csharp
        /// <summary>
        /// Lässt die Städte wachsen. Ohne dies kennt die Einwohnerzahl nur eine Richtung: Der
        /// <c>KatastrophenManager</c> senkt sie, niemand hebt sie je an – über eine lange Partie
        /// schrumpfen damit alle Märkte, und mit ihnen der Warenabsatz.
        ///
        /// Gerechnet wird in Promille, damit kein Gleitkomma in den Kern der Ökonomie gerät und
        /// E2E-Läufe seed-reproduzierbar bleiben.
        /// </summary>
        public void EinwohnerWachstumAktRundenEnde()
        {
            for (int i = 1; i < SW.Statisch.GetMaxStadtID(); i++)
            {
                Stadt stadt = GetStadtwithID(i);

                int wachstumPromille = GrundwachstumPromille
                                     + stadt.GetReichtum() * ReichtumBonusPromille
                                     - stadt.GetKriminalitaet() * KriminalitaetMalusPromille
                                     + SW.Statisch.Rnd.Next(-ZufallPromille, ZufallPromille + 1);

                int neu = stadt.GetEinwohner() + (stadt.GetEinwohner() * wachstumPromille) / 1000;

                if (neu < MindestEinwohner)
                    neu = MindestEinwohner;

                if (neu > MaxEinwohner)
                    neu = MaxEinwohner;

                stadt.SetEinwohnerAufX(neu);
            }
        }
```

Prüfen, dass der Namensraum von `Stadt` (`Conspiratio.Lib.Gameplay.Gebiete`) in der Datei bereits eingebunden ist — `GetStadtwithID` wird dort schon verwendet, also sehr wahrscheinlich ja.

- [ ] **Step 4: Tests laufen lassen und Erfolg bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~BevoelkerungTests"
```

Erwartet: 4 Tests grün.

- [ ] **Step 5: Vollständige Testsuite laufen lassen**

```bash
dotnet test
```

Erwartet: alles grün. Kippt ein Katastrophentest, ist das ein **echter** Befund (die Untergrenze schwächt schwere Katastrophen ab) — melden statt den Test stumm anzupassen.

- [ ] **Step 6: Commit**

```bash
git add Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs Conspiratio.Lib.Tests/BevoelkerungTests.cs
git commit -F - <<'EOF'
Staedte wachsen wieder

Die Einwohnerzahl kannte bisher nur eine Richtung: Der KatastrophenManager
senkte sie um 8-20 % je Ereignis (Pest doppelt), angehoben hat sie nie jemand.
Ueber 40 Jahre sind das rund 30 % Verlust ohne Erholung - unrealistisch fuer
die Zeit um 1600 und die Grundlage dafuer, dass der Warenabsatz spaeter
unrentabel wird.

Das Wachstum bemisst sich an Reichtum und Kriminalitaet der Stadt plus einem
Zufallsanteil, gerechnet in Promille. Die Untergrenze ist zwingend: Aus der
Null kaeme multiplikatives Wachstum nie heraus.
EOF
```

---

### Task 2: Reichtumswachstum aus Handelsvolumen

**Files:**
- Modify: `Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs` (neue Methode direkt nach `EinwohnerWachstumAktRundenEnde` aus Task 1)
- Test: `Conspiratio.Lib.Tests/BevoelkerungTests.cs` (erweitern)

**Interfaces:**
- Consumes: nichts aus Task 1 fachlich, aber dieselbe Datei und dieselbe Testdatei — Task 1 zuerst lesen.
- Produces: `public void DynamischeSpieldaten.ReichtumWachstumAktRundenEnde()`, aufgerufen in Task 6. Konstanten auf `DynamischeSpieldaten`: `HandelsvolumenJeReichtumsChance` (100), `KriminalitaetReichtumsMalus` (3), `MaxReichtumsChance` (40).

**Kontext:** `_reichtum` verfällt genauso einbahnig wie die Einwohnerzahl — `KatastrophenManager` senkt ihn um 1–2 Punkte je Ereignis, angehoben hat ihn nie jemand. Gelesen wird er nur von `LagerraumManager` (Preiszuschlag beim Lagerausbau) und der Stadtinfo-Anzeige — sowie seit Task 1 vom Einwohnerwachstum.

**Warum ein Würfelwurf statt eines Zählers:** `Reichtum` ist ein kleiner Ganzzahlwert (Stadtdaten 1–7, Obergrenze `GetMaxReichtum()` = 14). Ein Bruchteil-Zähler für langsames Wachstum wäre ein **neues serialisiertes Feld** und damit ein Savegame-Thema. Die vom Handelsvolumen abhängige Jahreschance kommt ohne neues Feld aus und liefert zugleich den gewünschten Zufallsanteil.

**Zwei Fallstricke:**
1. **`SetReichtumToX` klemmt nicht** (`Stadt.cs:169`) — die Obergrenze muss die Methode selbst ziehen.
2. **Die Verkaufsmenge je Stadt ist netto**: `KaufeRohstoff` bucht Einkäufe mit `-gekauft` in dieselbe Zahl (`HandelsManager.cs:238`). Der Wert kann also negativ sein und braucht einen Boden bei 0.

- [ ] **Step 1: Die fehlschlagenden Tests ergänzen**

An `Conspiratio.Lib.Tests/BevoelkerungTests.cs` innerhalb der Klasse anfügen (`using Conspiratio.Lib.Allgemein;` oben ergänzen):

```csharp
        private const int Korn = 1;

        /// <summary>
        /// Trägt einen Jahresabsatz in der Stadt ein, so wie ihn <c>VerkaufeRohstoff</c> hinterlässt –
        /// ohne den vollen Handelsweg nachzuspielen.
        /// </summary>
        private static void SetzeJahresabsatz(int stadtId, int menge)
        {
            SW.Dynamisch.GetAktHum().SetEinVerkaeufeInStadtXVonRohstoffIDYAufZ(stadtId, Korn, menge);
        }

        [Fact]
        public void Ohne_Handel_steigt_der_Reichtum_nicht()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            SetzeJahresabsatz(GrosseStadt, 0);
            stadt.SetReichtumToX(4);

            for (int runde = 0; runde < 20; runde++)
                SW.Dynamisch.ReichtumWachstumAktRundenEnde();

            Assert.Equal(4, stadt.GetReichtum());
        }

        [Fact]
        public void Reger_Handel_macht_die_Stadt_reicher()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            stadt.SetReichtumToX(3);
            stadt.SetKriminalitaetAufX(1);

            // Der Wurf ist probabilistisch, deshalb über mehrere Runden prüfen statt über eine.
            for (int runde = 0; runde < 20; runde++)
            {
                SetzeJahresabsatz(GrosseStadt, 2400);
                SW.Dynamisch.ReichtumWachstumAktRundenEnde();
            }

            Assert.True(stadt.GetReichtum() > 3,
                        "Zwanzig Jahre regen Handels müssen den Reichtum der Stadt heben.");
        }

        [Fact]
        public void Kriminalitaet_bremst_den_Reichtumszuwachs()
        {
            TestSpielwelt.Starte(seed: 1);

            var sicher = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            var unsicher = SW.Dynamisch.GetStadtwithID(KleineStadt);

            sicher.SetReichtumToX(3);
            unsicher.SetReichtumToX(3);
            sicher.SetKriminalitaetAufX(1);
            unsicher.SetKriminalitaetAufX(5);

            for (int runde = 0; runde < 30; runde++)
            {
                SetzeJahresabsatz(GrosseStadt, 2000);
                SetzeJahresabsatz(KleineStadt, 2000);
                SW.Dynamisch.ReichtumWachstumAktRundenEnde();
            }

            Assert.True(sicher.GetReichtum() > unsicher.GetReichtum(),
                        "Bei gleichem Handel muss die sichere Stadt stärker profitieren.");
        }

        [Fact]
        public void Der_Reichtum_ueberschreitet_die_Obergrenze_nie()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            stadt.SetReichtumToX(SW.Statisch.GetMaxReichtum());
            stadt.SetKriminalitaetAufX(1);

            for (int runde = 0; runde < 40; runde++)
            {
                SetzeJahresabsatz(GrosseStadt, 100000);
                SW.Dynamisch.ReichtumWachstumAktRundenEnde();
            }

            // SetReichtumToX klemmt nicht von sich aus - die Methode muss es tun.
            Assert.Equal(SW.Statisch.GetMaxReichtum(), stadt.GetReichtum());
        }

        /// <summary>
        /// Hält die bindende Aufrufreihenfolge fest: <c>RohBedarfAktRundenEnde</c> nullt die
        /// Verkaufsmengen. Liefe es zuerst, misst das Reichtumswachstum dauerhaft ein Volumen von null
        /// und wäre wirkungslos. Dieser Test schlägt an, falls die Reihenfolge je vertauscht wird.
        /// </summary>
        [Fact]
        public void Nach_der_Vorratsbuchung_ist_das_Handelsvolumen_verbraucht()
        {
            TestSpielwelt.Starte(seed: 1);

            var stadt = SW.Dynamisch.GetStadtwithID(GrosseStadt);
            stadt.SetReichtumToX(3);
            stadt.SetKriminalitaetAufX(1);

            for (int runde = 0; runde < 20; runde++)
            {
                SetzeJahresabsatz(GrosseStadt, 2400);
                SW.Dynamisch.RohBedarfAktRundenEnde();      // verbraucht und nullt die Menge
                SW.Dynamisch.ReichtumWachstumAktRundenEnde();
            }

            Assert.Equal(3, stadt.GetReichtum());
        }
```

- [ ] **Step 2: Tests laufen lassen und Fehlschlag bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~BevoelkerungTests"
```

Erwartet: Kompilierfehler `CS1061` — `ReichtumWachstumAktRundenEnde` gibt es noch nicht.

- [ ] **Step 3: Konstanten und Methode ergänzen**

In `Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs` zu den Konstanten aus Task 1 ergänzen:

```csharp
        /// <summary>
        /// Abgesetzte Stückzahl je Prozentpunkt Chance auf einen Reichtumspunkt. 100 bedeutet: Ein
        /// Jahresabsatz von 2 400 Stück ergibt 24 Prozentpunkte.
        /// </summary>
        public const int HandelsvolumenJeReichtumsChance = 100;

        /// <summary>Abzug auf die Chance je Punkt Kriminalität – Unsicherheit schreckt Kaufleute ab.</summary>
        public const int KriminalitaetReichtumsMalus = 3;

        /// <summary>Obergrenze der Jahreschance, damit auch ein Riesenmarkt nicht jedes Jahr zulegt.</summary>
        public const int MaxReichtumsChance = 40;
```

Und die Methode direkt nach `EinwohnerWachstumAktRundenEnde` einfügen:

```csharp
        /// <summary>
        /// Lässt den Reichtum einer Stadt mit ihrem Handel wachsen. Wie die Einwohnerzahl kannte auch
        /// der Reichtum bisher nur eine Richtung: Der <c>KatastrophenManager</c> senkt ihn, angehoben
        /// hat ihn nie jemand.
        ///
        /// Gewürfelt statt aufsummiert, weil <c>Reichtum</c> ein kleiner Ganzzahlwert ist (Obergrenze
        /// <c>GetMaxReichtum()</c> = 14): Ein Bruchteil-Zähler für langsames Wachstum wäre ein neues
        /// serialisiertes Feld und damit ein Savegame-Thema.
        ///
        /// **Muss vor <see cref="RohBedarfAktRundenEnde"/> laufen** – jenes bucht die Verkaufsmengen in
        /// den Stadtvorrat und nullt sie danach. Umgekehrt gerufen misst diese Methode dauerhaft null.
        /// </summary>
        public void ReichtumWachstumAktRundenEnde()
        {
            for (int stadtId = 1; stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
            {
                Stadt stadt = GetStadtwithID(stadtId);

                if (stadt.GetReichtum() >= SW.Statisch.GetMaxReichtum())
                    continue;

                // Handelsvolumen der Stadt in diesem Jahr. Einkaeufe stehen negativ in derselben Zahl
                // (KaufeRohstoff bucht -gekauft), deshalb der Boden bei 0.
                int volumen = 0;

                for (int spielerId = 1; spielerId <= GetAktivSpielerAnzahl(); spielerId++)
                {
                    HumSpieler spieler = GetHumWithID(spielerId);

                    if (spieler == null)
                        continue;

                    for (int rohId = 1; rohId < SW.Statisch.GetMaxRohID(); rohId++)
                        volumen += spieler.GetEinVerkaeufeInStadtXVonRohstoffIDY(stadtId, rohId);
                }

                if (volumen < 0)
                    volumen = 0;

                int chance = volumen / HandelsvolumenJeReichtumsChance
                           - stadt.GetKriminalitaet() * KriminalitaetReichtumsMalus;

                if (chance > MaxReichtumsChance)
                    chance = MaxReichtumsChance;

                // SetReichtumToX klemmt nicht von sich aus, die Obergrenze steht oben in der Schleife.
                if (chance > 0 && SW.Statisch.Rnd.Next(0, 100) < chance)
                    stadt.SetReichtumToX(stadt.GetReichtum() + 1);
            }
        }
```

Prüfen, dass der Namensraum von `HumSpieler` (`Conspiratio.Lib.Gameplay.Personen`) in der Datei eingebunden ist — `GetHumWithID` wird dort schon verwendet, also sehr wahrscheinlich ja.

- [ ] **Step 4: Tests laufen lassen und Erfolg bestätigen**

```bash
dotnet test --filter "FullyQualifiedName~BevoelkerungTests"
```

Erwartet: 9 Tests grün (4 aus Task 1, 5 aus Task 2).

- [ ] **Step 5: Vollständige Testsuite laufen lassen**

```bash
dotnet test
```

Erwartet: alles grün.

- [ ] **Step 6: Commit**

```bash
git add Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs Conspiratio.Lib.Tests/BevoelkerungTests.cs
git commit -F - <<'EOF'
Reger Handel macht eine Stadt reicher

Wie die Einwohnerzahl kannte auch der Reichtum bisher nur eine Richtung: Der
KatastrophenManager senkt ihn um 1-2 Punkte je Ereignis, angehoben hat ihn nie
jemand. Die Chance auf einen Punkt haengt jetzt vom Handelsvolumen der Stadt ab,
gemindert durch ihre Kriminalitaet.

Gewuerfelt statt aufsummiert, weil Reichtum ein kleiner Ganzzahlwert ist - ein
Bruchteil-Zaehler waere ein neues serialisiertes Feld. Die Methode muss vor
RohBedarfAktRundenEnde laufen, das die Verkaufsmengen verbraucht und nullt; ein
Test haelt diese Reihenfolge fest.
EOF
```

---

### Task 3: Sättigungsabhängiger Warenpreis

**Files:**
- Modify: `Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs` (Methode `GetRohstoffPreisVonIDX`, Zeile 108–117)
- Test: `Conspiratio.Lib.Tests/HandelsbalancingTests.cs` (neu)

**Interfaces:**
- Consumes: nichts aus früheren Tasks.
- Produces: `public const int Stadt.AbschlagJeBedarfsjahrProzent` (Wert 10), `public const int Stadt.MaxAbschlagProzent` (Wert 50). Task 7 justiert diese beiden Werte.

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

### Task 4: Progressiver Werkstatt-Kaufpreis

**Files:**
- Modify: `Conspiratio.Lib/Gameplay/Personen/HumSpieler.cs` (neue Methode `ZaehleWerkstaetten`, einzufügen bei den übrigen Werkstatt-Zugriffen um Zeile 280–295)
- Modify: `Conspiratio.Lib/Allgemein/HandelsManager.cs` (Methode `GetWerkstattKaufpreis`, Zeile 64–67)
- Test: `Conspiratio.Lib.Tests/HandelsbalancingTests.cs` (erweitern)

**Interfaces:**
- Consumes: fachlich nichts aus Task 3 (die beiden Formeln sind unabhängig). **Aber:** Die Tests erweitern die in Task 3 angelegte Datei `HandelsbalancingTests.cs` und nutzen deren bereits vorhandene private Konstanten `GrosseStadt` (= 1) und `KleineStadt` (= 2) mit. Diese Datei zuerst lesen.
- Produces: `public int HumSpieler.ZaehleWerkstaetten()`, `public const int HandelsManager.SteigerungProzent` (Wert 125), `public const int HandelsManager.MaxSteigerungsstufen` (Wert 20). Task 7 justiert `SteigerungProzent`.

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

Erwartet: alle Tests grün — 5 aus Task 3 und 9 aus Task 4 (die `Theory` zählt als vier Fälle).

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

### Task 5: Lib-Version, CHANGELOG und lokale Paketbereitstellung

**Files:**
- Modify: `Conspiratio.Lib/Conspiratio.Lib.csproj` (Zeile 42, `<Version>`)
- Modify: `CHANGELOG.md` (im Lib-Repo)
- Modify: `Conspiratio.Godot.csproj` (Zeile 15, `PackageReference`)

**Interfaces:**
- Consumes: die fertigen Formeln aus Task 1 bis 4.
- Produces: eine im lokalen NuGet-Cache verfügbare Lib-Version `4.1.0`, gegen die Task 6 und 7 messen können.

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
- Städte wachsen: Die Einwohnerzahl steigt jährlich abhängig von Reichtum und Kriminalität; bisher konnte sie nur durch Katastrophen sinken.
- Reger Handel macht eine Stadt reicher: Der Reichtum steigt mit dem Handelsvolumen, gemindert durch die Kriminalität.
```

Englisch:
```markdown
- Goods prices now respond to market saturation: the volume discount is measured in years of local demand instead of a fixed amount, so it scales with city size.
- Workshop purchase price rises with each workshop already owned, giving expansion a diminishing return.
- Cities grow: population rises each year depending on wealth and crime; until now it could only fall through disasters.
- Busy trade makes a city wealthier: its wealth rises with trade volume, dampened by its crime level.
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

### Task 6: Fehlende Rundenende-Aufrufe im Godot-Client nachziehen

**Files:**
- Modify: `assets/scripts/Kontor.cs` (im Godot-Repo, `IstLetzterSpielerImJahr()`-Block, Zeile 737–747)

**Interfaces:**
- Consumes: `EinwohnerWachstumAktRundenEnde()` aus Task 1 und `ReichtumWachstumAktRundenEnde()` aus Task 2; die Lib-Version 4.1.0 aus Task 5.
- Produces: eine vollständige Rundenende-Sequenz — erst dadurch werden die Formeln aus Task 1–4 im Spiel überhaupt wirksam und in Task 7 messbar.

**Kontext — der wichtigste Task des Plans:** Der Godot-Client hat drei Aufrufe der Rundenende-Sequenz des WinForms-Referenzclients (`Conspiratio/Main.cs:6345–6350`) nie übernommen. Jeder existiert in der Lib und wird von **keinem** Godot-Skript aufgerufen:

| Fehlender Aufruf | Folge im heutigen Godot-Spiel |
|---|---|
| `RohBedarfAktRundenEnde()` | Verkäufe landen nie im Stadtvorrat, die Bevölkerung verbraucht nichts — der Vorrat bleibt für immer unverändert |
| `RohPreiseRandomSchwanken()` | Warenpreise stehen dauerhaft still, es gibt keinerlei Marktbewegung |
| `RundenBestechungenAbwickeln()` | Rundenbestechungen werden nie abgewickelt |

**Ohne den ersten wäre Task 3 vollständig folgenlos** — `_rohstoffVorrat` bliebe konstant, und ein vorratsabhängiger Preis hätte nichts, worauf er reagieren könnte.

(`DeliktpunkteBerechnen()` fehlt dort ebenfalls, ist aber unkritisch: `KircheManager` und `Kirchgang` rufen es auf. Es wird hier **nicht** ergänzt, sonst liefe es doppelt.)

- [ ] **Step 1: Den heutigen Zustand belegen**

```bash
for m in RohBedarfAktRundenEnde RohPreiseRandomSchwanken RundenBestechungenAbwickeln; do echo "$m: $(grep -rn "$m" --include=*.cs assets/ | wc -l) Treffer"; done
```

Erwartet: alle drei mit `0 Treffer`. Ist einer bereits vorhanden, ihn **nicht** ein zweites Mal ergänzen.

- [ ] **Step 2: Die vier Aufrufe einfügen**

In `assets/scripts/Kontor.cs` im Block `if (_rundenManager.IstLetzterSpielerImJahr())` **vor** `await HalteWahlenAb();` einfügen:

```csharp
			// Wirtschaftliches Rundenende – im WinForms-Original der Auftakt der Rundenereignisse
			// (Main.cs, RundenEndnachrichtenAnzeigen). Diese Aufrufe fehlten bisher vollständig, weshalb
			// Warenpreise stillstanden, Verkäufe nie im Stadtvorrat landeten und Bestechungen nie
			// abgewickelt wurden.
			// Reihenfolge beachten: Das Reichtumswachstum liest die Verkaufsmengen, die
			// RohBedarfAktRundenEnde anschließend verbraucht und nullt.
			SW.Dynamisch.RohPreiseRandomSchwanken();
			SW.Dynamisch.ReichtumWachstumAktRundenEnde();
			SW.Dynamisch.RohBedarfAktRundenEnde();
			SW.Dynamisch.EinwohnerWachstumAktRundenEnde();
			SW.Dynamisch.RundenBestechungenAbwickeln();
```

**Die Reihenfolge der mittleren drei ist bindend, nicht kosmetisch:**
`RohBedarfAktRundenEnde` bucht die Verkaufsmengen in den Stadtvorrat und **nullt sie danach**. Stünde
`ReichtumWachstumAktRundenEnde` dahinter, läse es dauerhaft ein Handelsvolumen von null und wäre
wirkungslos. Das Einwohnerwachstum steht bewusst *nach* dem Reichtumswachstum, damit es den frisch
erhöhten Reichtum als Faktor nutzt. Der Test `Nach_der_Vorratsbuchung_ist_das_Handelsvolumen_verbraucht`
aus Task 2 hält diese Abhängigkeit fest.

Das Wachstum läuft bewusst **vor** dem weiter unten folgenden `await ZeigeKatastrophe();`: Erst wächst die Stadt um ihre Jahresrate, dann schlägt gegebenenfalls die Katastrophe zu. So ist das Wachstum die langsame Grundlinie und die Katastrophe der Schock.

Die Datei nutzt Tabs zur Einrückung — die vorhandene Einrückung des Blocks übernehmen.

- [ ] **Step 3: Bauen**

```bash
dotnet build
```

Erwartet: 0 Fehler. Fehlt `SW`, ist `using Conspiratio.Lib.Gameplay.Spielwelt;` bereits in der Datei — `SW.Dynamisch` wird dort schon verwendet.

- [ ] **Step 4: Smoke-Test**

```bash
"/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . --quit "res://scenes/Main.tscn"
```

Erwartet: sauberer Durchlauf ohne Stacktrace. Fehler gehen auf **stderr**.

- [ ] **Step 5: Nachweisen, dass der Vorrat jetzt tatsächlich wächst**

Genau das war der blinde Fleck — deshalb hier ein Beleg statt eines Vertrauensvorschusses:

```bash
"/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=15 --spieler=1 --seed=4711 --ohne-aktionen > /tmp/verdrahtet.txt 2>&1; echo "exit=$?"; grep -E "Gespielte Jahre|Verkaufte Waren|Spieler 1:|E2E-Durchlauf" /tmp/verdrahtet.txt
```

Erwartet: exit 0 und ein Endvermögen, das **deutlich unter** dem Vorwert von +80 088 (Seed 4711) liegt. Bleibt es unverändert, greift die Sättigung nicht — dann zuerst prüfen, ob `RohBedarfAktRundenEnde` wirklich läuft, bevor an den Konstanten gedreht wird.

- [ ] **Step 6: Commit**

```bash
git add assets/scripts/Kontor.cs
git commit -F - <<'EOF'
Fehlende Rundenende-Aufrufe nachgezogen (Conspiratio.Lib 4.1.0)

Der Client hat drei Aufrufe der Rundenende-Sequenz des WinForms-Originals nie
uebernommen: RohBedarfAktRundenEnde (Verkaeufe landeten nie im Stadtvorrat,
die Bevoelkerung verbrauchte nichts), RohPreiseRandomSchwanken (die Preise
standen dauerhaft still) und RundenBestechungenAbwickeln. Dazu kommt das neue
EinwohnerWachstumAktRundenEnde.

Damit wird der Warenkreislauf ueberhaupt erst wirksam - ohne die Vorratsbuchung
haette der neue saettigungsabhaengige Preis nichts, worauf er reagieren kann.
EOF
```

---

### Task 7: Kalibrierung gegen E2E-Läufe und Dokumentation

**Files:**
- Modify (nur falls die Messung es verlangt): `Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs`, `Conspiratio.Lib/Allgemein/HandelsManager.cs` (die Balancing-Konstanten)
- Modify: `CLAUDE.md` (im Godot-Repo, Abschnitt zur Handelsrunde)

**Interfaces:**
- Consumes: die verdrahtete Rundenende-Sequenz aus Task 6.
- Produces: festgeschriebene Balancing-Werte und aktualisierte Referenzzahlen in `CLAUDE.md`.

**Kontext:** Alle Balancing-Zahlen aus Task 1–4 sind begründete Startwerte, keine Endwerte. Der Zielkorridor lautet: spürbar unter den vor dieser Änderung gemessenen **+64 175 bis +80 088**, aber **deutlich positiv** — Expansion soll sich weiter lohnen, nur mit sinkendem Grenznutzen.

**Zwei Wirkungen überlagern sich hier**, und sie müssen getrennt beurteilt werden: die Verdrahtung aus Task 6 (der Warenkreislauf läuft erstmals überhaupt) und die neuen Formeln aus Task 1–4. Der in Task 6, Schritt 5 gemessene Wert ist die Bezugsgröße für die Formeln — nicht der alte Wert von +80 088, der aus einem Spiel ohne funktionierenden Vorrat stammt.

**Messmethodik:** `--ohne-aktionen --spieler=1` isoliert den Handel von den zufälligen Kontor-Aktionen, deren Streuung mehrere Zehntausend Taler beträgt und den Effekt sonst überdeckt. **Nicht** `--ohne-bereiche` verwenden — das überspringt den Heimatstadtbesuch und meldet null Handel.

- [ ] **Step 1: Referenzlauf über drei Seeds**

Aus `C:\Projekte\Godot\Conspiratio.Godot`:

```bash
for seed in 4711 1234 999; do "/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=15 --spieler=1 --seed=$seed --ohne-aktionen > /tmp/kalib_$seed.txt 2>&1; echo "--- seed $seed (exit $?) ---"; grep -E "Gespielte Jahre|Verkaufte Waren|Spieler 1:|E2E-Durchlauf" /tmp/kalib_$seed.txt; done
```

Vergleichswerte:
- **Vor dem gesamten Vorhaben** (kein Vorratskreislauf, feste Preise): Seed 4711 = +80 088, Seed 1234 = +64 175.
- **Nach Task 6** (Kreislauf läuft, Formeln aktiv): der in Task 6, Schritt 5 für Seed 4711 notierte Wert.

- [ ] **Step 2: Ergebnis bewerten und Parameter entscheiden**

Bewertungsregel, in dieser Reihenfolge:
- Endvermögen **negativ oder nahe null** → zu hart. Zuerst `Stadt.MaxAbschlagProzent` auf 40 senken; reicht das nicht, `HandelsManager.SteigerungProzent` auf 115 senken.
- Endvermögen **weiterhin über ~50 000** → zu lasch. `Stadt.AbschlagJeBedarfsjahrProzent` auf 15 anheben.
- Endvermögen **spürbar niedriger, aber klar positiv** → Werte bleiben.
- Sinkt die Einwohnerzahl über 15 Jahre im Mittel trotzdem → `DynamischeSpieldaten.GrundwachstumPromille` auf 13 anheben. Das Wachstum soll die Katastrophen ausgleichen, nicht überkompensieren.
- Erreicht die Heimatstadt schon nach wenigen Jahren `GetMaxReichtum()` → `HandelsvolumenJeReichtumsChance` auf 200 anheben (halbiert die Chance). Bleibt der Reichtum über 15 Jahre unverändert, den Wert auf 60 senken.

`MaxSteigerungsstufen` dabei **nicht** anheben — Überlaufschutz, kein Regler.

Bei jeder Änderung: Konstante anpassen, Task-5-Schritte 3–5 (bauen, Cache löschen, restore) wiederholen, dann erneut messen. Werden Konstanten geändert, ziehen die Erwartungswerte in `HandelsbalancingTests.cs` und `BevoelkerungTests.cs` mit — `dotnet test` im Lib-Repo laufen lassen.

- [ ] **Step 3: Vollen E2E-Lauf zur Regressionssicherung**

```bash
"/c/Program Files (x86)/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe" --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=15 --spieler=2 --seed=4711 --verbose > /tmp/voll.txt 2>&1; echo "exit=$?"; grep -E "Gespielte Jahre|E2E-Durchlauf" /tmp/voll.txt
```

Erwartet: exit 0, „E2E-Durchlauf bestanden", 15 von 15 Jahren. Ein negatives Endvermögen ist hier **kein** Fehlschlag — die Zufallsaktionen dominieren das Ergebnis (dokumentiert in `CLAUDE.md`).

- [ ] **Step 4: `CLAUDE.md` aktualisieren**

Im Abschnitt zur Handelsrunde (Punkt 4, „Automated play-through") sind die Zahlen **+33 311**, **+64 175 bis +80 088** und **+4 979** durch diese Änderung überholt — sie stammen aus einem Spiel, in dem der Warenkreislauf gar nicht lief. Zu ergänzen ist außerdem die neue Mechanik, weil sie das Verhalten des Treibers erklärt:

```markdown
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
```

Die konkreten gemessenen Endvermögen aus Schritt 1 an die Stelle der alten Zahlen setzen.

- [ ] **Step 5: Commit**

Falls Konstanten geändert wurden, zuerst im Lib-Repo:

```bash
git add Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs Conspiratio.Lib/Allgemein/HandelsManager.cs Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs Conspiratio.Lib.Tests/
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
