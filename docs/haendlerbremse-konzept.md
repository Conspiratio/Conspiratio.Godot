# Konzept: Eine Bremse für den reinen Händler

**Stand:** 2026-09-05 · **Betrifft:** eine Regeländerung in `Conspiratio.Lib` (`AbrechnungsManager`).
Nicht umgesetzt. Alle Zahlen sind mit `Conspiratio.Lib.Harness` gemessen, nicht geschätzt.

## Das Problem in einem Satz

Wer die Adelsleiter geht, wird gebremst — wer sie ignoriert, häuft ungebremst an.

Gemessen über 40 Jahre, 14 Exportlinien, drei Seeds:

| Weg | Endvermögen (Median) | Verlauf |
|---|---|---|
| reiner Händler | **1 961 598** | steigt linear um ~50 000 im Jahr, flacht nie ab |
| Adelsweg bis zum Herzog | 843 121 | steigt bis ~1 055 000 (Jahr 22), kippt danach nach unten |

Die Hofhaltung des Herzogs kostet 50 000 im Jahr — genau das, was der ausgereizte Betrieb netto
verdient. Deshalb kippt seine Kurve. Der Händler zahlt sie nie: Titel verlangen Wohnsitze und
Stützpunkte, und beides ist freiwillig.

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

40 Jahre, 14 Exportlinien, drei Seeds, Median Endvermögen:

| Grundeinheit | Abgabe/Jahr | Endvermögen | |
|---|---|---|---|
| 0 | 0 | 1 961 598 | heute |
| 60 | 12 539 | 1 460 078 | |
| 120 | 25 078 | 958 518 | |
| **150** | **31 347** | **707 718** | empfohlen |
| 180 | 37 616 | 456 958 | |
| 240 | 50 155 | 0 | bankrott |

**Empfohlen: 150.** Die Begründung liegt in der Symmetrie: Der Adelsweg endet bei 843 121, eine
Grundeinheit zwischen 120 und 150 setzt den Händler in dieselbe Größenordnung. Damit ist keiner der
beiden Wege strikt besser, und beide werden gebremst.

Die Kurve flacht dabei nicht nur ab, sie erreicht einen Ruhepunkt: Sie steigt bis rund 778 000 im
Jahr 17, pendelt danach zwischen 440 000 und 500 000. Das ist die Form, die dem Adelsweg entspricht.

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

**Nicht gemessen:** wie sich die Abgabe auf einen Betrieb auswirkt, der beide Wege geht — Adel *und*
Handel. Dort addieren sich Hofhaltung und Kapazitätsunterhalt, und das könnte zu viel sein.

## Wenn umgesetzt

- Neue Konstante in `AbrechnungsManager` neben `GrundunterhaltProWerkstatt`, eigener Posten im
  `AbrechnungsErgebnis`, damit der Spieler ihn im Abrechnungsdialog sieht statt ihn zu suchen.
- Tests in der Lib: Einsteiger zahlt nichts, Progression steigt quadratisch, Auslastung senkt sie.
- Zweisprachiger CHANGELOG-Eintrag; **Spielstände brauchen keine Migration** (eine neue Konstante,
  kein neues Feld auf dem Spieler).
- Danach mit der Harness gegenmessen — dieselbe Konfiguration, damit die Zahlen dieses Dokuments
  vergleichbar bleiben.
