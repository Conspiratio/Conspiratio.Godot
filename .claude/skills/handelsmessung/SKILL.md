---
name: handelsmessung
description: Fährt die E2E-Messmatrix über vier feste Seeds, isoliert den Handel von den Zufallsaktionen und gibt das Ergebnis als Band aus - die einzige Form, in der diese Zahlen aussagekräftig sind.
disable-model-invocation: true
---

# Handelsmessung

Misst, was eine Änderung an der Wirtschaft bewirkt hat. Führe
`scripts/messen.sh [jahre]` aus (Vorgabe: 15 Jahre).

## Die Schalter und warum genau diese

```
--jahre=15 --spieler=1 --ohne-aktionen --seed=<N>
```

- **`--ohne-aktionen`** schaltet die zufälligen Kontor-Aktionen ab. Deren Streuung erreicht
  Zehntausende Taler und überdeckt jeden Handelseffekt.
- **Nicht `--ohne-bereiche`** — das überspringt auch den Heimatstadtbesuch, und der Lauf meldet dann
  schlicht null Handel.
- **`--spieler=1`**, damit keine Marktkonkurrenz hineinspielt.

Seeds: 4711, 1234, 2023, 42. Vier Läufe à einige Minuten.

## Wie das Ergebnis zu lesen ist

**Ein Band, nie eine einzelne Zahl.** Der Seed beherrscht alles andere: Zwischen dem schwächsten und
dem stärksten Lauf lagen zuletzt Faktor vier. Unterschiede unter ~10 000 Talern sind nichts.

**Nicht auf Spieljahre normieren.** Der Treiber spielt immer exakt `--jahre` Züge, aber `Gespielte
Jahre` kann höher liegen — ein Schuldturmjahr kostet einen Zug, ohne einen zu spielen. Durch
Spieljahre zu teilen normiert auf einen Zähler, der nur die zuglosen Jahre zählt, und drückt genau
den Lauf, den man erklären will.

**Nie über eine Änderung hinweg vergleichen, die den Zufallsstrom umformt.** Verbraucht eine Änderung
zusätzliche Zufallszahlen aus `SW.Statisch.Rnd`, verschiebt sie jeden späteren Wurf — derselbe Seed
spielt dann nicht mehr dieselbe Partie. In dem Fall ist ein Vorher/Nachher-Vergleich wertlos; belege
die Wirkung stattdessen auf einem zufallsfreien Pfad (ein Konsolen-Harness ohne `Rnd`) und nimm die
Seeds nur für die Richtung.

## Wohin die Zahlen gehören

Die Historie steht in `docs/e2e-messwerte.md`, nach Schichten geordnet und mit dem Grund der
Entwertung. **Nicht** in `CLAUDE.md` — dort stehen nur die aktuell gültigen Zahlen und die Methodik.
Trägst du eine neue Messreihe ein, vermerke, wodurch die vorige entwertet wurde.
