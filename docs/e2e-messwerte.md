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

Diese Einzelzahl ist inzwischen ebenfalls entwertet (siehe Schicht 4): Paket A/B hat den Zufallsstrom
erneut umgeformt, derselbe Seed liefert heute 41 805.

## Schicht 4 — Grundlinie nach Paket A/B (Lib 4.4.1)

Erstmals über ein Band statt über Einzelzahlen festgeschrieben. **Zehn Seeds**, Standard-Invocation:

| Seed | 7 | 2023 | 42 | 1234 | 31337 | 555 | 90210 | 13 | 4711 | 2718 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Taler | 28 514 | 36 919 | 38 225 | 41 805 | 42 899 | 49 416 | 50 812 | 52 100 | 53 542 | 102 874 |

Band **+29 000 bis +103 000**, Mittel 49 710, Median 46 157, **0 von 10 negativ**, alle Exit 0.
Ohne den Ausreißer 2718 läge das Mittel bei 43 804 — also praktisch auf dem Schicht-2-Wert. Gegen das
Vorprojekt-Mittel von ~72 000 liegt der Stand weiterhin deutlich darunter.

### Warum die Schalter wichtiger sind als das Balancing

Anlass der Messung war die Sorge, Paket A/B habe das Spätspiel zu hart gemacht — gestützt auf zwei
Läufe, die negativ endeten. Das 2×2 über die beiden Schalter löst das auf (Endvermögen Spieler 1,
15 Jahre, dieselben Seeds):

| | ohne Aktionen | mit Aktionen |
|---|---:|---:|
| **1 Spieler** | **+49 710** (0 von 10 negativ) | **−19 033** (6 von 6 negativ) |
| **2 Spieler** | **+15 147** (1 von 12 negativ) | **−11 798** (12 von 12 negativ) |

(Die Mittel der Zwei-Spieler-Spalten über alle Spielerergebnisse, nicht nur Spieler 1.)

**Die Aktionen drehen das Vorzeichen, nicht das Balancing.** Belege aus denselben Logs:
- Die Handelsmengen brechen mit Aktionen um ein bis zwei Größenordnungen ein (Verkauf vor Ort 241 bis
  300 bei den Seeds 42/1234/2023, gegen konstant 5 000 bis 8 500 ohne Aktionen). Der Treiber verbraucht
  sein Klickbudget in Zufallsaktionen und spielt die Handelsrunde nicht mehr zu Ende.
- Die Klickzahlen steigen von 155–233 auf 1 602–3 373.
- `Gespielte Jahre` steigt auf 21–25 bei `--jahre=15`: lauter Schuldturmjahre, also Folge der Pleite,
  nicht ihre Ursache.

Die **Spielerzahl** dagegen wirkt echt: +49 710 auf +15 147 ohne Aktionen ist die Marktsättigung bei
geteilten Absatzmärkten — Spielmechanik, kein Messartefakt.

**Konsequenz für künftige Messungen:** Die Balancing-Grundlinie ist die Spalte *ohne Aktionen*. Ein Lauf
in Standardeinstellung misst den Treiber, nicht das Spiel; als Regressionssignal taugt er, als
Balancing-Aussage nicht.

### Zwei Sperren, die vor einer Spätspiel-Grundlinie liegen

Sechs Läufe über 40 Jahre mit `--spieler=1 --ohne-aktionen` schlugen **alle** mit Exit 1 fehl, vier
weitere mit `--spieler=2 --ohne-aktionen` ebenfalls. Zwei getrennte Ursachen:

1. **Der Treiber heiratet nie.** In `E2eTreiber.cs` kommt weder Brautwerbung noch Hochzeit oder Geburt
   vor. Ohne Ehe kein Erbe, also beendet der erste Todesfall die Dynastie (`Kontor.cs`, Pfad
   `testament.SpielVorbei`). Im Verbose-Lauf ist das die letzte Zeile vor dem Ende: ein einziger
   `SpielerTodDialog` in 24 Jahren. **Mit `--ohne-aktionen` kommt deshalb kein Lauf über ~25 Jahre
   hinaus**, unabhängig von der Spielerzahl. Der Wochenlauf entgeht dem nur, weil er *mit* Aktionen
   läuft und der Treiber dort gelegentlich zufällig in eine Brautwerbung stolpert.
2. **Der Treiber kennt kein reguläres Spielende.** `_spielLaeuft` wird einmal auf `true` gesetzt und nie
   zurückgenommen; ein zu Ende gegangenes Spiel meldet er als Hänger („Der Kontor wurde im Jahr X nicht
   bedienbereit").

Bei drei der vier Zwei-Spieler-Läufe kam ein dritter Zustand dazu, der noch nicht aufgeklärt ist: ein
Mensch lebt noch (69 937 / 83 210 / 117 700 Taler), sein `Kontor` ist sichtbar und nimmt Eingaben —
und *gleichzeitig* ist `Mainmenu` sichtbar, der Zug lässt sich nicht mehr beenden. Dazu passt, dass
`Kontor.BeendeZug()` im Spielende-Zweig nur `HideAndDisableInput()` aufruft, obwohl der Kommentar dort
„Zurück ins Hauptmenü" behauptet; kein Skript blendet `Mainmenu` je wieder ein. Ob daraus im echten
Hot-Seat-Spiel ein sichtbarer Fehler wird, ist offen.

**Beide Sperren sind inzwischen behoben** (siehe Schicht 5); die Zahlen dieser Schicht stammen also
von einem Treiber, der nie heiratete. Und bei ~50 000 Talern nach 15 Jahren greift von Paket A/B
ohnehin nichts: Die Hofhaltung kostet erst mit Adelstitel spürbar, Gesetz #3 steht bei 2 bis 6 Mio.

## Schicht 5 — Treiber repariert: Ehe, Erbe, Spielende, Rücklage

Vier Änderungen am Treiber, die zusammengehören — hier in drei Punkten: Der Zufallsstrom verschiebt sich damit
erneut: **Schicht 4 ist als Vergleichsband entwertet.**

1. **Der Treiber wirbt um einen Ehepartner** (Kupplerin in der Kirche) und **bestimmt im Testament einen
   Erben**. Beides läuft immer mit, auch mit `--ohne-aktionen`, aus demselben Grund wie die
   Handelsrunde: Es ist Kern des Spiels, keine Zufallshandlung.
2. **Er erkennt ein reguläres Spielende**, statt es als Hänger zu melden.
3. **Die Rücklage der Handelsrunde wächst mit dem Betrieb**, statt bei festen 1 500 Talern zu stehen.

### Das Band

Zehn Seeds, `--jahre=15 --spieler=1 --ohne-aktionen`:

| Seed | 90210 | 2718 | 555 | 2023 | 4711 | 7 | 31337 | 42 | 13 | 1234 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Taler | 3 880 | 8 445 | 11 862 | 15 277 | 15 933 | 29 028 | 29 407 | 34 930 | 46 696 | 53 162 |

Band **+3 900 bis +53 000**, Mittel 24 862, Median 22 480, **0 von 10 negativ**.

**Die Sperre ist weg:** 40-Jahre-Läufe (`--spieler=1 --ohne-aktionen`) kommen erstmals durch — 40, 40
und 44 gespielte Jahre bei +191 550 / +168 067 / +82 005. Mit zwei Spielern ebenso: viermal genau
40 von 40 Jahren, sieben von acht Spielerergebnissen positiv, bis +285 980. Vorher endete jeder solche
Lauf nach 21 bis 33 Jahren mit erloschener Dynastie.

### Das 40-Jahre-Band — und warum es Paket A/B trotzdem nicht misst

Zehn Seeds, `--jahre=40 --spieler=1 --ohne-aktionen`, alle Exit 0, alle über 40 Jahre:

| Seed | 4711 | 13 | 90210 | 42 | 2023 | 31337 | 1234 | 7 | 2718 | 555 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Taler | 37 521 | 94 185 | 99 782 | 110 307 | 121 357 | 128 998 | 153 716 | 158 828 | 161 291 | 178 117 |

Band **+38 000 bis +178 000**, Mittel 124 410, Median 125 177, **0 von 10 negativ**.

**Der Treiber läuft in ein Fließgleichgewicht.** Seed 42 in den letzten acht Jahren: 139 763 →
125 049 → 151 351 → 122 905 → 129 021 → 100 641 → 126 137 → 110 307. Kein Aufwärtstrend mehr, nur der
Sägezahn zwischen Export- und Verkaufsjahren; Einnahmen decken die Kosten und sonst nichts.

Die Ursache ist die Treiberstrategie, nicht das Spiel: Die Handelsrunde bespielt **einen** Werkstattplatz
in **einer** Stadt (`werkstattNr` ist die erste gefundene Werkstatt, konfiguriert wird nur Slot 0), und
die Stättenzahl deckelt `GetMaxArbeiterAnzahl()` bei 99 Arbeitern. Ein Betrieb dieser Größe trägt
rund 125 000 Taler und dann nichts mehr.

**Damit bleibt die Ausgangsfrage offen — aber aus einem klaren Grund.** Die Spätspielbremsen aus
Paket A/B greifen weit oberhalb dieses Plateaus:

| Bremse | Greift ab | Treiber erreicht |
|---|---|---|
| Gesetz #3 „Maximale Taler" | 2 bis 6 Mio. | 178 000 (Faktor 16 bis 34 darunter) |
| Werkstatt-Staffel jenseits `MaxSteigerungsstufen` | 21. Betrieb | 1 Betrieb |
| Hofhaltung (Herzog) | 50 000 im Jahr | nur bei hohem Titel |

Ein E2E-Lauf kann Paket A/B also **prinzipiell nicht** bewerten, egal wie lang er ist — der Engpass ist
die Anzahl der Betriebe, nicht die Spieldauer. Wer das messen will, braucht entweder einen Treiber, der
in mehreren Städten Wohnsitze und Werkstätten kauft, oder eine Konsolen-Harness, die einen reichen
Spieler direkt aufbaut und nur die Abrechnung durchrechnet. Letzteres ist deutlich billiger und ohne
Zufallsstrom auch aussagekräftiger.

### Warum die Rücklage mitwachsen muss — die Diagnose

Die ersten drei Änderungen allein ergaben ein **zweigipfliges** Band: −49 266 bis +60 466, drei von
zehn Läufen negativ. Der Jahresvergleich eines Absturz-Seeds gegen einen gesunden zeigte, woran es lag:

| Jahr | Spirale (2718) | Gesund (7) |
|---|---:|---:|
| 1600–1603 | −1 150 … −2 895 | −80 … −2 741 |
| ab 1605/1608 | −15 733 → −35 159 → −41 444 | −685 … +6 199 |
| Ausbruch | — | 1612: +25 145 (Verkauf 2 191) |
| Ende | −37 872 | +50 072 |

- **Beide** Läufe sind die ersten sieben Jahre negativ. Das war die Strategie, nicht das Pech: Die
  Handelsrunde fuhr das Vermögen planmäßig auf eine feste Rücklage von 1 500 Talern herunter,
  unabhängig von der Betriebsgröße, und die Jahresabrechnung drückte es danach unter null.
- **Der Schuldturm ist ein absorbierender Zustand.** Ab 1606 springt Seed 2718 je Schleifendurchlauf
  zwei Jahre — lauter Kerkerjahre — und der Lokalverkauf steht ab 1605 dauerhaft auf 0. Kerker → kein
  Zug → kein Handel → kein Geld → Kerker.
- Der Unterschied zum gesunden Lauf war **reines Timing**: Seed 7 kam 1612 zufällig über die Schwelle.

Zwei naheliegende Erklärungen sind dabei **widerlegt** worden, beide durch eine Gegenprobe:

- *Nicht die Duelle.* Sie lagen nahe, weil bei Seed 1234 879 von 1 197 Dialogklicks im Duelldialog
  stecken und bei Seed 2718 sogar 1 345. Der positive Seed 7 hat **3 283** — mehr als beide
  Absturz-Seeds — bei +50 072 Talern.
- *Nicht der Erbfall.* In beiden 15-Jahre-Läufen gibt es **null** Todesfälle.

Die Ehe war also nicht die Ursache, sondern nur der Tropfen: Die Kupplerin nahm ihren Lohn aus genau dem
Polster, das die Handelsrunde als Reserve stehen gelassen hatte.

**Der Fix** (`BerechneRuecklage`) spiegelt die Formel des `AbrechnungsManager` — Arbeiter mal
`GetWSArbeiterpreis` plus Stätten mal `GetWSEinzelpreis` — und verdoppelt sie; der Aufschlag deckt die
Kostenblöcke ab, die sich ohne Verbuchen nicht vorausberechnen lassen. Zusätzlich wirbt der Treiber
erst ab dem doppelten Rücklagenbetrag. Der Faktor 2 ist eine **Heuristik**, keine Herleitung — belegt
ist nur ihre Wirkung: Spannweite von 109 732 auf 49 282 halbiert, Katastrophenschwanz weg, Median
nahezu unverändert (27 376 → 22 480).

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
