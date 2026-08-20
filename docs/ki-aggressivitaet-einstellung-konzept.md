# Konzept: Eine einzige Einstellung für die Aggressivität der KI-Spieler

Stand: 20.08.2026 · Status: **Plan (freigegeben, noch nicht umgesetzt)**

Es soll genau **eine** Einstellung geben, die die Aggressivität der KI-Spieler steuert:
ein Regler von 0 bis 100 %, der **alle** Bereiche betrifft, in denen KI-Spieler dem
Menschen gegenüber feindselig oder aktiv werden — Militärstützpunkt-Ereignisse, Überfälle,
Gerichtsverhandlungen, Sabotage, Anschwärzen, Anklagen, Beleidigungen, Duelle.

Heute ist die Lage zersplittert und teilweise tot:

| Was | Wo | Zustand heute |
|---|---|---|
| `Spieleinstellungen.AggressivitaetKISpieler` (`EnumSchwierigkeitsgrad`: Niedrig/Mittel/Hoch) | Lib | **Im Godot-Client tot**: kein dortiges UI-Element setzt es je, läuft im Godot-Client immer auf `Mittel`. Der eingefrorene Legacy-WinForms-Client setzt und liest es dagegen weiterhin (`frmEinstellungen.cs:125,133,141`, `Main.cs:6067`) — die Löschung dieses Felds (Abschnitt 2) lässt jenen Client folglich nicht mehr gegen diese Lib-Version kompilieren; hingenommen, da er laut Projektkonvention nur noch Referenzimplementierung ist. Beeinflusst nur Gerichtsurteile, Amtsenthebungs-Schwelle, Anklage-Häufigkeit. |
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
| 2 | Streuung zwischen einzelnen KIs | **Erhalten**: die Einstellung verschiebt alle Bosheitswerte gemeinsam, der individuelle Charakterwurf jeder KI bleibt erhalten — es gibt also weiterhin mildere und bösartigere KIs |
| 3 | Wirkung auf die bisher enum-gesteuerten Mechaniken | **Stufenlos interpolieren** zwischen den bisherigen Niedrig-/Mittel-/Hoch-Werten, statt intern wieder in Stufen einzuteilen |
| 4 | Verhalten bei 50 % | **Bit-identisch zum heutigen Normalfall** an jeder einzelnen Stelle — 50 % ist der Regressionsanker des ganzen Vorhabens |
| 5 | Bestehender Regler | **Umgewidmet**, kein zweiter Regler daneben (Anforderung „nur eine einzige Einstellung") |
| 6 | Wirkzeitpunkt | **Sofort, auch im laufenden Spiel** — inklusive bereits existierender KI-Spieler und geladener alter Spielstände |

**Nicht in diesem Vorhaben:** ein Schwierigkeitsgrad, der auch nicht-aggressive Aspekte
betrifft (KI-Wirtschaftskraft, Startvermögen, Preisverhalten); eine Aggressivität pro
KI-Spieler statt global.

Entscheidung 6 ist der Grund für den Zuschnitt von Abschnitt 3. Heute wirkt der Regler
**gar nicht** auf ein laufendes Spiel: Er schreibt nur nach `ClientSettings`, und in den
Spielstand — den die Spiellogik liest — wird der Wert ausschließlich beim Anlegen eines
neuen Spiels kopiert (`NewLocalGameMenu.cs:119`). Ein Spieler, der ihn mitten im Spiel
verstellt, sieht also keinerlei Wirkung, obwohl der Regler bedienbar aussieht. Dieses
Konzept behebt das mit.

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
referenzierte Datei `EnumSchwierigkeitsgrad.cs`. Für den Godot-Client gibt es nichts zu
migrieren: Da dort nie ein UI-Element das Feld gesetzt hat, steht es in *jedem* mit diesem
Client erzeugten Spielstand auf `Mittel` — und `Mittel` ist genau der Punkt, den die neue
50-%-Vorgabe reproduziert. Für Spielstände aus dem Legacy-WinForms-Client gilt das nicht:
Dessen `frmEinstellungen` setzt das Feld tatsächlich auf `Niedrig`/`Mittel`/`Hoch`, sodass
ein von dort mitgebrachter Spielstand nach der Löschung dieses Felds seine individuelle
Einstellung verliert und wie `Mittel` (= 50 %) behandelt wird. Hingenommen, da WinForms-
Savegame-Kompatibilität für den Godot-Client ohnehin keine Priorität ist.

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
Inkonsistenzen — greift die Einstellung **einmal** an, und zwar dort, wo Bosheit *gelesen*
wird: im Getter selbst. Alle acht Verbraucher ändern sich dadurch automatisch mit, ohne
dass ihr Code angefasst wird, und ein künftiger neunter Verbraucher bekommt die Skalierung
ebenfalls automatisch — er kann sie gar nicht vergessen.

```csharp
// KISpieler.cs
/// <summary>
/// Die wirksame Bosheit dieser KI: ihr ausgewürfelter Charakterwert, verschoben um die
/// eingestellte KI-Aggressivität. Bei 50 % (Standard) ist das exakt der Charakterwert,
/// bei 100 % um 50 Punkte höher, bei 0 % um 50 niedriger – jeweils auf 0–100 begrenzt.
/// Die Streuung zwischen den KIs bleibt erhalten: Die Einstellung verschiebt alle
/// Charaktere gemeinsam, sie gleicht sie nicht an.
/// </summary>
public int GetBosheit()
{
    int aggressivitaet = SW.Dynamisch.GetKiAggressivitaetProzent();  // mit <= 0 -> 50 Fallback
    return Math.Max(0, Math.Min(100, _boese + (aggressivitaet - 50)));
}
```

Der gespeicherte Charakterwert `_boese` bleibt unangetastet — er wird weiterhin bei der
Erzeugung gewürfelt (`Rnd.Next(0, 101)`, `DynamischeSpieldaten.cs:175` und `:427`),
unverändert serialisiert und beim Laden unverändert wiederhergestellt. Die Einstellung
moduliert nur, wie stark sich dieser Charakter auswirkt.

**Warum im Getter und nicht bei der Erzeugung** (der naheliegendere erste Entwurf): Nur so
wirkt die Einstellung *sofort und rückwirkend* — auf bereits existierende KI-Spieler, im
laufenden Spiel und in geladenen alten Spielständen (Entscheidung 6). Würde die
Verschiebung beim Auswürfeln einbacken, wäre die Bosheit jeder KI ab ihrer Erschaffung
eingefroren, und der Regler bliebe für alles Laufende wirkungslos.

Bei 50 % ist der Summand exakt 0 — `GetBosheit()` liefert den Rohwert, also bit-identisches
Verhalten zu heute an allen acht Verbrauchern.

**Kein Rohwert-Bedarf anderswo:** Die Bosheit wird dem Spieler nirgends angezeigt (im
gesamten Godot-Client kommt sie nur in einem Kommentar in `E2eTreiber.cs:273` vor). Die
Serialisierung ist feldbasiert und greift auf `_boese` zu, nicht auf den Getter. Es gibt
also keine Stelle, an der ein modulierter Getter den Rohwert verdecken würde.

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

**Wo die Helfer wohnen.** `InterpoliereNachAggressivitaet` und das öffentliche
`GetKiAggressivitaetProzent()` (mit dem `<= 0`-Fallback) gehören zu `DynamischeSpieldaten` —
dort liegen die Spieleinstellungen, dort sitzen die meisten Aufrufer, und ein eigener
Manager für zwei kurze Methoden wäre Überbau (YAGNI). `GetKiAggressivitaetProzent()` muss
öffentlich sein, weil `KISpieler.GetBosheit()` (Abschnitt 3), `Raeuberlager` und `Zollburg`
(Abschnitt 5) es von außen aufrufen.

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
- **Neu — der Regler wirkt sofort:** `OnKiAktivitaetGeaendert` (künftig
  `OnKiAggressivitaetGeaendert`, `OptionenDialog.cs:145-154`) schreibt den Wert zusätzlich
  nach `SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent`. Damit greifen
  Bosheit (Abschnitt 3), die drei interpolierten Mechaniken (Abschnitt 4) und die
  Stützpunkt-Aktivität (Abschnitt 5) ab dem nächsten Auswerten — ohne Neustart, ohne neues
  Spiel. Der Wert wandert über die normale Spielstand-Serialisierung mit in den Speicherstand.

  **Eine Fallunterscheidung „läuft gerade ein Spiel?" ist nötig.** `Spielstand` ist entgegen
  der ersten Annahme dieses Konzepts sehr wohl `null`, solange kein Spiel läuft: Die
  Zuweisung in `DynamischeSpieldaten.cs:43-44` steht innerhalb von `NeuInitialisieren()`,
  nicht im Property-Getter, und der Aufruf im Konstruktor ist auskommentiert
  (`DynamischeSpieldaten.cs:32-35`). `NeuInitialisieren()` läuft nur über
  `NewGameManager.CreateNewGame` oder beim Laden eines Spielstands — auf dem Weg
  Hauptmenü → Optionen also nie. Der Schreibzugriff wird deshalb mit
  `if (SW.Dynamisch.Spielstand != null)` abgesichert; ohne laufendes Spiel gibt es nichts,
  worauf die Einstellung sofort wirken könnte, und `ClientSettings` liefert den Wert bei der
  Spielerstellung ohnehin. `ClientSettings` bleibt die Vorgabe für neue Spiele, der
  Spielstand-Wert das, was das laufende Spiel tatsächlich verwendet.

Kein neuer Regler, kein zweites Bedienelement: der bestehende Slider wird umgewidmet.

## 7. Tests (Conspiratio.Lib.Tests)

Der Regressionsanker ist überall derselbe: **bei 50 % muss sich nichts ändern.**

- `KISpieler.GetBosheit`: bei 50 % exakt der Rohwert `_boese`; bei 0 % und 100 % um 50
  verschoben, an den Grenzen sauber bei 0 bzw. 100 gekappt; über eine Großstichprobe (einige
  Tausend KIs) verschiebt sich der Mittelwert wie erwartet, während die Streuung erhalten
  bleibt (Spannweite bei 100 % deutlich > 0, nicht alle KIs identisch).
- **Sofortwirkung** (Kern von Entscheidung 6): derselben KI zweimal `GetBosheit()` entlocken,
  dazwischen nur die Einstellung ändern — der zweite Wert muss höher sein. Analog für eine
  der interpolierten Mechaniken. Das ist der Test, der die ursprünglich angedachte
  Erzeugungszeit-Variante hätte auffallen lassen.
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
  leicht nachjustierbar. Der zuletzt umgesetzte Aggressions-Mechanismus (Sabotage/
  Anschwärzen) hat gezeigt, wie weit gefühlte und gemessene Ereignisrate auseinanderliegen
  können: Dort messen, nicht schätzen.
- Die Umbenennung `KiAktivitaetProzent` → `KiAggressivitaetProzent` kostet laufende
  Spielstände die individuelle Einstellung (Rückfall auf 50 %). Alternativ ließe sich das
  alte Feld als veralteter Alias mitlesen; angesichts des Vorabversions-Status des Clients
  ist der einfache Schnitt vorzuziehen — falls das anders gesehen wird, ist es der eine
  Punkt, an dem dieses Konzept nachzuschärfen wäre. Praktisch gemildert wird es dadurch,
  dass der Regler jetzt jederzeit im laufenden Spiel nachgezogen werden kann
  (Entscheidung 6): Ein Spieler stellt seinen Wert einmal neu ein, statt ein Spiel damit
  verloren zu geben.
- `GetBosheit()` liefert künftig einen abgeleiteten statt eines gespeicherten Werts. Das ist
  in diesem Codebestand ungewöhnlich (die meisten Getter geben ihr Feld direkt zurück) und
  wird nur durch den XML-Kommentar kenntlich gemacht. Sollte später doch einmal der reine
  Charakterwert gebraucht werden — etwa für eine Anzeige im Savegame-Editor —, wäre ein
  zusätzliches `GetBosheitRoh()` der saubere Weg, nicht ein Zurückdrehen der Modulation.
