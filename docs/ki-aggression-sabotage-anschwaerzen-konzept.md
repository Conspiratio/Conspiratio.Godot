# Konzept: Aggressive KI — Sabotage und Anschwärzen gegen Menschen

Stand: 18.08.2026 · Status: **Plan (freigegeben, noch nicht umgesetzt)**

KI-Spieler mit schlechter Beziehung zu einem menschlichen Spieler sollen ihn künftig
aktiv angreifen: mit Saboteuren gegen seinen Besitz, oder indem sie ihn bei anderen
Würdenträgern anschwärzen — verstärkt, wenn die KI Beweise gegen ihn hat. Beide
Mechaniken existieren heute nur in der Richtung Mensch→KI bzw. Mensch→Mensch/KI; dieses
Konzept spiegelt sie in die Gegenrichtung.

Betroffen sind zwei Repos: Spiellogik in **Conspiratio.Lib**, Präsentation/Auslösung in
**Conspiratio.Godot** (dieses Repo). Umsetzung wie üblich Lib zuerst, dann Godot mit
Versionsreferenz im Commit.

## 1. Getroffene Entscheidungen

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Sabotage-Wirkung | **Exakt gespiegelt** zur bestehenden Mensch→KI-Sabotage: gleiche Kosten-/Dauer-/Schadensformel, keine Werkstatt-spezifische Wirkung, reiner Vermögensschaden |
| 2 | Beziehungsabhängigkeit | **Gleitende Wahrscheinlichkeit**, dieselbe Formel wie bei der KI-Beleidigung (`FechtDuellManager.PruefeKiBeleidigtSpieler`) — Beziehung dominiert, Bosheit kleiner Zuschlag |
| 3 | Wirkung eines Beweises beim Anschwärzen | Senkt die **Glaubwürdigkeits-Schwelle** (von 80), erhöht **nicht** den Schaden |
| 4 | Auslöse-Zeitpunkt (Initiierung) | Gleicher Hook wie die KI-Beleidigung: `Kontor.NaechstenSpielerAnkuendigen` (Zugbeginn) |
| 5 | Häufung pro Zug | **Exklusiv**: eine KI löst höchstens eine der drei feindlichen Aktionen (Beleidigung, Sabotage, Anschwärzen) pro Zug gegen denselben Menschen aus |
| 6 | Adressat beim KI-Anschwärzen | Die KI mit der **besten Beziehung zum Ankläger** (ausgenommen Ankläger und Opfer selbst) |
| 7 | Geltungsbereich der Beweis-Schwelle | **Einheitliche Logik** für Mensch- und KI-Ankläger (ein gemeinsamer `AnschwaerzenAusfuehren`), nur die Beweisquelle unterscheidet sich je Täter-Typ |

**Nicht in v1:** Sabotage/Anschwärzen von KI gegen KI, von Mensch gegen Mensch (jenseits
des bestehenden Mensch→KI-Anschwärzens), ein eigener KI-Spionage-Mechanismus.

## 2. Bestehende Mechanik (Referenz, Stand der Analyse)

**Sabotage Mensch→KI** (`DynamischeSpieldaten.Sabotage`, `Conspiratio.Lib/Gameplay/Spielwelt/DynamischeSpieldaten.cs:1707`;
Wirkung `ZugNachrichtenManager.ErmittleSabotageNachrichten`, `Allgemein/ZugNachrichtenManager.cs:406`;
Speicherung `HumSpieler._aktiveSabotagen`, `Gameplay/Personen/HumSpieler.cs:170`, Struktur `AktiveSabotagen`):

- Initiierung: Kosten = `max(1000, 4% × Zielvermögen)`, Dauer 5 Jahre. Bereits aktiv? →
  Dialog zum Zurückpfeifen/Weiterlaufen. Bei verbotenem Gesetz #21 zusätzlich ein
  Gesetzesverstoß-Eintrag beim Täter.
- Laufende Wirkung (pro Jahr, in `ZeigeVerdeckteEreignisse`, `Kontor.cs:797`): Chance
  1/2, gesenkt auf 1/3 bzw. 1/4, wenn das Ziel Privileg 18 bzw. 19 hat (Wachen/Leibgarde).
  Bei Erfolg: Mächtigkeit = `Rnd(1,9)`, Schaden = `Zielvermögen × Mächtigkeit / 100`
  (1–8 %), direkt vom Taler-Vermögen abgezogen. Dauer sinkt um 1, bei 0 entfernt.

**Anschwärzen** (`DynamischeSpieldaten.Anschwaerzen`, `DynamischeSpieldaten.cs:1764`):
zweistufiger UI-Zustand (`_anschwaerzID`): 1. Klick merkt den Anzuschwärzenden X, 2. Klick
wählt den Adressaten Y (muss KI sein). Glaubt Y (`BeziehungZuKI(Ankläger) ≥ 80`), verliert
X 30 Beziehungspunkte bei Y, Y verliert 10 bei sich selbst zum Ankläger. Glaubt Y nicht
und X ist eine KI, berichtet Y es X (X −50 Beziehung zum Ankläger, Y −20). Ist X ein
Mensch und Y glaubt nicht, passiert nichts weiter (bestehende Asymmetrie, wird
unverändert übernommen). Beweise spielen bisher keine Rolle.

**KI-Beleidigung** (`FechtDuellManager.PruefeKiBeleidigtSpieler`, `Allgemein/FechtDuellManager.cs:209`,
aufgerufen in `Kontor.cs:272`): sucht den KI-**Amtsträger** mit der niedrigsten Beziehung
zum aktiven Menschen. `Feindseligkeit = max(0, 50 − Beziehung)`,
`Chance = (Feindseligkeit × 13 + Bosheit × 1) / 100`. Bleibt in diesem Konzept
unverändert — eigene, bereits eingespielte Balance.

## 3. Datenmodell (Lib, savegame-sicher)

Neues Feld auf `HumSpieler` (analog `_aktiveSabotagen`, aber Blickrichtung umgekehrt:
„Sabotagen, die eine KI gegen mich laufen hat"):

```csharp
private AktiveSabotagen[] _gegnerischeSabotagen; // lazy, savegame-sicher (kein Konstruktor-Init)

public AktiveSabotagen GetGegnerischeSabotage(int taeterKiId)
{
    _gegnerischeSabotagen ??= new AktiveSabotagen[SW.Statisch.GetMaxKIID()];
    _gegnerischeSabotagen[taeterKiId] ??= new AktiveSabotagen(0, 0);
    return _gegnerischeSabotagen[taeterKiId];
}

public void GegnerischeSabotageEntfernen(int taeterKiId)
{
    GetGegnerischeSabotage(taeterKiId).SetDauer(0);
    GetGegnerischeSabotage(taeterKiId).SetKosten(0);
}
```

Feld bleibt bei bestehenden Savegames `null` und wird beim ersten Zugriff angelegt — exakt
das Lazy-Init-Muster aus `Spieler.BegingVerbrechenSicher()` bzw. `HumSpieler.GetAhnentafelListe()`.
Für Anschwärzen ist **kein** neues Feld nötig; es hat keine Laufzeit über die Runde hinaus.

## 4. Sabotage (KI → Mensch)

Neuer, gemeinsamer **`AggressionManager`** (Lib, `Allgemein/AggressionManager.cs`) für die
beiden neuen Verhalten (Sabotage- und Anschwärzen-Initiierung), analog zu `FechtDuellManager`.

**Initiierung** `PruefeKiSabotiertSpieler()`:
- Iteriert **alle** KIs (nicht nur Amtsträger — Sabotage braucht kein Amt).
- Gleiche Feindseligkeits-/Chance-Formel wie `PruefeKiBeleidigtSpieler` (Konstanten dorthin
  extrahieren oder duplizieren — Entscheidung bei der Implementierung, siehe Abschnitt 6
  zur Exklusivität, die beide Prüfungen verzahnt).
- Bei Erfolg: Dauer exakt wie die bestehende Mensch→KI-Sabotage (5 Jahre). Setzt
  `mensch.GetGegnerischeSabotage(kiId)`.
- **Abweichung von der Spiegelung:** Der Kosten-Wert (`4% × Zielvermögen`) ist bei der
  bestehenden Sabotage eine **jährlich wiederkehrende** Belastung, die `AbrechnungsManager`
  ausschließlich für den gerade aktiven Menschen abrechnet (`AbrechnungsManager.cs:95`).
  Eine gleichwertige Buchhaltung für KI-Finanzen existiert nicht und für einen Effekt, den
  der Mensch nie zu Gesicht bekommt, lohnt sich eine parallele Pipeline nicht (YAGNI). Die
  KI zahlt daher **keine** laufenden Kosten; `AktiveSabotagen.SetKosten` bleibt bei der
  gegnerischen Sabotage auf 0, nur `SetDauer` wird gesetzt.
- Bereits eine laufende gegnerische Sabotage derselben KI gegen denselben Menschen? Dann
  einfach keine zweite auslösen (anders als beim bestehenden Mechanismus gibt es hier
  keine Rückpfeif-Interaktion, da die KI nicht mit dem Menschen verhandelt — sie läuft
  bis zum Ablauf der Dauer einfach weiter).
- **Keine Meldung bei der Initiierung.** Sabotage ist covert — der Mensch erfährt erst
  etwas, wenn eine Wirkung tatsächlich zuschlägt (siehe unten). Löst in einem 5-Jahres-
  Fenster kein einziger Jahreswurf aus, bemerkt er die Sabotage nie — spiegelt die
  Unsicherheit des Originals, bei dem der Erfolg pro Jahr ebenfalls nur eine Chance ist.

**Laufende Wirkung** `ZugNachrichtenManager.ErmittleGegnerischeSabotageNachrichten()`,
aufgerufen in `ZeigeVerdeckteEreignisse` neben der bestehenden Sabotage-Meldung:
- Für jede KI mit `mensch.GetGegnerischeSabotage(kiId).GetDauer() > 0`: identische
  Chance-/Schadensformel wie `ErmittleSabotageNachrichten` (Chance 1/2, gesenkt auf 1/3
  bzw. 1/4 durch **des Menschen** Privileg 18/19; Schaden 1–8 % von **seinem** Vermögen).
- Schaden wird vom Taler-Vermögen des Menschen abgezogen, Dauer sinkt, bei 0 wird
  `GegnerischeSabotageEntfernen` aufgerufen.
- Meldungstext parallel zur bestehenden Formulierung, aus Sicht des Opfers: „Es gelang
  [KI-Name]s Saboteuren, Euren Besitztümern [Stärke]-Schäden in Höhe von X anzurichten."

## 5. Anschwärzen (Entkopplung + Beweisgewichtung + KI-Initiierung)

**Entkopplung.** Die Kernlogik wandert in eine UI-freie Methode, die ihre Meldung als
Rückgabewert statt über `BelTextAnzeigen` liefert (damit sowohl der bestehende
Mensch-Flow als auch der neue KI-Flow sie weiterverwenden können):

```csharp
// DynamischeSpieldaten.cs
public string AnschwaerzenAusfuehren(int taeterId, int x, int y)
```

Sie enthält exakt die heutige Logik aus `Anschwaerzen(int id)` ab dem zweiten Schritt
(Gesetzesverstoß, Glaubwürdigkeits-Check, Beziehungsänderungen), aber mit `taeterId` als
Parameter statt `GetAktiverSpieler()`, und gibt den Meldungstext zurück statt ihn über
`BelTextAnzeigen` auszugeben. Die bestehende `Anschwaerzen(int id)` wird zum dünnen
Wrapper: hält weiterhin `_anschwaerzID` und den Selbst-Check/Human-Adressat-Check (die
UI-Validierung, die nichts mit dem Täter zu tun hat) und ruft im zweiten Schritt
`BelTextAnzeigen(AnschwaerzenAusfuehren(GetAktiverSpieler(), _anschwaerzID, id))`.

**Beweisgewichtung.** Die Glaubwürdigkeits-Schwelle (bisher fix 80) wird:

```
schwelle = 80 - min(Beweispunkte(taeterId, x) * 3, 30)   // Boden: 50
```

`Beweispunkte(taeterId, x)` unterscheidet sich je Täter-Typ, weil dafür schon zwei
passende, aber unterschiedliche Werte existieren:

- **Mensch als Täter** (bestehender Fall): `GetHumWithID(taeterId).GetAktiveSpionage(x).GetDelikte()`
  — was der Mensch tatsächlich über X herausspioniert hat (bestehender Spionage-Mechanismus,
  vgl. `AktiveSpionagen.GetDelikte()`, verwendet auch bei der Erpressung).
- **KI als Täter** (neu): `GetSpWithID(x).GetDeliktpunkte()` — die Ground-Truth-Sündenpunkte
  des Opfers (`Spieler.GetDeliktpunkte()`, für Menschen aus `DeliktpunkteBerechnen()`
  gewichtet aus tatsächlich begangenen Gesetzesverstößen berechnet). Die KI „weiß" das
  einfach, ohne eigene Spionage-Struktur — vermeidet, für Issue eine komplette
  KI-Spionage-Infrastruktur zu bauen, die niemand sonst braucht.

Dieser Unterschied in der Beweisquelle ist beabsichtigt; die **Schwellenformel** selbst
ist für beide Täter-Typen identisch (das war die „einheitliche Logik" aus Entscheidung 7).

**Wichtig für die Regression:** Bei `Beweispunkte == 0` ist `schwelle == 80` — exakt das
heutige Verhalten. Der bestehende Mensch→KI-Pfad bekommt die Beweisgewichtung also
automatisch mit (bisher spioniert kaum jemand vor dem Anschwärzen, daher meist unverändert),
ändert aber nichts an bestehenden Spielständen oder deren Erwartungshaltung, wenn keine
Spionage vorliegt.

**KI-Initiierung** `AggressionManager.PruefeKiSchwaerztSpielerAn()`:
- X = aktiver Mensch. Ankläger-KI nach derselben Feindseligkeits-/Chance-Formel wie bei
  der Sabotage-Initiierung (alle KIs, kein Amt nötig).
- Adressat Y: die KI mit der besten Beziehung zum Ankläger, ausgenommen Ankläger und X
  selbst. Gibt es außer dem Ankläger keine weitere KI (Minimalspiel), entfällt die Aktion.
- Ruft `SW.Dynamisch.AnschwaerzenAusfuehren(anklaegerId, x, y)` und behält den
  zurückgegebenen Meldungstext.

**Meldung — sofort, nicht verzögert.** Anders als Sabotage ist Anschwärzen ein
Einmal-Ereignis mit sofortigem Ergebnis (glaubt Y oder nicht), es gibt keinen späteren
Moment, in dem sich das noch entscheiden würde. Die Meldung erscheint deshalb **im selben
Zugbeginn-Block wie die Initiierung** (Abschnitt 7), nicht in `ZeigeVerdeckteEreignisse` —
dort landet nur der Sabotage-Wirkungs-Block.

## 6. Exklusivität: gemeinsamer Feindseligkeits-Dispatcher

`PruefeKiBeleidigtSpieler` bleibt unverändert (eigene, bereits gemessene Balance,
beschränkt auf Amtsträger). Eine neue Methode mit folgender Signatur ersetzt den in
Abschnitt 7 zuvor skizzierten Einzel-Int-Rückgabewert, weil hier — anders als bei der
Beleidigung — mehrere KIs unabhängig voneinander im selben Zug feuern können:

```csharp
public enum AggressionsAktion { Sabotage, Anschwaerzen }

public class AggressionsErgebnis
{
    public int TaeterId { get; init; }
    public AggressionsAktion Aktion { get; init; }
    /// <summary>Nur bei Aktion == Anschwaerzen befüllt; das Ergebnis von AnschwaerzenAusfuehren.</summary>
    public string Meldung { get; init; }
}

// AggressionManager.cs
public List<AggressionsErgebnis> PruefeKiAggression(int beleidigerId)
```

- Für jede KI außer `beleidigerId`: einmal würfeln (gleiche Feindseligkeits-Formel wie
  `PruefeKiBeleidigtSpieler`).
- Bei Erfolg: Wahl zwischen Sabotage und Anschwärzen — 50/50, außer die KI hat gegen
  diesen Menschen bereits eine laufende Sabotage (dann Anschwärzen bevorzugt, um nicht
  wirkungslos eine zweite Sabotage gegen dasselbe Ziel zu prüfen). Bei Sabotage wird
  `Meldung` nicht gesetzt (covert, siehe Abschnitt 4); bei Anschwärzen enthält es den
  Rückgabewert von `AnschwaerzenAusfuehren`.
- Jede KI führt höchstens eine Aktion pro Zug gegen denselben Menschen aus; verschiedene
  KIs können im selben Zug unabhängig voneinander verschiedene Aktionen gegen denselben
  Menschen auslösen (keine globale Ein-Aktion-pro-Zug-Grenze).

## 7. Godot-Integration

`Kontor.cs`, `NaechstenSpielerAnkuendigen`, direkt nach dem bestehenden
KI-Beleidigungs-Block (`Kontor.cs:267–295`):

```csharp
var aggression = new AggressionManager();

foreach (var ergebnis in aggression.PruefeKiAggression(beleidiger))
{
    if (ergebnis.Aktion == AggressionsAktion.Anschwaerzen)
    {
        UpdateHud();
        await _main.RundenNachrichtenDialog.ShowDialog("Intrigen\n\n" + ergebnis.Meldung);
    }
    // Aktion == Sabotage: bewusst keine Meldung, siehe Abschnitt 4 (covert)
}
```

`ZeigeVerdeckteEreignisse` (`Kontor.cs:767`): zwei neue Meldungsblöcke „Sabotage gegen Euch"
und „Intrigen", eingefügt neben den bestehenden Spionage-/Sabotage-Blöcken, gleiches Muster
(`if (meldung != null) await _main.RundenNachrichtenDialog.ShowDialog(...)`).

## 8. Tests (Conspiratio.Lib.Tests)

- Sabotage-Initiierung: Kosten-/Dauerformel, Feindseligkeits-Chance über große Stichprobe
  (wie bei den Duellen: Rate über mehrere Tausend Läufe statt Einzelfall).
- Sabotage-Wirkung: Schadensformel, Privileg-18/19-Senkung der Chance, Dauer-Countdown und
  Entfernen bei 0.
- Anschwärzen-Refactor: Regressionstest, der `Anschwaerzen(id)` vor und nach dem Umbau bei
  `Beweispunkte == 0` über den ganzen Eingaberaum vergleicht (Muster aus den
  `PrivilegienAktualisieren`-Regressionstests).
- Beweis-Schwellenformel: Grenzwerte (0, 10, ≥10 Beweispunkte → Schwelle 80/50/50).
- KI-Initiierung Sabotage/Anschwärzen: Verteilung über Stichprobe, Exklusivität mit
  `PruefeKiBeleidigtSpieler`.

## 9. Versionierung / Rollout

Lib-`CHANGELOG.md`, ein Eintrag unter `[Unreleased]` (DE+EN), `<Version>` im csproj
bumpen. Godot-Commit danach mit Versionsreferenz im Subject (`… (Conspiratio.Lib x.y.z)`),
`CHANGELOG.md` (DE+EN) auf dieser Seite.

## 10. Offene Punkte für die Umsetzung

- Exakte Konstanten der Beweis-Schwellenformel (`× 3`, Boden 50) sind ein erster Vorschlag,
  keine gemessene Balance — ggf. nach ersten Testläufen (z. B. E2E-Langlauf) nachjustieren.
- Ob die Feindseligkeits-Formel-Konstanten aus `FechtDuellManager` extrahiert oder für
  `AggressionManager` dupliziert werden, ist eine Implementierungsdetail-Entscheidung ohne
  Verhaltensunterschied.
