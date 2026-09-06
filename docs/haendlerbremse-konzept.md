# Konzept: Eine Bremse für den reinen Händler

**Stand:** 2026-09-06 (Ämterweg nachgemessen) · **Betrifft:** eine Regeländerung in `Conspiratio.Lib` (`AbrechnungsManager`).
Nicht umgesetzt. Alle Zahlen sind mit `Conspiratio.Lib.Harness` gemessen, nicht geschätzt.

## Das Problem in einem Satz

Wer die Adelsleiter geht, wird gebremst — wer sie ignoriert, häuft ungebremst an.

Gemessen über 40 Jahre, 14 Exportlinien, drei Seeds:

| Weg | Endvermögen (Median) | Verlauf |
|---|---|---|
| reiner Händler | 1 961 598 | steigt linear um ~50 000 im Jahr, flacht nie ab |
| Adelsweg **ohne** Ämter | 843 121 | steigt bis ~1 055 000 (Jahr 22), kippt danach nach unten |
| Adelsweg **mit** Ämtern | **2 452 562** | steigt weiter, und schneller als der Händler |

Die Hofhaltung des Herzogs kostet 50 000 im Jahr — genau das, was der ausgereizte Betrieb netto
verdient. Deshalb kippt die mittlere Kurve. Der Händler zahlt sie nie: Titel verlangen Wohnsitze und
Stützpunkte, und beides ist freiwillig.

**Die dritte Zeile ist erst später dazugekommen und ändert das Bild.** Titel sind
Zugangsvoraussetzung für Ämter, und der Regent zahlt 50 000 Taler im Jahr — exakt so viel, wie die
Hofhaltung des Herzogs kostet. Wer den Weg zu Ende geht, ist damit nicht ärmer als der Händler,
sondern reicher. Was unten über die Kalibrierung steht, gilt deshalb nur gegen die **mittlere** Zeile;
die Einordnung am Ende des Dokuments zieht die Folgerung.

## Warum die vorhandene Bremse nicht greift

**Der Unterhalt ist bereits progressiv** — die n-te Werkstatt kostet das n-Fache, 84 Werkstätten
kosten 357 000 im Jahr. Der Kommentar in `AbrechnungsManager` nennt ihn ausdrücklich „die Bremse
selbst".

Nur bemisst er sich am **Besitz an Werkstätten** (`HumSpieler.ZaehleWerkstaetten`), und genau daran
wächst ein Händler nicht. Der gemessene Betrieb:

| | |
|---|---|
| Werkstätten | **14** |
| Produktionsstätten | **646** |
| Arbeiter | 1 360 |
| Unterhalt | **10 500 Taler** (100 × 14 × 15 / 2) |

Gewachsen wird *innerhalb* der Werkstatt, über Stätten und Arbeiter — und die kosten linear
(Betriebskosten, Löhne). Erlös linear, Kosten linear, Marge konstant und positiv: daraus folgt die
gerade Linie, die nie abflacht. **Die Progression sitzt an der falschen Größe.**

## Warum nicht am Sättigungsabschlag

Der naheliegende Gedanke — den 50-Prozent-Deckel anheben, damit Überproduktion sich selbst bestraft —
trifft den Falschen. Gemessen bleibt der Abschlag im Exportbetrieb bei **10 bis 21 %**, weil der
Händler die Zielstadt wechselt, sobald eine sättigt. Der Deckel ist gar nicht die bindende Grenze.

Er greift beim Zuschütten eines *einzelnen* Marktes (dort erreicht er 50 % binnen zweier Jahre), nicht
beim breit aufgestellten Betrieb. Eine Anhebung wäre also eine Regeländerung an einer Stelle, die den
gemessenen Fall nicht berührt.

## Vorschlag: Der Unterhalt bemisst sich an der Kapazität

Ein progressiver Zuschlag auf die **Gesamtzahl der Produktionsstätten** des Betriebs: Die n-te Stätte
kostet das n-Fache eines Tausendstels einer Grundeinheit.

```
Kapazitätsunterhalt = Stätten × (Stätten + 1) / 2 × Grundeinheit / 1000
```

Bei 646 Stätten und Grundeinheit 150 sind das **31 347 Taler im Jahr**.

**Die Form musste gemessen werden, geraten hätte ich falsch.** Der erste Entwurf ließ die Progression
*je Linie* laufen (die n-te Stätte einer Werkstatt kostet das n-Fache). Das war viel zu steil: Schon
eine Grundeinheit von 2 Talern drückte das Endvermögen von 1,96 Millionen auf 56 000, bei 5 war der
Betrieb bankrott. 46 Stätten je Linie zu quadrieren trifft härter als die 14 Werkstätten, an denen der
heutige Unterhalt hängt. Über den ganzen Betrieb gerechnet und mit feinerer Einheit ist die Kurve
steuerbar.

## Die Kalibrierung

40 Jahre, 14 Exportlinien, fünf Seeds, Median Endvermögen:

| Grundeinheit | Abgabe/Jahr | Endvermögen | |
|---|---|---|---|
| 0 | 0 | 1 961 598 | heute |
| 60 | 12 538 | 1 460 078 | |
| 120 | 25 077 | 958 518 | |
| **130** | **27 167** | **874 918** | **empfohlen** |
| 150 | 31 347 | 707 718 | |
| 180 | 37 616 | 456 958 | |
| 240 | 50 155 | 0 | bankrott |

**Empfohlen: 130.** Die Begründung ist ausdrücklich *kein* Gleichstand, sondern ein Vorsprung für
den Händler.

Titel sind im Spiel keine Zierde. `GetMinTitelStadtEbene`, `GetMinTitelLandEbene` und
`GetMinTitelReichsEbene` machen sie zur **Zugangsvoraussetzung für Ämter** — und Ämter bringen
Amtseinkommen, Privilegien und eigenes Ansehen. Dazu zählt `GetAnsehenGesamt()` den `BonusAnsehen` des
Rangs hinzu (Baron +75, Graf +100), was unmittelbar in die Gerichtsverhandlung eingeht. Wer Titel
kauft, tauscht also Geld gegen Macht. Bliebe das Endvermögen beider Wege gleich, wäre der Adelsweg
strikt besser: gleich viel Geld und obendrein Einfluss.

Alle Wege mit Abgabe 130 gemessen, 40 Jahre, fünf Seeds:

| Weg | Endvermögen (Median) |
|---|---|
| reiner Händler | 874 918 |
| Adelsweg ohne Ämter | 367 734 |
| Adelsweg **mit** Ämtern | **1 235 506** |

Gegen den Adligen **ohne** Ämter behält der Händler das Zweieinhalbfache — so weit trägt die
Begründung. Gegen den Adligen **mit** Ämtern liegt er 41 % zurück, und die Abgabe vergrößert diesen
Rückstand sogar: ohne sie sind es 25 %. Sie wirkt auf beide Wege gleich, aber der Adlige hat eine
zweite Einnahmequelle, die sie nicht berührt.

Die Kurve erreicht dabei einen Ruhepunkt, statt ewig zu steigen: Anstieg bis rund 877 000 im Jahr 15,
danach Pendeln zwischen 630 000 und 670 000. Die Abgabe beträgt 27 167 Taler im Jahr.

**Der Einsteiger merkt nichts.** Gemessen an einem Betrieb mit einer Stadt und zwei Stätten über
15 Jahre: Endvermögen **24 579 mit und ohne Abgabe, auf den Taler gleich**. Zwei Stätten kosten
rechnerisch 0,45 Taler, ganzzahlig also nichts. Die Progression trifft ausschließlich den Großbetrieb —
das war die Bedingung, unter der der Unterhalt überhaupt als Geldsenke taugt.

## Warum diese Form und keine andere

**Sie ist bekämpfbar.** Wer seine Stätten gut auslastet, wenige gut genutzte statt vieler halbleerer
betreibt und sich spezialisiert, zahlt weniger. Ein fester Prozentsatz auf das Vermögen wäre das nicht —
und genau den hattest du beim Moneysink abgelehnt.

**Sie passt zur vorhandenen Designsprache.** Die Auslastungsbelohnung im `AbrechnungsManager` gibt es
schon, der Werkstatt-Kaufpreis staffelt bereits mit dem Besitz, und der Unterhalt ist bereits
progressiv. Der Vorschlag verschiebt die Progression nur auf die Größe, die tatsächlich wächst.

**Sie hat eine natürliche Obergrenze.** Der Arbeiterdeckel von 99 je Slot begrenzt die Stätten je Linie
ohnehin. Die Progression macht das Ausreizen teuer, statt es zu verbieten.

## Einschränkungen der Messung

**Der Händler der Harness passt sich nicht an.** Er produziert bei voller Kapazität weiter, auch wenn
die Abgabe ihn ruiniert — deshalb bricht Grundeinheit 240 den Betrieb, statt ihn zu verkleinern. Ein
Mensch würde Stätten abbauen oder die Auslastung verbessern, sobald die Abgabe beißt. **Die Messung
überzeichnet den Schaden**, der brauchbare Wert liegt also eher am oberen Rand des Korridors.

**Drei Seeds sind wenig.** Die Streuung zwischen ihnen beträgt hier rund 350 000 Taler; die Rangfolge
der Grundeinheiten ist über alle Seeds einheitlich, die Einzelwerte sind es nicht.

**Wie lange man ein Amt hält, ist die entscheidende Unbekannte.** Die Harness *übernimmt* Ämter
über `CheatManager.UebernehmeAmt`; im Spiel gewinnt man sie in einer Wahl und verliert sie durch
Amtsenthebung. Die beiden gemessenen Adelszeilen sind deshalb eine **Klammer**, keine Aussage:
367 734 wenn man nie ein Amt hält, 1 235 506 wenn man durchgehend das höchste hält. Wo der wirkliche
Wert darin liegt, hängt allein an der Amtsdauer — und die bildet die Harness nicht ab.

**Was die Ämter tragen, ist gemessen.** Der Unterschied zwischen beiden Adelszeilen beträgt
1 609 441 Taler über 40 Jahre, rund 40 000 im Jahr. Das Spitzenamt zahlt 50 000; die Leiter darunter
reicht von 700 (Ratsherr) über 8 000 (Vogt) und 20 000 (Finanzminister, Erzbischof) bis dorthin.

**Der Ansehensgewinn ebenfalls.** Am Ende eines Herzog-und-Regent-Laufs steht ein rohes Ansehen von
319 bis 332 — darin steckt der Ämterbonus, beim Regenten +100 — und ein Standesansehen von 469 bis
482, also zuzüglich der 150 des Herzogtitels. Zum Vergleich: Die Schwellen des Gerichts liegen bei 80
und 30. Der Standesbonus gilt ausdrücklich nur dort, wo über eine *Person* geurteilt wird (Gericht,
Schuldenprozess), nicht in Wahlen.

**Eine Zahl in einer früheren Fassung dieses Dokuments war nicht belastbar.** Die Harness setzte den
Seed erst nach `CreateNewGame` und der Spielererstellung, die Welt entstand also ungeseedet. Der
Händlerweg war davon unbeeindruckt, der Adelsweg nicht: Dieselbe Kommandozeile lieferte über mehrere
Aufrufe 266 441, 367 734 und 187 274 Taler Median. Der Seed wird jetzt zweimal gesetzt, vor dem Aufbau
und wenn das Spiel steht; alle Zahlen hier stammen aus dem reparierten Stand.

## Einordnung: Was die Abgabe leistet — und was nicht

Sie tut, wofür sie gebaut wurde: Die Kurve des Händlers erreicht einen Ruhepunkt, statt ewig zu
steigen, und der Einsteiger merkt nichts davon. Das ist ein echter Spätspiel-Sink, und er ist
bekämpfbar.

Sie leistet **nicht**, was die ursprüngliche Begründung ihr zuschrieb. Die stützte sich darauf, dass
der Adelsweg der finanziell schwächere sei und der Händler deshalb einen Vorsprung brauche. Mit
Ämtern gemessen ist der Adelsweg der **stärkere** — er bringt 25 % mehr als der reine Handel und
zusätzlich Ansehen, Gerichtsstand und Privilegien. Die Abgabe vergrößert diesen Abstand auf 41 %,
weil sie nur die Produktionskapazität trifft, die beide gleichermaßen haben.

Wer den Händlerweg finanziell attraktiver machen will, muss deshalb am Amtseinkommen ansetzen, nicht
am Unterhalt. Bevor daran jemand dreht, wäre allerdings die Amtsdauer zu messen: Wenn ein Spieler den
Regenten im Spiel nur wenige Jahre hält statt vier Jahrzehnte, ist der gemessene Vorsprung ein
Zerrbild. Das ist der nächste sinnvolle Ausbau der Harness — Wahlen und Amtsenthebungen mitlaufen
lassen.

## Wenn umgesetzt

- Neue Konstante in `AbrechnungsManager` neben `GrundunterhaltProWerkstatt`, eigener Posten im
  `AbrechnungsErgebnis`, damit der Spieler ihn im Abrechnungsdialog sieht statt ihn zu suchen.
- Tests in der Lib: Einsteiger zahlt nichts, Progression steigt quadratisch, Auslastung senkt sie.
- Zweisprachiger CHANGELOG-Eintrag; **Spielstände brauchen keine Migration** (eine neue Konstante,
  kein neues Feld auf dem Spieler).
- Danach mit der Harness gegenmessen — dieselbe Konfiguration, damit die Zahlen dieses Dokuments
  vergleichbar bleiben.
