# E2E-Messwerte: Historie

Ausgelagert aus `CLAUDE.md`, damit dort nur die aktuell gültigen Zahlen und die Methodik stehen. Hier
liegt die vollständige Kette — jede Schicht entwertet die vorherige, und genau das ist der Grund, warum
sie nicht in die Projektübersicht gehört.

**Lies das hier nur, wenn du nachvollziehen willst, warum sich eine Zahl geändert hat.** Für die Frage
„was ist der aktuelle Stand" ist `CLAUDE.md` zuständig.

Alle Läufe, wenn nicht anders vermerkt: `--jahre=15 --spieler=1 --ohne-aktionen`.

---

## Schicht 1 — vor dem Handelsbalancing

Feste Stättenzahl, kein Sättigungspreis, Warenkreislauf lief nicht.

| Messung | Wert |
|---|---|
| Direkt gegen die Lib, Stättenzahl-Vergleich | +4 979 gegen +33 311 |
| Durch die Client-Bildschirme, bis zu 14 Stätten | +64 175 bis +80 088 |

**Entwertet**, weil keiner dieser Läufe mit drehendem Warenkreislauf lief: `RohBedarfAktRundenEnde`
wurde vom Godot-Client damals gar nicht aufgerufen. Der Stättenzahl-Vergleich lässt sich aus den Daten
jener Zeit auch nicht ehrlich nachrechnen.

## Schicht 2 — nach Sättigungspreis und Rundenende-Aufrufen

Vier Seeds, gleiche Schalter:

| Seed | 1234 | 42 | 2023 | 4711 |
|---|---:|---:|---:|---:|
| Taler | +19 882 | +26 292 | +39 633 | +90 822 |

Band grob **+20 000 bis +90 000**, Mittel ~44 000 gegen ein Vorprojekt-Mittel von ~72 000. Der Ausreißer
4711 ist überwiegend Spielzeit statt Ertrag — Schuldturmjahre strecken ihn auf 19 gespielte Jahre gegen
16 bei 1234; je gespieltem Jahr liegen die vier bei 1 243 / 1 643 / 2 331 / 4 780.

Eine Zwischenmessung derselben Seeds lag bei +38 204 bis +58 519, wobei zwei Wiederholungen von 4711 bei
identischer Eingabe um 3 861 Taler auseinanderlagen. Seit der Behebung des bedingten Würfelwurfs
reproduzieren gleiche Seeds wieder exakt (4711 zweimal: 90 822 Taler, 877 Klicks).

Auf Basis dieser Zahlen wurden die Balancing-Konstanten bewusst **nicht** angefasst.

## Schicht 3 — nach dem wiederhergestellten KI-Jahreswechsel

`KIAktionenDurchfuehren` in `Kontor.cs` fehlte bis dahin. Der Aufruf zieht rund **300 000 Zufallszahlen
pro Jahr** (390 KIs × 390 Beziehungen) und formt den Zufallsstrom ab dem ersten Zug um — kein Seed behält
sein altes Ergebnis.

Gemessen danach: Seed 1234 ergibt **+68 392**, wo dieselbe Invocation vorher +19 882 lieferte.

Eine eigene Grundlinie ist absichtlich noch nicht festgeschrieben; sie sollte wie das Band in Schicht 2
über mehrere Seeds entstehen.

## KI-Aggressivität (`--aggressivitaet=N`)

**Vor dem Handelsbalancing gemessen**, drei Seeds × 15 Jahre × 2 Spieler. Jede Einstellung von 1 bis 100
spielt alle Jahre durch und endet mit Exit 0.

| Einstellung | 1 % | 50 % | 100 % |
|---|---:|---:|---:|
| Klicks (Mittel) | 1 743 | 1 965 | 2 564 |

Was mit der Einstellung steigt, ist die **Dialoglast**, nicht die Härte des Ergebnisses: Das Endvermögen
wird von Seed-Rauschen beherrscht, die Streuung innerhalb einer Einstellung reichte von +7 379 bis
−62 073 und übertrifft den Abstand zwischen den Einstellungen bei Weitem.

Bei 100 % war der Lauf damals nicht mehr seed-reproduzierbar: Bei 50 % ergaben zwei Läufe desselben Seeds
identische Taler (nur der Klickzähler wich um ±2 ab), bei 100 % divergierten ganze Ergebnisse — mehr
interaktive Ereignisse geben dem Frame-Timing des Treibers mehr Angriffsfläche.
