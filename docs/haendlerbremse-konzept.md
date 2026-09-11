# Konzept: Eine Bremse für den reinen Händler

**Stand:** 2026-09-08 · **Betrifft:** `Conspiratio.Lib` (`AbrechnungsManager`).
**Umgesetzt** als `KapazitaetsunterhaltProMille = 140`, samt Anzeige in der Jahresabrechnung des
Clients. Alle Zahlen sind mit `Conspiratio.Lib.Harness` gemessen, nicht geschätzt.

Das Dokument behält den Weg dorthin: Zwei Zwischenstände (Grundeinheit 130, Kalibrierung auf
Gleichstand) haben sich als falsch begründet erwiesen, und beide Male hat erst eine weitere Messung
das gezeigt. Wer die Grundeinheit später verstellt, sollte wissen, woran sie hängt.

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
| 130 | 27 167 | 874 918 | |
| **140** | **29 257** | **791 318** | **umgesetzt** |
| 150 | 31 347 | 707 718 | |
| 180 | 37 616 | 456 958 | |
| 240 | 50 155 | 0 | bankrott |

**Umgesetzt: 140.** Die Begründung ist ausdrücklich *kein* Gleichstand, sondern ein Vorsprung für
den Händler.

> **Nachgetragen: Diese Tabelle beschreibt einen Händler, der nicht reagiert.** Der Harness fuhr
> den Betrieb damals immer auf Anschlag, auch während die Abgabe ihn ruinierte. Mit `--adaptiv`
> — er drosselt nach einem Verlustjahr — senkt dieselbe Abgabe das Endvermögen nur von
> 1 876 208 auf **1 144 547** statt auf 791 318, die Bremse wirkt also um rund ein Drittel
> schwächer als hier gemessen. **Die Wahl von 140 bleibt davon unberührt**, denn sie wurde am
> Abstand zum Adelsweg begründet, und der bleibt bestehen (siehe unten). Wer die Grundeinheit
> neu begründen will, sollte aber am anpassungsfähigen Händler messen: Der starre zeigt eine
> Wirkung, die im Spiel niemand erleidet, der aufpasst.
>
> **Und noch etwas steht unter diesen Zahlen:** Sie gelten für einen Händler auf
> **Werkstattplatz 1** jeder Stadt. Der trägt zwar die Hauptproduktion und damit die höchste
> Effizienz, aber es sind Waren der Stufe 1. Mit `--startplatz` gemessen (40 Jahre, drei
> Seeds, Mediane): Platz 1 bringt 791 318, Platz 4 dagegen 2 741 260 und Platz 6 2 751 310 —
> der Preis der höheren Warenstufen schlägt den Effizienzvorteil um mehr als das Dreifache.
> Die Grundeinheit 140 ist also an der **ungünstigsten Warenwahl des Spiels** geeicht.
>
> **Eine Neueichung am klugen Händler wurde versucht und ist gescheitert** — und das ist selbst
> das Ergebnis. Wer je Stadt den besten Platz wählt und drosselt, zahlt 3 849 Taler Abgabe bei
> 550 000 Gesamtkosten, also 0,7 %: Die Abgabe hängt an der **Zahl der Stätten** und wächst
> quadratisch darin, und hochwertige Ware erzielt denselben Erlös mit einem Bruchteil davon.
> Eine härtere Grundeinheit macht ihn sogar reicher (140 → 2 633 870, 400 → 2 832 410,
> 800 → 2 852 762), weil sie den drosselnden Händler früher auf seinen günstigeren
> Betriebspunkt zwingt; eine Gewichtung nach Warenstufe ändert nichts (2 909 668). Und der
> Adelsweg liegt bei dieser Warenwahl mit 2 800 078 **gleichauf** — die Begründung oben, der
> Händler solle das Doppelte halten, trägt dort nicht mehr.
>
> Was bliebe, wenn das Spätspiel des klugen Händlers zu reich erscheint: die **Marktsättigung**
> härter stellen (`Stadt.MaxAbschlagProzent` oder `AbschlagJeBedarfsjahrProzent`) — sie ist es,
> die ihn heute bei 2,6 bis 2,9 Millionen hält —, das **Vermögensgesetz** enger fassen, dessen
> Grenze mit 2 bis 6 Millionen ohnehin in diesem Bereich liegt, oder den Kapazitätsunterhalt an
> den **Erlös** statt an die Stätten hängen. Alle drei sind Regeländerungen, keine Eichung.

Titel sind im Spiel keine Zierde. `GetMinTitelStadtEbene`, `GetMinTitelLandEbene` und
`GetMinTitelReichsEbene` machen sie zur **Zugangsvoraussetzung für Ämter** — und Ämter bringen
Amtseinkommen, Privilegien und eigenes Ansehen. Dazu zählt `GetAnsehenGesamt()` den `BonusAnsehen` des
Rangs hinzu (Baron +75, Graf +100), was unmittelbar in die Gerichtsverhandlung eingeht. Wer Titel
kauft, tauscht also Geld gegen Macht. Bliebe das Endvermögen beider Wege gleich, wäre der Adelsweg
strikt besser: gleich viel Geld und obendrein Einfluss.

**Der Endstand, mit allem was inzwischen gebaut ist** — Abgabe 140, echte Wahlen und
Amtsenthebungen, Ansehensverfall. 40 Jahre, 14 Exportlinien, fünf Seeds:

| Weg | Endvermögen (Median) | Rang | Amt | Standesansehen |
|---|---|---|---|---|
| reiner Händler | **791 318** | Ritter | keines | 60 |
| Adelsweg | 384 239 | Graf bis Herzog | Zollmeister bis Regent | 182 – 324 |

Mit einem Händler, der drosselt (`--adaptiv`), liegen beide höher — **das Verhältnis aber
bleibt**, und darauf kam es an:

| Weg | starr | adaptiv |
|---|---:|---:|
| reiner Händler | 791 318 | 1 144 547 |
| Adelsweg | 384 239 | 493 866 |
| **Vorsprung des Händlers** | **2,06×** | **2,32×** |

Der Adlige hält in beiden Fällen sein Standesansehen von 182 bis 324 und Ämter bis zum Regenten.
Die Asymmetrie, um die es hier geht, ist also robust gegen die Spielweise.

Der Händler hält gut **das Doppelte an Talern**, der Adlige das **Drei- bis Fünffache an
Standesansehen** und als einziger ein Amt — mit Amtseinkommen, Privilegien und Stimmrecht. Das ist
die beabsichtigte Asymmetrie.

Die Kurve des Händlers erreicht dabei einen Ruhepunkt, statt ewig zu steigen. Die Abgabe beträgt bei
646 Stätten 29 257 Taler im Jahr.

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

**Die Amtsdauer war die entscheidende Unbekannte, und sie ist inzwischen gemessen.** Eine
Zwischenfassung dieses Dokuments schloss, der Adelsweg sei der *stärkere* — 25 % mehr als der reine
Handel. Das galt nur, weil die Harness sich Ämter damals per Cheat nahm und das höchste vier
Jahrzehnte lang hielt. Mit echten Wahlen, Bewerbungen und Amtsenthebungen (`--wahlen`) hält ein
Spieler am Ende alles zwischen dem Zollmeister (2 000 im Jahr) und dem Regenten (50 000) — und liegt
damit finanziell rund die Hälfte unter dem Händler.

Die Klammer von damals — 367 734 ohne Amt bis 1 235 506 mit Dauer-Spitzenamt — ist damit auf einen
Wert zusammengezogen: **384 239**. Er liegt nahe am unteren Ende, und das ist die eigentliche
Erkenntnis: Ein Spitzenamt ist erreichbar, aber nicht zu halten.

## Was umgesetzt wurde

- `AbrechnungsManager.KapazitaetsunterhaltProMille = 140`, eigener Posten
  `AbrechnungsErgebnis.Kapazitaetsunterhalt`, fünf Tests in `KapazitaetsunterhaltTests`.
- Der Godot-Abrechnungsdialog führt ihn zwischen Unterhalt und Hofhaltung. Das Positionsraster trägt
  dafür eine 15. Zeile; Pergament und Schaltfläche mussten um 45 px wachsen, weil „Weiter" sonst
  unmittelbar auf der letzten Zeile saß — im Bildnachweis geprüft, nicht geschätzt.
- Spielstände brauchen keine Migration: eine Konstante, kein neues Feld auf dem Spieler.

**Ein Folgebefund aus derselben Messreihe.** Das Ansehen wuchs linear mit der Spieldauer — ein
schlichter Ritter ohne Amt kam nach 40 Jahren auf ein Standesansehen von 205, weil die
Auslastungsbelohnung jedes Jahr bis zu 5 Punkte auf ein reines Konto buchte. Die Gerichtsschwellen
(80 und 30) waren danach bedeutungslos. Sie anzuheben wäre falsch gewesen: Der Median der 390
KI-Spieler liegt über 40 Jahre unverändert bei 29, die Schwellen sitzen also richtig — inflationiert
war allein der Mensch, denn `PermaAnsehen` gibt es nur auf `HumSpieler`. Seit
`AnsehensverfallProzent = 10` verblaßt Ruhm jährlich, und die Werte pendeln sich ein: KI-Median 29,
Händler 60, Herzog mit Amt gut 300.

Der Verfall wirkt auf den Adelsweg zurück, weil Ansehen in die Annahme von Stützpunktangeboten und in
die Stimmen der KI eingeht. Die Adelszahlen oben sind deshalb mit ihm gemessen und nicht mit den
älteren vergleichbar.
