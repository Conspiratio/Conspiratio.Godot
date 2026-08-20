# Konzept: Eine einzige Einstellung für die Aggressivität der KI-Spieler

Stand: 20.08.2026 · Status: **Plan (freigegeben, noch nicht umgesetzt)**

Es soll genau **eine** Einstellung geben, die die Aggressivität der KI-Spieler steuert:
ein Regler von 0 bis 100 %, der **alle** Bereiche betrifft, in denen KI-Spieler dem
Menschen gegenüber feindselig oder aktiv werden — Militärstützpunkt-Ereignisse, Überfälle,
Gerichtsverhandlungen, Sabotage, Anschwärzen, Anklagen, Beleidigungen, Duelle.

Heute ist die Lage zersplittert und teilweise tot:

| Was | Wo | Zustand heute |
|---|---|---|
| `Spieleinstellungen.AggressivitaetKISpieler` (`EnumSchwierigkeitsgrad`: Niedrig/Mittel/Hoch) | Lib | **Tot**: kein UI-Element setzt es je, läuft immer auf `Mittel`. Beeinflusst nur Gerichtsurteile, Amtsenthebungs-Schwelle, Anklage-Häufigkeit. |
| `Spieleinstellungen.KiAktivitaetProzent` (1–100) | Lib | Lebt, per Options-Regler gesetzt — beeinflusst aber **nur** Räuberlager-/Zollburg-Aktivität. |
| `KISpieler.Bosheit` (`_boese`, 0–100) | Lib | Rein zufällig je KI (`Rnd.Next(0, 101)`), **von keiner Einstellung beeinflussbar**. Treibt 8 Mechaniken (siehe Abschnitt 3). |

Der XML-Kommentar von `AggressivitaetKISpieler` beschreibt bereits ausdrücklich auch die
Militärstützpunkt-Häufigkeit — die beiden Einstellungen waren also von Anfang an als eine
gedacht und sind nur nie zusammengeführt worden. Genau das holt dieses Konzept nach.

Betroffen sind beide Repos: Spiellogik in **Conspiratio.Lib**, Regler und Beschriftung in
**Conspiratio.Godot**. Umsetzung wie üblich Lib zuerst, dann Godot mit Versionsreferenz im
Commit.

## 1. Getroffene Entscheidungen

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Wie viele Einstellungen am Ende? | **Genau eine**, 0–100 %, für sämtliche KI-Aggressivität |
| 2 | Streuung zwischen einzelnen KIs | **Erhalten**: die Einstellung verschiebt den Mittelwert der Bosheit-Verteilung, jede KI würfelt weiterhin individuell darum herum |
| 3 | Wirkung auf die bisher enum-gesteuerten Mechaniken | **Stufenlos interpolieren** zwischen den bisherigen Niedrig-/Mittel-/Hoch-Werten, statt intern wieder in Stufen einzuteilen |
| 4 | Verhalten bei 50 % | **Bit-identisch zum heutigen Normalfall** an jeder einzelnen Stelle — 50 % ist der Regressionsanker des ganzen Vorhabens |
| 5 | Bestehender Regler | **Umgewidmet**, kein zweiter Regler daneben (Anforderung „nur eine einzige Einstellung") |

**Nicht in diesem Vorhaben:** ein Schwierigkeitsgrad, der auch nicht-aggressive Aspekte
betrifft (KI-Wirtschaftskraft, Startvermögen, Preisverhalten); eine Aggressivität pro
KI-Spieler statt global; ein nachträgliches Ändern im laufenden Spiel (der Wert wird wie
heute bei der Spielerstellung in den Spielstand übernommen).

## 2. Datenmodell

`Spieleinstellungen.KiAktivitaetProzent` wird zur alleinigen Einstellung und heißt künftig
**`KiAggressivitaetProzent`** — „Aktivität" trifft die neue, breitere Bedeutung nicht mehr.
Typ, Wertebereich und der bereits vorhandene Alt-Spielstand-Fallback (`<= 0` wird wie 50 %
behandelt) bleiben unverändert.

```csharp
/// <summary>
/// Aggressivität der KI-Spieler als Prozentwert (1–100, Standard 50). Steuert sämtliche
/// feindseligen und militärischen KI-Aktivitäten: Bosheit der KI-Charaktere (und damit
/// Beleidigungen, Duelle, Sabotage, Anschwärzen, KI-Verbrechen), Anklagen, Gerichtsurteile,
/// Amtsenthebungen sowie Ausbau und Aktionen der Militärstützpunkte. 50 % entspricht dem
/// bisherigen Normalwert; alte Spielstände (Wert 0) werden wie 50 % behandelt.
/// </summary>
public int KiAggressivitaetProzent { get; set; } = 50;
```

`AggressivitaetKISpieler` **entfällt ersatzlos**, ebenso die dann nirgends mehr
referenzierte Datei `EnumSchwierigkeitsgrad.cs`. Es gibt nichts zu migrieren: Da nie ein
UI-Element das Feld gesetzt hat, steht es in *jedem* existierenden Spielstand auf `Mittel`
— und `Mittel` ist genau der Punkt, den die neue 50-%-Vorgabe reproduziert.

**Serialisierung / alte Spielstände.** Die Umbenennung von `KiAktivitaetProzent` ist ein
Feldwechsel im field-basierten JSON: Ein alter Spielstand bringt `KiAktivitaetProzent` mit,
das neue Feld kommt daher als Standardwert an. Da der Standard ohnehin 50 ist und der
`<= 0`-Fallback ebenfalls auf 50 führt, landet ein Altstand in beiden Fällen beim
bisherigen Normalverhalten. Ein Spieler, der den Regler vor dem Update verstellt hatte,
verliert diese Wahl für laufende Spielstände — vertretbar, weil die Einstellung ihre
Bedeutung ohnehin erweitert und der Client-seitige Wert (`ClientSettings`, siehe
Abschnitt 6) für neue Spiele erhalten bleibt.

## 3. Bosheit — ein Eingriff statt acht

`KISpieler.Bosheit` (`_boese`) treibt heute acht Mechaniken:

| # | Mechanik | Fundstelle |
|---|---|---|
| 1 | KI beleidigt den Spieler (Chance) | `FechtDuellManager.BerechneKiFeindseligkeitChance` (über `PruefeKiBeleidigtSpieler`) |
| 2 | Beleidigte KI verlangt Satisfaktion (Duell) | `FechtDuellManager.cs:177` |
| 3 | Gegnerstärke im gewürfelten Duell | `FechtDuellManager.cs:279` |
| 4 | Gegnerstärke im interaktiven Wortgefecht | `WortgefechtManager.cs:169` |
| 5 | Sabotage / Anschwärzen gegen den Menschen | `AggressionManager.cs:63` (über dieselbe Feindseligkeits-Formel) |
| 6 | Häufigkeit zufälliger KI-Straftaten | `RundenEndeManager.cs:104` |
| 7 | Anfängliche Deliktpunkte einer KI | `DynamischeSpieldaten.cs:186` |
| 8 | Jährliche Deliktpunkte-Drift | `DynamischeSpieldaten.cs:2244` |

Statt an acht Stellen einzeln zu skalieren — acht Gelegenheiten für Balance-Fehler und
Inkonsistenzen — greift die Einstellung **einmal** dort an, wo Bosheit entsteht: bei der
Erzeugung eines KI-Spielers. Alle acht Verbraucher ändern sich dadurch automatisch mit,
ohne dass ihr Code angefasst wird.

Zwei Erzeugungsstellen, beide `Rnd.Next(0, 101)` als vierter Konstruktorparameter:

- `DynamischeSpieldaten.cs:175` — Aufbau der Spielwelt
- `DynamischeSpieldaten.cs:427` — Ersatz-KI, wenn eine KI stirbt oder ausscheidet

Beide rufen künftig einen gemeinsamen Helfer:

```csharp
/// <summary>
/// Würfelt die Bosheit einer neuen KI aus und verschiebt sie um die eingestellte
/// KI-Aggressivität: Bei 50 % (Standard) bleibt die Verteilung unverändert bei 0–100,
/// bei 100 % liegt sie bei 50–100, bei 0 % bei 0–50. Die Streuung zwischen einzelnen
/// KIs bleibt dabei erhalten – die Einstellung verschiebt den Mittelwert, sie
/// vereinheitlicht die Charaktere nicht.
/// </summary>
public int WuerfleBosheit()
{
    int aggressivitaet = GetKiAggressivitaetProzent();  // mit <= 0 -> 50 Fallback
    return Math.Max(0, Math.Min(100, SW.Statisch.Rnd.Next(0, 101) + (aggressivitaet - 50)));
}
```

Bei 50 % ist der Summand exakt 0 — die Bosheit wird bit-identisch wie heute gewürfelt.

**Bewusste Nebenwirkung:** Über Verbraucher 3 und 4 beeinflusst die Einstellung nicht nur,
*wie oft* die KI angreift, sondern auch, *wie stark* sie im Duell ist. Das ist gewollt:
„aggressivere KI" heißt dann auch „gefährlichere KI", und beides aus einer Quelle zu
speisen hält die Einstellung verständlich.

## 4. Die drei bisher enum-gesteuerten Mechaniken

Alle drei ersetzen ihren `switch` über `EnumSchwierigkeitsgrad` durch eine stückweise
lineare Interpolation über die Stützpunkte 0 % / 50 % / 100 %, die exakt die bisherigen
Niedrig-/Mittel-/Hoch-Werte übernimmt:

| Mechanik | Fundstelle | Niedrig (0 %) | Mittel (50 %) | Hoch (100 %) |
|---|---|---|---|---|
| Urteilsneigung des KI-Richters (`faktor`-Zuschlag) | `GerichtsverhandlungManager.cs:577-588` | −2 | +5 | +10 |
| Schwelle, ab der eine KI die Absetzung Untergebener fordert (`maxAbsetzSympathie`-Zuschlag) | `DynamischeSpieldaten.cs:2280-2291` | −2 | +10 | +20 |
| Anklage-Häufigkeit gegen den Menschen (`faktor`) | `DynamischeSpieldaten.cs:2861-2872` | 2 | 5 | 12 |

Dafür ein gemeinsamer Helfer, damit die Interpolation nur einmal existiert:

```csharp
/// <summary>
/// Interpoliert einen Wert stufenlos anhand der eingestellten KI-Aggressivität zwischen
/// drei Stützpunkten: <paramref name="beiNull"/> bei 0 %, <paramref name="beiFuenfzig"/>
/// bei 50 % und <paramref name="beiHundert"/> bei 100 %. Die Stützpunkte entsprechen den
/// früheren Stufen Niedrig/Mittel/Hoch, sodass 50 % das bisherige Verhalten reproduziert.
/// </summary>
public int InterpoliereNachAggressivitaet(int beiNull, int beiFuenfzig, int beiHundert)
{
    int prozent = GetKiAggressivitaetProzent();

    return prozent <= 50
        ? beiNull + (beiFuenfzig - beiNull) * prozent / 50
        : beiFuenfzig + (beiHundert - beiFuenfzig) * (prozent - 50) / 50;
}
```

Bei 50 % liefert der Helfer exakt `beiFuenfzig`, also den heutigen `Mittel`-Wert.

**Wo die Helfer wohnen.** Beide (`WuerfleBosheit`, `InterpoliereNachAggressivitaet`) plus
der private `GetKiAggressivitaetProzent()` mit dem `<= 0`-Fallback gehören zu
`DynamischeSpieldaten` — dort liegen die Spieleinstellungen, dort sitzen drei der fünf
Aufrufer, und ein eigener Manager für drei kurze Methoden wäre Überbau (YAGNI).

## 5. Militärstützpunkte (Räuberlager, Zollburg)

`Raeuberlager.cs:70` und `Zollburg.cs:91` lesen künftig `KiAggressivitaetProzent` statt
`KiAktivitaetProzent` — sinnvollerweise über denselben `GetKiAggressivitaetProzent()`-Helfer,
womit ihr lokal duplizierter `<= 0`-Fallback entfällt. Die Faktor-Logik selbst
(50 % = Faktor 1.0, 100 % = Faktor 2.0) bleibt unverändert; hier ändert sich nur der
Feldname und die Herkunft des Fallbacks.

## 6. Godot-Client

- `ClientSettings.KiAktivitaetProzent` → `KiAggressivitaetProzent`, Config-Schlüssel
  `ki_aktivitaet_prozent` → `ki_aggressivitaet_prozent` (`ClientSettings.cs:91-96`).
  Ein Spieler, der den alten Regler verstellt hatte, startet nach dem Update wieder bei
  50 % — akzeptabel für eine Einstellung, deren Bedeutung sich ohnehin erweitert.
- `OptionenDialog.cs`: Feld- und Methodennamen (`_sliderKiAktivitaet`,
  `OnKiAktivitaetGeaendert`, `AktualisiereKiAktivitaetLabel`) auf „Aggressivitaet"
  umbenennen; Label-Text (`:158`) von „Aktivität der KI-Spieler" auf **„Aggressivität der
  KI-Spieler"**. Der Node-Name in `OptionenDialog.tscn` (`Rahmen/SliderKiAktivitaet`) wird
  mit umbenannt, samt der `GetNode`-Pfade — sonst driften Szene und Skript auseinander.
- `NewLocalGameMenu.cs:119` übernimmt den Wert wie bisher in den Spielstand, nur unter dem
  neuen Namen.

Kein neuer Regler, kein zweites Bedienelement: der bestehende Slider wird umgewidmet.

## 7. Tests (Conspiratio.Lib.Tests)

Der Regressionsanker ist überall derselbe: **bei 50 % muss sich nichts ändern.**

- `WuerfleBosheit`: bei 50 % identische Verteilung wie `Rnd.Next(0, 101)` bei gleichem Seed;
  bei 0 % und 100 % Mittelwert-Verschiebung über Großstichprobe (einige Tausend Würfe),
  Grenzen sauber bei 0 bzw. 100 gekappt; Streuung bleibt erhalten (Standardabweichung bzw.
  Spannweite bei 100 % deutlich > 0, nicht alle KIs identisch).
- `InterpoliereNachAggressivitaet`: exakte Stützpunkte bei 0/50/100 %, Monotonie und
  plausible Zwischenwerte bei 25 % und 75 %, für alle drei realen Wertetripel.
- Je einen Test für die drei umgestellten Mechaniken, der bei 50 % den heutigen Wert
  festnagelt (Urteils-`faktor`, `maxAbsetzSympathie`, Anklage-`faktor`).
- Räuberlager/Zollburg: unveränderter Faktor bei 50 %, verdoppelt bei 100 %.
- Alte Spielstände: `KiAggressivitaetProzent == 0` verhält sich exakt wie 50 %.

## 8. Versionierung / Rollout

Lib-`CHANGELOG.md`, ein Eintrag unter `[Unreleased]` (DE+EN) mit ausdrücklichem Hinweis auf
die entfallene `AggressivitaetKISpieler`-Einstellung und die Umbenennung; `<Version>` im
csproj bumpen. Godot-Commit danach mit Versionsreferenz im Subject, dazu `CHANGELOG.md`
(DE+EN) auf dieser Seite.

## 9. Offene Punkte für die Umsetzung

- Die Bosheit-Verschiebung ist bewusst linear (`+ (prozent - 50)`). Ob sich 100 % im Spiel
  tatsächlich „doppelt so aggressiv" anfühlt oder eher überzogen, lässt sich erst am
  laufenden Spiel beurteilen — über einen E2E-Langlauf messbar und über die eine Formel
  leicht nachjustierbar.
- Die Umbenennung `KiAktivitaetProzent` → `KiAggressivitaetProzent` kostet laufende
  Spielstände die individuelle Einstellung (Rückfall auf 50 %). Alternativ ließe sich das
  alte Feld als veralteter Alias mitlesen; angesichts des Vorabversions-Status des Clients
  ist der einfache Schnitt vorzuziehen — falls das anders gesehen wird, ist es der eine
  Punkt, an dem dieses Konzept nachzuschärfen wäre.
