# Konzept: Produktionsfertigkeit und der Faktor

**Stand:** 2026-09-05 · **Betrifft:** Lib (Regel, Manager) und Godot-Client (Dialoge). Nicht umgesetzt.

## Der Anknüpfungspunkt: eine Zahl, die es schon gibt und die nichts bedeutet

`Produktionsslot.GetProduktion` würfelt auf jede Jahresproduktion eine Schwankung von ±10 % — und
rechnet **genau diesen Würfel** in `qualitaetProzent` um (50 = kein Ausschlag). Das Buch zeigt das als
„schlecht / mäßig / normal / gut / ausgezeichnet" (`BuchManager.QualitaetAlsText`).

Es ist also die Anzeige von reinem Rauschen: Der Spieler kann sie nicht beeinflussen, und sie hat keine
Wirkung — sie steht nur da. **Eine Fertigkeit gäbe dieser vorhandenen Zahl zum ersten Mal eine
Ursache**, und die Anzeige dafür ist schon gebaut.

## Die Mechanik

Eine Fertigkeit **je Ware**, 0–100. Sie hebt den unteren Rand der Schwankung an: Der Meister hat
weniger Fehlchargen, nicht bessere Glücksjahre.

```csharp
int PlusMinus  = Convert.ToInt32(vorlaeufigeProduktion * 0.1);
int Schwankung = SW.Statisch.Rnd.Next(-PlusMinus, PlusMinus);

// NEU: Die Fertigkeit hebt den Boden, nicht die Decke.
int bonus  = (PlusMinus * fertigkeit) / MaxFertigkeit;
Schwankung = Math.Min(PlusMinus, Schwankung + bonus);
```

Weil `qualitaetProzent` aus `Schwankung` abgeleitet wird, **wandert die Qualitätsanzeige im Buch
automatisch mit** — kein einziger Handgriff an der Darstellung.

**Was das wert ist:** Heute liegt die Schwankung im Mittel bei 0, bei Fertigkeit 100 im Mittel bei
+5 % der Produktion. Das ist bewusst klein. Zwei Stellschrauben, beide eine Konstante:

- `MaxFertigkeit` (100) — wie fein die Skala ist.
- Ein optionaler `MeisterZuschlag`, der bei hoher Fertigkeit auch die *obere* Grenze anhebt. **Auf 0
  lassen, bis gemessen ist.** Er ist der Weg, die Wirkung zu vergrößern, ohne die Formel zu ändern.

**Warum nicht 0–100 % auf die Menge:** Bei 0 gäbe es kein Einkommen, das Spiel wäre unspielbar; der
Bereich müsste faktisch bei 60 % beginnen und wäre dann ein Faktor 1,6 auf die Kernwirtschaft. Vor
allem aber wäre ein dauerhaft gekaufter Ertragsbonus im Spätspiel ein **Anti-Sink**: einmal bezahlt,
für immer wirksam — er hebt genau die Obergrenze, die gesenkt werden soll.

**Ein Nebenbefund für die Umsetzung:** `Rnd.Next(-PlusMinus, PlusMinus)` hat eine exklusive
Obergrenze, die Schwankung ist heute also leicht negativ verzerrt ([−P, P−1], Mittel −0,5). Wer die
Zeile ohnehin anfasst, sollte das mitnehmen — aber getrennt beurteilen, weil es für sich genommen
schon eine (winzige) Ertragsänderung ist.

## Was es *nicht* berührt: das KI-Balancing

`GetProduktion` hat genau **einen** Aufrufer: `BuchManager`, und der erstellt das Buch des
menschlichen Spielers. KI-Spieler produzieren nicht über diesen Weg. Die Fertigkeit ist damit ein rein
menschenseitiger Hebel, zwischen mehreren Menschen am selben Rechner symmetrisch. Die KI braucht keine
eigene Fertigkeit, und es entsteht keine Schieflage ihr gegenüber.

## Datenmodell und Spielstände

Auf `HumSpieler` ein `int[]` je Ware, dazu ein zweites für „zuletzt produziert im Jahr" (für das
Verlernen). Beide nach der bestehenden Konvention über einen **Lazy-Init-Accessor**
(`GetProduktionsfertigkeitenSicher()`, Muster wie `GetAhnentafelListe()`), weil die Serialisierung
feldbasiert ist und Konstruktoren umgeht.

**Alte Spielstände brauchen keine Migration:** Das Feld kommt als `null` an, der Accessor legt es mit
Nullen an — und **Fertigkeit 0 ist exakt das heutige Verhalten**. Ein geladener Spielstand rechnet
also weiter wie bisher.

Die Fertigkeit sollte `GetProduktion` als **Parameter** übergeben werden, nicht im Slot über
`SW.Dynamisch` geholt werden: Der Slot kennt seinen Besitzer nicht, `BuchManager` schon — und ein
Parameter bleibt testbar. Bei einem einzigen Aufrufer ist das eine Zeile.

## Vier Wege hinauf, einer hinunter

| Weg | Wann | Wirkung | Charakter |
|---|---|---|---|
| **Buch kaufen** | jederzeit, in einer Stadt mit dieser Hauptproduktion | +10, einmalig je Ware | planbar, billig |
| **Meisterschrift finden** | Zufallsfund, selten | +15, einmalig je Ware | Glück, nicht käuflich |
| **Vortrag** | jährliche Einladung für eine Ware, die man produziert | +4 gegen Eintritt | Entscheidung, kleines Risiko |
| **Lehrstunde** | jederzeit, gegen steigendes Honorar | +5 je Stunde | der teure, verlässliche Weg |
| **Hausgelehrter** | nur mit Schloss | +2 pro Jahr, bis höchstens 80 | Spätspiel, laufende Kosten |
| **Verlernen** | Ware drei Jahre nicht produziert | −3 pro Jahr | erzwingt Spezialisierung |

**Bücher** binden die Fertigkeit an die Landkarte: Das Buch zur Ware gibt es dort, wo die Ware
Hauptproduktion ist (`Stadt` kennt das bereits über `GetEffizienzVonRohstoffMitIDX` und die
Hauptproduktions-Reihe im Stadtinformationen-Dialog). Damit lohnt Reisen, ohne dass eine neue Mechanik
nötig wäre. Titel in deinem Ton, der Rest der 21 Waren folgt dem Muster:

| Ware | Grundlagenbuch |
|---|---|
| Holz | „Stolz wie Holz — Geheimnisse des Waldbauern" |
| Wolle | „Das Tolle der Wolle — Tricks für Schäfer" |
| Fisch | „Angeln richtig gemacht" |
| Glas | „Glas richtig blasen" |
| Bier | „Vom Hopfen und vom Malz" |
| Korn | „Der Kalender des Schnitters" |
| Wein | „Im Weine liegt die Wahrheit — und im Fass die Arbeit" |
| Salz | „Weißes Gold — vom Sieden und vom Sieben" |
| Waffen | „Vom Hammer zum Harnisch" |
| Silber | „Der stille Glanz — Scheiden und Treiben" |

**Der Vortrag** ist der Ort für dein Zufallsereignis, aber mit einer Entscheidung darin: Einmal im
Jahr kann für eine Ware, die der Spieler tatsächlich produziert, eine Einladung kommen. Annehmen
kostet Eintritt (~1 500), bringt +4 — **und in einem von sechs Fällen war der Redner ein Schwätzer**:
Das Geld ist weg, die Fertigkeit unverändert. Ablehnen kostet nichts.

Das ist bewusst die Form, in der ein Ereignis „schiefgehen" kann. **Ein Ereignis, das die Fertigkeit
einfach senkt, würde ich sparsam einsetzen**: Sie hängt am Kerneinkommen, und ein Verlust ohne
Zutun des Spielers fühlt sich als Willkür an — zwei Spieler mit identischem Spiel driften dann
auseinander, ohne etwas falsch gemacht zu haben. Der ehrliche Weg nach unten ist das **Verlernen bei
Nichtgebrauch**: nachvollziehbar, abwendbar, und es erzwingt genau die Spezialisierung, die zur
Marktsättigung passt — wer alles kann, sättigt jeden Markt, den er beliefert.

Wenn du trotzdem echtes Pech willst, dann klein und selten: „Ihr folgtet einer neumodischen Lehre"
(−3, höchstens alle paar Jahre) — spürbar als Ärgernis, nicht als Rückschlag.

## Zur Schloss-Frage: für das Training nein, für den Hausgelehrten ja

Gemessen: Ein **Schloss kostet 250 000 Taler** (Kate 1 000, Hütte 2 000, Haus 3 500, Landhaus 10 000,
Villa 20 000). Das gemessene 40-Jahre-Band des E2E-Treibers liegt bei **+38 000 bis +178 000**, Median
125 177 — das Schloss kostet also **mehr als das gesamte Endvermögen eines typischen Laufs**.

Das Band ist die Decke des *Treibers*, nicht die eines guten Spielers: Er bearbeitet einen
Werkstattplatz in einer Stadt und ist bei 99 Arbeitern gedeckelt. Ein Mensch kommt deutlich höher.
Aber die Größenordnung bleibt: Das Schloss ist der letzte Kauf des Spiels, und die Lehrstunde dahinter
wäre nicht „später verfügbar", sondern **in den meisten Partien nie**. Der Skill bewegte sich dann
über die gesamte Spielzeit nur durch Glück — Zufall in der Phase, in der die Marge am dünnsten ist,
und kein Gegenmittel. Das würde ich nicht tun.

**Der bessere Zuschnitt:** Die Lehrstunde ist von Anfang an verfügbar und regelt sich über den Preis
selbst — nach dem erprobten Muster der Fechtstunde (`GrundpreisFechtstunde` 1 000,
`PreisSteigerungProStunde` 750, gezählt über `FechtstundenGenommen`). Vorschlag hier: **1 000 Grundpreis
+ 500 je bereits genommener Stunde *dieser Ware***. Eine Ware auf 50 zu bringen kostet damit rund
32 500, auf 100 rund 115 000 — früh spürbar, spät erreichbar, und niemals nebenbei für alle 21 Waren.

Das Schloss bekommt stattdessen den **Hausgelehrten**: +2 pro Jahr auf eine gewählte Ware, gegen
Jahreslohn, und nur bis Fertigkeit 80 — die letzte Strecke bleibt Büchern, Vorträgen und Stunden
vorbehalten. Das gibt dem Schloss endlich eine Funktion über die 50 Ansehen hinaus, die es heute allein
für 250 000 Taler liefert, und es ist eine *laufende* Ausgabe statt einer einmaligen.

## Die Fertigkeit anzeigen

### Zuerst die Wortfalle

Das Buch zeigt bereits `schlecht / mäßig / normal / gut / ausgezeichnet` — und das ist **der
Jahreswürfel**, nicht das Können. Eine zweite Textleiter mit denselben Wörtern für die Fertigkeit
stünde unmittelbar daneben und bedeutete etwas anderes: genau die Klasse von Verwechslung, die beim
farbigen Lagerstandsfeld schon einmal aufgetreten ist (siehe
[`saettigungsrabatt-sichtbar-konzept.md`](saettigungsrabatt-sichtbar-konzept.md), Stufe D — dort wies
die Farbe in allen gemessenen Fällen in die gefährliche Richtung).

**Die Regel, die beide auseinanderhält: Adjektive beschreiben das Jahr, eigene Wörter das Können.**
Die Leiter unten übernimmt deshalb `laienhaft`, `mittelmäßig` und `annehmbar` aus deinem Vorschlag,
lässt aber `schlecht` und `gut` aus — das sind die beiden, die wörtlich mit dem Buch kollidieren.

| Fertigkeit | Wort |
|---|---|
| 0–14 | unkundig |
| 15–29 | laienhaft |
| 30–44 | mittelmäßig |
| 45–59 | annehmbar |
| 60–74 | tüchtig |
| 75–89 | meisterlich |
| 90–100 | vollendet |

Sieben Stufen zu je rund 15 Punkten. Ein Buch (+10) oder zwei Lehrstunden (+10) rücken den Spieler
also nicht zwingend eine Stufe weiter — deshalb gehört **das Wort auf die Wand und die Zahl in den
Tooltip**, sonst wirkt eine bezahlte Stunde folgenlos.

### Wo

**In der Warenspalte der Stadtansicht**, als dritte Zeile unter Preis und Bestand — dort, wo der
Spieler die Ware ohnehin ansieht, und nur für Werkstätten, die er besitzt (dieselbe Bedingung
`hatWerkstatt` wie bei den beiden anderen Zeilen).

Der Platz ist gemessen und vorhanden, anders als in den Pergamentzeilen: `LabelPreis` endet bei
y = 372, `LabelBestand` bei y = 518, das Pergament des Hintergrundbildes beginnt erst bei y ≈ 664 —
rund 140 px frei, genug für eine weitere Zeile von 26 px. Der Spaltenabstand beträgt 212 px bei 100 px
Labelbreite; „vollendet" braucht rund 116 px, „meisterlich" rund 142 px, beide passen mittig zwischen
die Spalten. Zum Vergleich: In der Verkaufszeile blieben nur 84 px, weshalb Stufe C dort auf einen
Tooltip ausweichen musste.

**Das ist eine Rechnung, kein Beweis.** Die Breiten sind aus einer mittleren Zeichenbreite
hochgerechnet; vor dem Commit gehört derselbe Nachweis dazu wie bei Stufe B — rendern, das Bild
ansehen, Pixel nachmessen. Genau an dieser Stelle ist die erste Fassung von Stufe C aufgelaufen.

### Und an zwei weiteren Stellen

- **Im Tooltip der Werkstatt** die Zahl und der Wirkungssatz, z. B. *„Bierbrauen: annehmbar (48).
  Schlechte Jahre werden seltener, gute nicht besser."* Der Produktknopf der Produktionszeile trägt
  mit `ErmittleVerhaeltnisTooltip` bereits einen Tooltip — dort ließe sich das anhängen.
- **Im Jahresbuch**, neben der Qualitätsangabe: *„Die Bierproduktion geriet gut — als annehmbarer
  Braumeister (48)."* Das ist der Moment, in dem der Ertrag verbucht wird, und damit die beste
  Gelegenheit, Ursache und Wirkung nebeneinanderzustellen.

### Was die Anzeige *nicht* tun sollte

**Keine Farbcodierung.** Das Gold der Warenspalte trägt seit Stufe B bereits eine Bedeutung — es
bleicht mit dem Marktabschlag aus. Eine zweite Bedeutung auf demselben Kanal ließe sich nicht mehr
auseinanderhalten, und Rot ist in dieser Ansicht ohnehin für ungültige Werte vergeben. Die Fertigkeit
bekommt Worte, nicht Farbe.

**Nicht in die Produktionszeile schreiben.** Sie liegt auf demselben Pergament wie die Verkaufszeile
und unterliegt derselben Grenze bei x = 1101.

## Der Faktor: Bequemlichkeit und Vorausschau, nicht Erklärung

Getrennte Sache, getrennt zu bauen — und die eigentliche Geldsenke.

**Was er verkauft** (nichts davon nimmt etwas weg, das heute sichtbar ist):

- **Der Vergleich aller vierzehn Städte** für eine Ware in einer Tabelle. Den gibt es heute nirgends;
  die Zielstadt wird beim Export einzeln durchgeschaltet (siehe
  [`saettigungsrabatt-sichtbar-konzept.md`](saettigungsrabatt-sichtbar-konzept.md), Stufe C).
- **Die Prognose.** Aus Stadtvorrat *V*, Jahresbedarf *J* = Einwohner/10 und der eigenen
  Jahreslieferung *L*: Der Deckel ist bei fünf Jahresbedarfen Vorrat erreicht, also

  ```
  Jahre bis zum Höchstabschlag = (5·J − V) / (L − J)        für L > J
  ```

  Sie ignoriert fremde Lieferungen und die Preisschwankung — das muss der Text sagen, sonst ist sie
  ein Versprechen statt einer Schätzung.
- Warnung, wenn ein belieferter Markt den Deckel erreicht.

**Kosten:** Jahreslohn, der mit dem Betrieb wächst — etwa 750 Grundlohn + 350 je Stadt, in der man
eine Werkstatt besitzt. Bei sechs Städten rund 2 850 im Jahr, also deutlich über dem
`GrundunterhaltProWerkstatt` von 100.

**Warum er die bessere Senke ist als der Skill:** Sein Nutzen kumuliert nicht. Er kostet jedes Jahr
aufs Neue und macht den Spieler nicht reicher, sondern klüger. Der Skill dagegen ist Fortschritt —
einmal bezahlt, für immer wirksam — und darf deshalb nur klein sein.

## Was gemessen wurde

Nachgetragen, nachdem Regel, Erwerbswege und Anzeige standen. Der Harness kannte die Fertigkeit
bis dahin nicht – die drei Fragen unten waren also offen, obwohl das Feature fertig war. Zwei
neue Schalter machen sie messbar: `--fertigkeit=N` setzt jede betriebene Ware darauf,
`--lehrgeld` bezahlt den Weg dorthin mit echten Lehrstunden statt ihn zu schenken.

14 Exportlinien, 40 Jahre, fünf Seeds, Mediane des Endvermögens:

| Fertigkeit | Startkapital 500 000 | Startkapital 900 000 |
|---|---:|---:|
| 0 | 791 318 | 1 191 318 |
| 50 (geschenkt) | 1 052 901 | — |
| 100 (geschenkt) | 1 312 390 | — |
| 100 (mit Lehrgeld bezahlt) | 828 743 | 1 138 890 |

**1. Der Ertragshebel ist groß – größer, als die Regel vermuten lässt.** Geschenkte volle
Fertigkeit bringt **+66 %** Endvermögen. Die Regel hebt den Ertrag nur um rund 5 %, aber sie hebt
ihn auf eine dünne Marge (rund 320 000 Erlös gegen 290 000 Kosten), und die Kosten bleiben
gleich: Ein Zwanzigstel mehr Erlös ist dort die Hälfte mehr Gewinn. Wer die Fertigkeit billig
bekommt, bekommt viel.

**2. Die Sättigung fängt ihn nicht auf.** Die Erwartung oben – mehr Ware drückt den Preis, der
Hebel begrenzt sich teilweise selbst – **bestätigt sich nicht**: Der Marktabschlag steht bei
Fertigkeit 100 praktisch unverändert (10/17/18 % gegen 10/17/11 % über drei Seeds). Wer in
vierzehn Städte exportiert, verschiebt mit 5 % mehr Menge keinen Vorrat nennenswert. Die
Selbstbegrenzung gibt es nur für den, der alles in eine Stadt liefert.

**3. Bezahlt trägt die Senke.** Das Lehrgeld für die betriebenen Waren beträgt rund 378 000
Taler, und damit liegt das Ergebnis im Rauschen um den ungelernten Fall: +4,7 % bei 500 000
Startkapital, **−4,4 % bei 900 000**. Der zweite Vergleich ist der ehrlichere, weil dort das
Lehrgeld nicht zugleich als Betriebskapital fehlt. Über vierzig Jahre zehrt die Preisstaffel den
Ertragsgewinn also ungefähr auf – genau das, was ein dauerhafter Ertragsbonus tun muss, um kein
Anti-Sink zu sein.

**Was daraus folgt.** Die Kalibrierung trägt, aber sie hängt vollständig am Preis des Erwerbs,
nicht an der Regel. Gemessen ist nur der Lehrstundenweg; Buch (+10 für 2 000 bis 6 000),
Meisterschrift (+15, gratis gefunden), Vortrag (+4 für 1 500) und Hausgelehrter (+2 im Jahr für
3 000) sind je Punkt billiger. Wer sie nutzt, bekommt denselben Hebel günstiger – die
Meisterschrift sogar umsonst. Sollte sich das Spätspiel als zu reich erweisen, ist der Preis der
billigen Wege die Stellschraube, nicht der Bonus selbst.

### Was dafür nötig war

Ein Ertragshebel ist genau die Art Änderung, die nicht nach Gefühl entschieden werden darf, und das
Messinstrument fehlt: Ein E2E-Lauf kann das Spätspiel nicht beurteilen (ein Werkstattplatz, eine
Stadt, 99 Arbeiter). Nötig ist die **Konsolen-Harness, die einen reichen Spieler direkt aufbaut** —
derselbe offene Punkt, der schon beim Paket-A/B-Balancing stehen blieb.

Zu messen wären drei Dinge: der Ertragsunterschied zwischen Fertigkeit 0 und 100 bei sonst gleichem
Betrieb; ob die Sättigung den Gewinn wie erwartet auffängt (mehr Ware drückt den Preis — der Hebel
sollte sich teilweise selbst begrenzen); und was die Trainingskosten über eine Partie tatsächlich
abschöpfen.

Ein günstiger Umstand: Die Änderung **verbraucht gleich viele Zufallsziehungen** wie bisher, nur mit
anderen Grenzen — der Zufallsstrom verschiebt sich also nicht, anders als bei den Rundenende-Aufrufen
damals. Gleiche Seeds bleiben trotzdem nicht vergleichbar, weil das Ergebnis über das Vermögen in alle
späteren Entscheidungen streut. Also weiterhin: Verteilungen über mehrere Seeds vergleichen, in
derselben Sitzung gemessen.

## Was ich ausdrücklich **nicht** vorschlage

- **Keine Sperre für die Erklärung.** Der Sättigungsabschlag wirkt ab der ersten Runde; wer ihn nicht
  sieht, wird für eine unsichtbare Regel bestraft. Verkauft werden Übersicht und Vorausschau, nie die
  Ursache.
- **Kein globaler Skill.** Je Ware zu rechnen ist der ganze Reiz: Es erzwingt eine Wahl und spielt mit
  der Sättigung zusammen. Ein globaler Wert wäre bloß eine Machtkurve.
- **Keine Fertigkeit für die KI.** Sie produziert nicht über diesen Weg; ihr eine zu geben hieße, eine
  Wirtschaft zu erfinden, die es nicht gibt.
