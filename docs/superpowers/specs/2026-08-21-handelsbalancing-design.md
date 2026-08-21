# Handelsbalancing: Sättigungspreis und progressiver Werkstatt-Kaufpreis

**Datum:** 2026-08-21
**Betrifft:** `Conspiratio.Lib` (Spiellogik) und `Conspiratio.Godot` (fehlende Rundenende-Aufrufe, E2E-Messung)
**Ziel:** Den unbegrenzten Skalierungsvorteil mehrerer Kontore bremsen, ohne Aufbaufreude zu nehmen — und
die dafür nötige Wirtschaftsmechanik überhaupt erst zum Laufen bringen.

---

## 1. Problem

Erfahrene Spieler verdienen mit mehreren Kontoren beliebig viel Geld. Die Messungen der aktuellen
E2E-Läufe und ein Blick in die Lib zeigen dafür zwei Ursachen (1.1, 1.2) — und bei deren Prüfung kamen
zwei weitere Befunde ans Licht, ohne die eine Gegenmaßnahme wirkungslos bliebe (1.4, 1.5).

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

### 1.4 Drei Rundenende-Aufrufe fehlen im Godot-Client

Bei der Prüfung des Nenners `Einwohner / 10` kam heraus, dass der Godot-Client Teile der
Rundenende-Sequenz des WinForms-Referenzclients (`Conspiratio/Main.cs:6345–6350`) nie übernommen hat.
Jeder dieser Aufrufe existiert in der Lib, wird aber von **keinem** Godot-Skript aufgerufen:

| Fehlender Aufruf | Folge im heutigen Godot-Spiel |
|---|---|
| `DynamischeSpieldaten.RohBedarfAktRundenEnde()` | Spielerverkäufe landen nie im Stadtvorrat, die Bevölkerung verbraucht nichts — der Vorrat bleibt für immer unverändert |
| `DynamischeSpieldaten.RohPreiseRandomSchwanken()` | Die Warenpreise stehen dauerhaft still; es gibt keinerlei Marktbewegung |
| `DynamischeSpieldaten.RundenBestechungenAbwickeln()` | Rundenbestechungen werden nie abgewickelt |

`DeliktpunkteBerechnen()` fehlt in dieser Sequenz ebenfalls, ist aber unkritisch: `KircheManager` und
`Kirchgang` rufen es auf.

**Der erste dieser drei ist eine harte Voraussetzung für Entwurf A.** Ohne ihn bleibt `_rohstoffVorrat`
konstant, und ein vorratsabhängiger Preis wäre wirkungslos — die Änderung wäre nicht messbar, sondern
schlicht folgenlos.

### 1.5 Die Einwohnerzahl kann nur sinken

`_einwohner` wird an genau zwei Stellen geschrieben: im Konstruktor und in `KatastrophenManager`
(Zeile 230–231), der sie ausschließlich **senkt** — 8–20 % je Ereignis, bei Pest über
`PestEinwohnerFaktor` verdoppelt auf 16–40 %, und über `neueEinwohner < 0 ? 0` bis auf null. Ein
Wachstum gibt es nirgends. Dasselbe gilt für `_reichtum`, den derselbe Manager um 1–2 Punkte je
Ereignis senkt — Entwurf D gibt ihm ein Gegengewicht.

Bei 22 % Jahreschance (`KatastrophenManager.JahresChance`) und den Umfangsanteilen (15 % Reich,
35 % Grafschaft, sonst eine Stadt) trifft es eine Stadt über 40 Jahre im Mittel rund 2,6-mal, was etwa
30 % Bevölkerungsverlust ohne jede Erholung bedeutet.

Für Entwurf A wirkt das **doppelt**: Der Jahresbedarf im Nenner sinkt (der Abschlag wird steiler) *und*
der Verbrauch, der den Vorrat abbaut, sinkt mit. Die Marktsättigung würde sich über die Spieldauer also
selbst verschärfen, bis der Handel dauerhaft unrentabel wäre. Ein Bevölkerungswachstum ist damit keine
Ergänzung, sondern Voraussetzung — und historisch ohnehin geboten: Um 1600 wuchs die Bevölkerung.

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

## 4. Entwurf C: Bevölkerungswachstum

### 4.1 Formel

Neue Methode `DynamischeSpieldaten.EinwohnerWachstumAktRundenEnde()`, am Rundenende neben den übrigen
Wirtschaftsaufrufen. Gerechnet wird in **Promille**, damit kein Gleitkomma in den Kern der Ökonomie
gerät:

```csharp
foreach (Stadt in allen Städten)
{
    int wachstumPromille = GrundwachstumPromille
                         + stadt.GetReichtum() * ReichtumBonusPromille
                         - stadt.GetKriminalitaet() * KriminalitaetMalusPromille
                         + SW.Statisch.Rnd.Next(-ZufallPromille, ZufallPromille + 1);

    int neu = stadt.GetEinwohner() + (stadt.GetEinwohner() * wachstumPromille) / 1000;

    stadt.SetEinwohnerAufX(Math.Min(MaxEinwohner, Math.Max(MindestEinwohner, neu)));
}
```

**Startwerte:** `GrundwachstumPromille = 10`, `ReichtumBonusPromille = 2`,
`KriminalitaetMalusPromille = 2`, `ZufallPromille = 5`, `MindestEinwohner = 250`,
`MaxEinwohner = 12000`.

### 4.2 Begründung der Werte

Die Stadtdaten führen `Reichtum` 1–7 (Skala bis `GetMaxReichtum()` = 14) und `Kriminalität` 1–5, im
Mittel etwa 3,6 bzw. 2,8. Daraus folgt ein mittleres Wachstum von
`10 + 3,6·2 − 2,8·2 ≈ 11,6 ‰`, also ~1,2 % pro Jahr gegen ~0,9 % Katastrophenverlust — netto ein
langsames Wachstum, das Rückschläge ausgleicht, ohne sie bedeutungslos zu machen. Die Spanne über alle
Städte reicht von etwa −0,3 % bis +2,7 % jährlich, sodass sich Städte spürbar unterschiedlich
entwickeln.

### 4.3 Die beiden Grenzen sind bewusst gesetzt

- **`MindestEinwohner`**: Multiplikatives Wachstum kann eine auf null gefallene Stadt nie wiederbeleben
  (`0 × irgendwas = 0`). Die Untergrenze macht Katastrophen erholbar statt endgültig. Sie hebt eine
  verwüstete Stadt aktiv wieder an — das schwächt schwere Katastrophen bewusst ab und ist der Preis
  dafür, dass keine Stadt dauerhaft aus dem Spiel fällt.
- **`MaxEinwohner`**: Verhindert unbegrenztes Wachstum. Bewusst eine **globale Konstante** statt einer
  Obergrenze relativ zum Startwert der Stadt: Deren Startwert wird nirgends gespeichert, und ein neues
  Feld dafür wäre ein serialisiertes Feld mit Savegame-Folgen (siehe Randbedingungen).

### 4.4 Zusammenspiel mit Entwurf A

Die Untergrenze von 250 Einwohnern setzt zugleich einen Boden unter den Jahresbedarf
(`Math.Max(1, Einwohner / 10)` ≥ 25). Ohne sie könnte der Nenner gegen 1 laufen und der Mengenabschlag
selbst bei geringem Vorrat sofort den Deckel erreichen.

---

## 5. Entwurf D: Reichtumswachstum

### 5.1 Formel

Neue Methode `DynamischeSpieldaten.ReichtumWachstumAktRundenEnde()`. `Reichtum` ist ein kleiner
Ganzzahlwert (Stadtdaten 1–7, Obergrenze `GetMaxReichtum()` = 14), lässt sich also nicht in kleinen
Schritten erhöhen. Statt eines Bruchteil-Zählers — der ein **neues serialisiertes Feld** wäre — entscheidet
je Jahr ein Würfelwurf, dessen Chance vom Handelsvolumen abhängt:

```csharp
// Handelsvolumen der Stadt: was die Menschen dieses Jahr hier netto abgesetzt haben.
int volumen = Summe über alle Menschen und Rohstoffe von
              GetEinVerkaeufeInStadtXVonRohstoffIDY(stadtId, rohId);

if (volumen < 0)
    volumen = 0;          // Einkäufe stehen negativ in derselben Zahl

int chance = volumen / HandelsvolumenJeReichtumsChance
           - stadt.GetKriminalitaet() * KriminalitaetReichtumsMalus;

if (chance > MaxReichtumsChance)
    chance = MaxReichtumsChance;

if (chance > 0 && SW.Statisch.Rnd.Next(0, 100) < chance)
    stadt.SetReichtumToX(stadt.GetReichtum() + 1);   // nie über GetMaxReichtum()
```

**Startwerte:** `HandelsvolumenJeReichtumsChance = 100` (100 Stück Absatz = 1 Prozentpunkt Chance),
`KriminalitaetReichtumsMalus = 3`, `MaxReichtumsChance = 40`.

Ein Markt mit 2 400 Stück Jahresabsatz und Kriminalität 3 kommt damit auf `24 − 9 = 15 %` Chance pro
Jahr, also grob einen Punkt alle sieben Jahre. `SetReichtumToX` klemmt **nicht** von sich aus — die
Obergrenze `GetMaxReichtum()` muss die Methode selbst ziehen.

### 5.2 Die Reihenfolge ist bindend

`RohBedarfAktRundenEnde` **nullt** die Verkaufsmengen, nachdem es sie in den Stadtvorrat gebucht hat
(`SetEinVerkaeufeInStadtXVonRohstoffIDYAufZ(..., 0)`). `ReichtumWachstumAktRundenEnde` muss deshalb
**davor** laufen, sonst misst es dauerhaft ein Handelsvolumen von null und wirkt nie.

Aus demselben Grund scheidet `GetUmsatzInStadtX` als Bezugsgröße aus, obwohl Taler-Umsatz für „Reichtum"
näherliegend wäre: `AbrechnungsManager` setzt ihn bei der Jahresabrechnung des Spielers zurück
(Zeile 208), die vor dem Rundenende liegt. Die Stückzahl ist die einzige Größe, die zum Rundenende noch
unversehrt vorliegt.

### 5.3 Was Reichtum bewirkt — und die Rückkopplung, die daraus entsteht

`Reichtum` wird heute nur an zwei Stellen gelesen: `LagerraumManager` (Preiszuschlag beim Lagerausbau,
`GetReichtum() / GetMaxReichtum()`) und die Stadtinformationen. Zusammen mit Entwurf C entsteht ein
geschlossener Kreis:

**Viel Handel → Stadt wird reicher → (a) Lagerausbau dort wird teurer, (b) die Stadt wächst schneller
(Entwurf C nutzt `Reichtum` als Wachstumsfaktor) → mehr Einwohner heben den Jahresbedarf → der
Sättigungsabschlag aus Entwurf A fällt milder aus.**

Das ist ausdrücklich erwünscht: Es gibt dem Spieler die Möglichkeit, einen Markt über Jahre zu
*entwickeln*, statt nach der Sättigung nur weiterzuziehen. Der Kreis ist durch `GetMaxReichtum()` (14)
und `MaxEinwohner` (12 000) beidseitig beschränkt und kann daher nicht davonlaufen.

Kriminalität wirkt als Gegengewicht, und `KatastrophenManager` senkt den Reichtum weiterhin um 1–2
Punkte je Ereignis.

---

## 6. Fehlende Rundenende-Aufrufe nachziehen

Die drei in 1.4 genannten Aufrufe werden im Godot-Client ergänzt, in der Reihenfolge des
Referenzclients, zusammen mit dem neuen Wachstum aus Entwurf C. Einstiegspunkt ist der
`IstLetzterSpielerImJahr()`-Block in `assets/scripts/Kontor.cs` (Zeile 737–747), **vor** den bereits
vorhandenen Aufrufen:

```csharp
SW.Dynamisch.RohPreiseRandomSchwanken();
SW.Dynamisch.ReichtumWachstumAktRundenEnde();   // MUSS vor RohBedarfAktRundenEnde stehen
SW.Dynamisch.RohBedarfAktRundenEnde();          // bucht die Verkaeufe und nullt sie danach
SW.Dynamisch.EinwohnerWachstumAktRundenEnde();
SW.Dynamisch.RundenBestechungenAbwickeln();
```

**Die Reihenfolge der ersten drei ist bindend, nicht kosmetisch:** `RohBedarfAktRundenEnde` nullt die
Verkaufsmengen. Stünde das Reichtumswachstum danach, läse es dauerhaft ein Handelsvolumen von null.
Das Einwohnerwachstum steht bewusst *nach* dem Reichtumswachstum, damit es den frisch aktualisierten
Reichtum als Faktor verwendet.

Das Wachstum läuft **vor** `ZeigeKatastrophe()`, das im selben Block später folgt: Erst wächst die Stadt
um ihre Jahresrate, dann schlägt gegebenenfalls die Katastrophe zu. So ist das Wachstum die langsame
Grundlinie und die Katastrophe der Schock — nicht umgekehrt.

`RundenBestechungenAbwickeln` gehört fachlich nicht zum Handel, wird aber mit aufgenommen, weil es
dieselbe fehlende Sequenz betrifft und sonst weiter unbemerkt ausfiele.

---

## 7. Randbedingungen

- **Keine Savegame-Migration.** Beide Änderungen sind reine Formeln über bereits vorhandenem Zustand
  (`_rohstoffVorrat`, Werkstatt-Flags). Es kommt **kein serialisiertes Feld hinzu** — die Serialisierung
  ist feldbasiert, jedes neue Feld käme aus Altständen als `null`/`0`.
- **netstandard2.0.** Keine modernen BCL-APIs.
- **Ganzzahlarithmetik** in beiden Formeln, damit E2E-Läufe seed-reproduzierbar bleiben.
- **Deutsche Domänenbenennung** für Konstanten und Methoden.
- **Lib-CHANGELOG**: bilinguale Stichpunkte unter dem einen `## [Unreleased]`-Block, kein Versionsheader.
- **Commit-Reihenfolge**: erst Lib, dann Godot; der Godot-Commit nennt die Lib-Version im Betreff.

---

## 8. Test- und Messstrategie

### 8.1 Unit-Tests (`Conspiratio.Lib.Tests`, xUnit)

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

Bevölkerungswachstum:
- Eine Stadt wächst über mehrere Runden messbar (Zufallsanteil durch festen Seed gebunden).
- Eine reiche Stadt wächst schneller als eine gleich große mit hoher Kriminalität.
- `MaxEinwohner` wird nie überschritten, auch nicht nach vielen Runden.
- Eine auf 0 gesetzte Stadt erholt sich auf `MindestEinwohner` — die Probe darauf, dass multiplikatives
  Wachstum nicht bei null hängen bleibt.

Reichtumswachstum:
- Ohne Handel in der Stadt steigt der Reichtum nicht.
- Nach einem Jahr mit hohem Absatz steigt er (Zufall über festen Seed gebunden; notfalls über mehrere
  Runden prüfen, da der Wurf probabilistisch ist).
- `GetMaxReichtum()` wird nie überschritten — wichtig, weil `SetReichtumToX` selbst **nicht** klemmt.
- Hohe Kriminalität senkt die Chance messbar gegenüber einer sonst gleichen Stadt.
- **Die Reihenfolge-Probe:** Läuft `RohBedarfAktRundenEnde` zuerst, misst das Reichtumswachstum ein
  Volumen von null und der Reichtum steigt nicht. Dieser Test hält die bindende Aufrufreihenfolge aus
  6. fest, damit eine spätere Umstellung nicht unbemerkt die Mechanik abschaltet.

Zur Katastrophen-Wechselwirkung genügt die Zusicherung, dass Wachstum und `KatastrophenManager`
denselben Wert über `SetEinwohnerAufX` bzw. `SetReichtumToX` schreiben; ein kombinierter Test wäre wegen
der 22-%-Jahreschance zufallsabhängig und damit unzuverlässig.

### 8.2 Kalibrierung gegen E2E

Die vier Zahlen (`AbschlagJeBedarfsjahrProzent`, `MaxAbschlagProzent`, `SteigerungProzent`,
`MaxSteigerungsstufen`) sind **begründete Startwerte, keine Endwerte**. Nach der Implementierung:

1. Referenzlauf mit `--ohne-aktionen --spieler=1` über feste Seeds — das isoliert den Handel von den
   zufälligen Kontor-Aktionen (deren Streuung mehrere Zehntausend Taler beträgt und sonst alles
   überdeckt).
2. Zielkorridor: Der Ertrag soll spürbar unter den heute gemessenen +64 175 bis +80 088 liegen, aber
   deutlich positiv bleiben — Expansion muss sich weiter lohnen, nur mit sinkendem Grenznutzen.
3. Erst danach die Werte festschreiben und `CLAUDE.md` mit den neuen Referenzzahlen aktualisieren, da die
   dort dokumentierten Messwerte durch diese Änderung ungültig werden.

### 8.3 Was sich in bestehenden Messungen zwangsläufig ändert

`CLAUDE.md` dokumentiert Handelsergebnisse (+33 311 im Harness, +64 175 bis +80 088 im Client-Pfad). Diese
Werte werden durch die Änderung ungültig und sind Teil der Umsetzung, nicht ein separates Aufräumen.

---

## 9. Ausdrücklich nicht im Umfang

- Verwaltungs-/Aufmerksamkeitskosten pro Kontor (der dritte, verworfene Vorschlag).
- Eine Wirtschaftssimulation für KI-Spieler.
- Neubalancierung des Karawanenzolls beim Export — erst messen, ob Entwurf A den Export von allein
  attraktiv macht.
- Anpassung der Produktionsformel oder der 99-Arbeiter-Grenze.
