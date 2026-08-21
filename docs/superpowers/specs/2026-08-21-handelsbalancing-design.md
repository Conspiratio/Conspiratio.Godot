# Handelsbalancing: Sättigungspreis und progressiver Werkstatt-Kaufpreis

**Datum:** 2026-08-21
**Betrifft:** `Conspiratio.Lib` (Spiellogik), nachgelagert `Conspiratio.Godot` (E2E-Messung, CHANGELOG)
**Ziel:** Den unbegrenzten Skalierungsvorteil mehrerer Kontore bremsen, ohne Aufbaufreude zu nehmen.

---

## 1. Problem

Erfahrene Spieler verdienen mit mehreren Kontoren beliebig viel Geld. Die Messungen der aktuellen
E2E-Läufe und ein Blick in die Lib zeigen zwei unabhängige Ursachen.

### 1.1 Der Werkstatt-Kaufpreis ist besitzunabhängig

`HandelsManager.GetWerkstattKaufpreis` (`Allgemein/HandelsManager.cs:64`) gibt schlicht
`Rohstoff.GetWSKaufpreis()` zurück — 2 000 Taler Basis, ×5 bzw. ×20 für Stufe 2/3. Der Preis hängt
weder davon ab, wie viele Betriebe der Spieler bereits besitzt, noch von seinem Vermögen.

Bei 14 Städten × 6 Werkstattplätzen sind **84 Betriebe** möglich. Ein voll ausgebauter Betrieb warf in
der Messung ~+3 700 Taler/Jahr ab. Die Amortisation liegt damit **unter einem Jahr**, und der Vorgang ist
84-mal risikofrei wiederholbar. Expansion ist folglich nicht eine Option unter mehreren, sondern strikt
dominant.

### 1.2 Der Mengen-Preisverfall sättigt sofort

Die Angebot-/Nachfrage-Rückkopplung existiert bereits, ist aber wirkungslos.
`Stadt.GetRohstoffPreisVonIDX` (`Gameplay/Gebiete/Stadt.cs:108`):

```csharp
int temp = _rohstoffPreis[X];
temp -= Convert.ToInt32(_rohstoffVorrat[X] / 1000);   // je 1000 Vorrat: −1 Taler
if (temp < GetPreisMin()) temp = GetPreisMin();       // harter Boden
return temp;
```

Der Vorrat wird korrekt gefüttert: `DynamischeSpieldaten.RohBedarfAktRundenEnde` bucht die
Spielerverkäufe in den Stadtvorrat und zieht jährlich `Einwohner / 10` als Bevölkerungsverbrauch ab.

Die Preiswirkung ist jedoch verschwindend. Für Stufe-1-Waren (Korn: `preisMin` 7, `preisStd` 8,
`preisMax` 20) beträgt der maximal mögliche Abschlag **1 Taler = 12,5 %**, und er ist nach ~1 000 Stück
Vorrat erreicht — das produziert ein *einzelner* Betrieb (~2 400 Stück/Jahr) im ersten Jahr. Danach ist
beliebiges weiteres Abladen kostenlos.

Der gemessene E2E-Erlös von exakt 7,0 Talern/Stück (= `preisMin`) bestätigt, dass der Preis dauerhaft am
Boden klebt.

### 1.3 Warum das nur Menschen betrifft

`KISpieler` (178 Zeilen) besitzt weder Werkstätten noch Produktion noch Lager — nur einen abstrakten
`Taler`-Wert. Die Warenwirtschaft ist ein reines Solo-Optimierungsproblem gegen konstante Preise. Beide
Änderungen wirken daher ausschließlich auf menschliche Spieler; eine KI-Nachjustierung entfällt.

---

## 2. Entwurf A: Sättigungsabhängiger Preis

### 2.1 Formel

`Stadt.GetRohstoffPreisVonIDX` misst den Abschlag künftig in **Jahren lokalen Bedarfs** statt in fixen
Talern:

```csharp
int jahresbedarf = Math.Max(1, _einwohner / 10);

// Abschlag in Prozent, linear in der Ueberversorgung, gedeckelt bei MaxAbschlagProzent.
int abschlagProzent = Math.Min(MaxAbschlagProzent,
                               (_rohstoffVorrat[X] * AbschlagJeBedarfsjahrProzent) / jahresbedarf);

return _rohstoffPreis[X] * (100 - abschlagProzent) / 100;
```

**Startwerte:** `AbschlagJeBedarfsjahrProzent = 10`, `MaxAbschlagProzent = 50`.

Beide als `public const int` auf `Stadt` — das ist das etablierte Muster der Lib für Balancing-Zahlen
(vgl. `ErpressungManager.BeziehungsverlustBeiMisserfolg`, `KatastrophenManager.PreisanstiegMin`).

### 2.2 `preisMin` verliert seine Rolle als Boden in diesem Getter

**Das ist die zentrale Designentscheidung und darf nicht übersehen werden.** Bliebe `preisMin` ein
absoluter Boden, wäre die neue Formel exakt so wirkungslos wie die alte: Für Korn liegt `preisMin` (7)
nur 12,5 % unter `preisStd` (8), sodass jeder Abschlag über 12,5 % sofort wieder weggeklemmt würde.

Künftige Aufgabenteilung:

- **`preisMin` / `preisMax` binden weiterhin den Basispreis `_rohstoffPreis[X]`** — durchgesetzt in
  `SetRohstoffPreisVonIDXToY` und `ErhoeheRohstoffPreisVonIDXByY`. Dort gehört die Grenze hin: Sie
  begrenzt den *Marktpreis*, den Zufallsschwankung und Katastrophen bewegen. Diese beiden Setter bleiben
  unverändert.
- **`MaxAbschlagProzent` ist der Boden des Sättigungsabschlags.** Bei 50 % fällt Korn im Extremfall auf
  4 Taler statt auf 7.

Der `Math.Max(..., GetPreisMin())`-Aufruf entfällt in `GetRohstoffPreisVonIDX` ersatzlos.

### 2.3 Der Vorrat wirkt mit einem Jahr Verzögerung

**Wichtig für Test und Erwartung:** `VerkaufeRohstoff` liest den Preis im Moment des Verkaufs, aber der
Vorrat wird erst zum Rundenende gebucht (`DynamischeSpieldaten.RohBedarfAktRundenEnde`). Der Absatz eines
Jahres drückt den Preis also **nicht im selben Jahr**, sondern ab dem folgenden. Diese Verzögerung bleibt
bewusst erhalten: Sie ist realistisch (der Markt reagiert verzögert) und erspart einen Eingriff in die
Zugreihenfolge.

### 2.4 Wirkung im Beharrungszustand (Korn, Basispreis 8)

Entscheidend ist nicht eine Momentaufnahme, sondern das Gleichgewicht: Der Vorrat wächst jedes Jahr um
`Absatz − Verbrauch`. Solange der Absatz den lokalen Verbrauch übersteigt, wächst er unbegrenzt weiter,
bis der Abschlag den Deckel `MaxAbschlagProzent` erreicht.

Ein voll ausgebauter Betrieb erzeugt ~2 400 Stück/Jahr bei ~13 000 Talern Betriebskosten. Eine
5 000-Einwohner-Stadt verbraucht 500 Stück/Jahr.

| Absatzstrategie | Jährlicher Überhang | Beharrungszustand | Preis | Erlös | Ergebnis |
|---|---|---|---|---|---|
| 2 400 in eine 5 000er-Stadt | +1 900 | Deckel nach ~2 Jahren | 4 | 9 600 | **Verlust** |
| 2 400 auf 3 Städte verteilt | +300 je Stadt | Deckel nach ~8 Jahren | 8 → 4 | fallend | verzögerter Verlust |
| Absatz ≤ Summe der Verbräuche | 0 | stabil | 8 | 19 200 | +6 200 |

**Die daraus folgende Kernaussage des Entwurfs:** Dauerhaft verkaufbar ist nur so viel, wie die belieferten
Städte tatsächlich verbrauchen. Wer 2 400 Stück/Jahr absetzen will, braucht Märkte mit zusammen
24 000 Einwohnern. Streuung verzögert die Sättigung, hebt sie aber nicht auf — genau das erzeugt den vom
Auftraggeber gewünschten sinkenden Grenznutzen, statt einer harten Obergrenze.

Erholung: Stellt der Spieler den Absatz ein, baut der Bevölkerungsverbrauch den Vorrat ab (im Beispiel
~4 Jahre bis zur vollen Preiserholung).

### 2.4 Erwünschte Nebenwirkungen

- **Stadtgröße wird erstmals strategisch.** Der Nenner ist `Einwohner / 10`; eine 5 000-Einwohner-Stadt
  verkraftet doppelt so viel Absatz wie eine mit 2 500. Diese Achse existiert heute nicht.
- **Selbstheilung ohne neue Zeitmechanik.** Der Bevölkerungsverbrauch baut den Vorrat über die Jahre ab;
  ein übersättigter Markt erholt sich von allein.
- **Export bekommt einen Zweck.** Der Karawanenzoll machte den Export bisher unrentabel gegenüber dem
  Verkauf vor Ort. Da der Heimatmarkt nun sättigt, wird die Fracht in einen frischen Markt zur echten
  Alternative.
- **`GetBedarf` zieht automatisch mit.** Es rankt nach `preis − stdpreis`; gesättigte Städte rutschen von
  selbst aus der Bedarfsliste, sodass die Export-Zielwahl sie meidet.
- **Übersättigte Märkte werden billige Einkaufsmärkte.** `KaufeRohstoff` nutzt denselben Getter. Das ist
  realistisch und eröffnet Arbitrage — ausdrücklich gewollt, nicht als Fehler zu behandeln.

---

## 3. Entwurf B: Progressiver Werkstatt-Kaufpreis

### 3.1 Formel

`HandelsManager.GetWerkstattKaufpreis` staffelt nach der Zahl der **reichsweit** bereits besessenen
Betriebe:

```csharp
int basis    = SW.Dynamisch.GetRohstoffwithID(RohstoffIdAnPlatz(stadtId, werkstattNr)).GetWSKaufpreis();
int besessen = Math.Min(SW.Dynamisch.GetAktHum().ZaehleWerkstaetten(), MaxSteigerungsstufen);
int preis    = basis;

for (int i = 0; i < besessen; i++)
    preis = preis * SteigerungProzent / 100;

return preis;
```

**Startwerte:** `SteigerungProzent = 125`, `MaxSteigerungsstufen = 20`.

Die Schleife mit Ganzzahl-Multiplikation ersetzt `Math.Pow` bewusst: exakt, deterministisch und ohne
Gleitkomma im Kern der Ökonomie.

### 3.2 `MaxSteigerungsstufen` ist ein Überlaufschutz, kein Balancing-Wert

Ohne Deckel läuft `int` über: `2000 × 1,25^n` überschreitet `int.MaxValue` etwa ab dem **62. Betrieb**,
und 84 sind besitzbar. Bei 20 Stufen liegt die Obergrenze bei 173 382 Talern; darüber bleibt der Preis
konstant. Dieser Deckel darf beim Kalibrieren nicht ersatzlos angehoben werden.

### 3.3 Preisverlauf

Exakte Werte der Ganzzahlschleife (jeder Schritt schneidet ab, das Ergebnis liegt daher etwas unter
`2000 × 1,25ⁿ` — Tests müssen gegen **diese** Zahlen prüfen, nicht gegen die Gleitkommapotenz):

| Kauf | 1. | 5. | 10. | 15. | ab 21. |
|---|---|---|---|---|---|
| `besessen` | 0 | 4 | 9 | 14 | 20 (gedeckelt) |
| Preis | 2 000 | 4 882 | 14 895 | 45 452 | 173 382 |

Der erste Betrieb bleibt für Einsteiger unverändert billig. Der zehnte kostet rund vier Jahresgewinne und
wird damit zur Entscheidung statt zum Selbstläufer.

### 3.4 Neuer Zähler auf `HumSpieler`

```csharp
/// <summary>Zahl der aktiven Werkstaetten dieses Spielers ueber alle Staedte.</summary>
public int ZaehleWerkstaetten()
```

Iteriert `_spielerHatInStadtXWerkstaettenY` und zählt `GetEnabled()`.

**Achtung, bekannte Stolperfalle:** Das Feld ist als `[GetMaxStadtID(), GetMaxWerkstaettenProStadt()]`
angelegt, also `[stadtId, werkstattIndex]` mit **0-basiertem** Werkstattindex. Der öffentliche Zugriff
`GetSpielerHatInStadtXWerkstaettenY(werkstaettenNr, stadtID)` hat dagegen **vertauschte Parameter** und
erwartet `werkstaettenNr` **1-basiert** (`HumSpieler.cs:616`). Beim direkten Feldzugriff gilt das Muster
aus `ErmittleLagerplatzInStadt` (`HumSpieler.cs:280`).

### 3.5 Verkaufspreis bleibt gekoppelt

`GetWerkstattVerkaufspreis` gibt weiterhin ¾ des Kaufpreises zurück und erbt die Staffelung damit
automatisch. Das ist beabsichtigt: Wer verkauft, bekommt anteilig zurück, was er bezahlt hat. Ein
Rückkauf-Arbitrage entsteht nicht, weil ¾ < 1 ist.

---

## 4. Randbedingungen

- **Keine Savegame-Migration.** Beide Änderungen sind reine Formeln über bereits vorhandenem Zustand
  (`_rohstoffVorrat`, Werkstatt-Flags). Es kommt **kein serialisiertes Feld hinzu** — die Serialisierung
  ist feldbasiert, jedes neue Feld käme aus Altständen als `null`/`0`.
- **netstandard2.0.** Keine modernen BCL-APIs.
- **Ganzzahlarithmetik** in beiden Formeln, damit E2E-Läufe seed-reproduzierbar bleiben.
- **Deutsche Domänenbenennung** für Konstanten und Methoden.
- **Lib-CHANGELOG**: bilinguale Stichpunkte unter dem einen `## [Unreleased]`-Block, kein Versionsheader.
- **Commit-Reihenfolge**: erst Lib, dann Godot; der Godot-Commit nennt die Lib-Version im Betreff.

---

## 5. Test- und Messstrategie

### 5.1 Unit-Tests (`Conspiratio.Lib.Tests`, xUnit)

Beide Formeln sind reine Funktionen; bislang deckt sie **kein** Test ab.

Sättigungspreis:
- Ohne Vorrat entspricht der Preis unverändert dem Basispreis.
- Ein Vorrat in Höhe eines Jahresbedarfs ergibt genau `AbschlagJeBedarfsjahrProzent`.
- Der Abschlag ist bei `MaxAbschlagProzent` gedeckelt, auch bei extremem Vorrat.
- Der Preis darf unter `preisMin` fallen (die bewusste Verhaltensänderung aus 2.2).
- Zwei Städte mit gleichem Vorrat, aber unterschiedlicher Einwohnerzahl ergeben unterschiedliche Preise.

Progressiver Kaufpreis:
- Ohne Betrieb entspricht der Preis unverändert `GetWSKaufpreis()`.
- Die Staffelung trifft die exakten Ganzzahlwerte aus 3.3 (Stichproben `besessen` = 4, 9, 14).
- Jenseits von `MaxSteigerungsstufen` bleibt der Preis konstant und **läuft nicht über** (Test mit 84
  Betrieben; ohne Deckel überliefe `int` ab dem 62.).
- `ZaehleWerkstaetten` zählt über Stadtgrenzen hinweg und ignoriert deaktivierte Plätze.

### 5.2 Kalibrierung gegen E2E

Die vier Zahlen (`AbschlagJeBedarfsjahrProzent`, `MaxAbschlagProzent`, `SteigerungProzent`,
`MaxSteigerungsstufen`) sind **begründete Startwerte, keine Endwerte**. Nach der Implementierung:

1. Referenzlauf mit `--ohne-aktionen --spieler=1` über feste Seeds — das isoliert den Handel von den
   zufälligen Kontor-Aktionen (deren Streuung mehrere Zehntausend Taler beträgt und sonst alles
   überdeckt).
2. Zielkorridor: Der Ertrag soll spürbar unter den heute gemessenen +64 175 bis +80 088 liegen, aber
   deutlich positiv bleiben — Expansion muss sich weiter lohnen, nur mit sinkendem Grenznutzen.
3. Erst danach die Werte festschreiben und `CLAUDE.md` mit den neuen Referenzzahlen aktualisieren, da die
   dort dokumentierten Messwerte durch diese Änderung ungültig werden.

### 5.3 Was sich in bestehenden Messungen zwangsläufig ändert

`CLAUDE.md` dokumentiert Handelsergebnisse (+33 311 im Harness, +64 175 bis +80 088 im Client-Pfad). Diese
Werte werden durch die Änderung ungültig und sind Teil der Umsetzung, nicht ein separates Aufräumen.

---

## 6. Ausdrücklich nicht im Umfang

- Verwaltungs-/Aufmerksamkeitskosten pro Kontor (der dritte, verworfene Vorschlag).
- Eine Wirtschaftssimulation für KI-Spieler.
- Neubalancierung des Karawanenzolls beim Export — erst messen, ob Entwurf A den Export von allein
  attraktiv macht.
- Anpassung der Produktionsformel oder der 99-Arbeiter-Grenze.
