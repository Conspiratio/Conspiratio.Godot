# Moneysink: Unterhalt, Hofhaltung und die Entwertung des Geldansehens

**Stand:** 2026-08-28 · **Betrifft:** Conspiratio.Lib (Kern) und Conspiratio.Godot (Ansicht)

## Warum

Erfahrene Spieler bemängeln, dass Conspiratio ab einem gewissen Talervermögen zu leicht wird. Paket A/B
(Lib 4.3.0/4.4.0) hat darauf mit der Hofhaltung und dem Vermögensgesetz geantwortet. Eine Messung der
Kostenseite über die Vermögensskala zeigt, dass das nicht trägt:

| Vermögen | 100k | 250k | 500k | 750k | 1,0M | 1,5M | 2,0M | 2,5M | 2,9M | 3,1M | 4M | 8M |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Jahreslast | 8,0 % | 10,0 % | 10,0 % | 6,7 % | 5,0 % | 3,3 % | 2,5 % | 2,0 % | **1,7 %** | **16,4 %** | 16,1 % | 15,5 % |

(Gemessen mit einer Wegwerf-Harness gegen Conspiratio.Lib 4.4.1, Einzelheiten unter „Messgrundlage".)

Zwei Befunde:

1. **Zwischen 500 000 und 2,9 Mio. Talern fällt die einzige laufende Belastung von 10 % auf 1,7 %** —
   Faktor sechs. Ursache ist strukturell: Die Hofhaltung ist bei 50 000 Talern gedeckelt, weil die Titel
   beim Herzog enden und `Adelstitel.GetJahresaufwand()` an der Titelschwelle hängt. Oberhalb des
   Herzogs kommt keine Schwelle mehr, also fällt die Last ungebremst.
2. **Der Übergang bei 3 Mio. ist eine Klippe**, kein Übergang: 1,7 % auf 16,4 % in einem Schritt.

Dazu ein dritter Befund aus derselben Messung: **Besitz kostet nichts.** `AbrechnungsManager` summiert
`Betriebskosten` über `produktionsslot.GetProduktionStaetten()`, also Stätten *in Produktion*. Dreißig
stillgelegte Werkstätten kosten null im Jahr; die Werkstattzahl verändert die Tabelle an keiner Stelle.

Die bestehende Bremse ist damit eine **Steuer auf den Kontostand**. Gegen einen Kontostand kann man nicht
spielen: Alles, was der Spieler wirtschaftlich gut macht, vergrößert nur die Bemessungsgrundlage, und
Horten ist die einzige Antwort — die gerade nicht bestraft wird.

## Ziel

Ein Moneysink, gegen den sich anwirtschaften lässt. Statt Geld wegzunehmen wird Geld **gebunden** und in
etwas verwandelt, das man nicht horten kann. Die Belastung hängt an Entscheidungen des Spielers
(wie gut nutze ich, was ich besitze; wie repräsentiere ich), nicht an seinem Kontostand.

Randbedingung aus früheren Runden: **Einsteiger sollen davon nichts merken.**

## Baustein A — Unterhalt auf ungenutzte Kapazität

Unterhalt zahlt nur, wer Kapazität brachliegen lässt. Eine voll ausgelastete Werkstätte kostet **nichts**;
eine stillgelegte kostet vollen Unterhalt. Damit trifft der Posten das Horten und nicht das Wirtschaften.

**Stufenlos statt Schwelle.** Eine Werkstätte gilt nicht entweder als genutzt oder ungenutzt, sondern
zahlt anteilig zu ihrer Untätigkeit: `Unterhalt = Grundunterhalt × (1 − Auslastung)`. Als Auslastung
dient das Verhältnis, das `Produktionsslot.GetProduktion` ohnehin verwendet — gesetzte Arbeiter zu
benötigten Arbeitern (`Staetten × Arbeiter / Werkstaetten` der Ware), gedeckelt bei 1.

Der Grund für die Stufenlosigkeit ist ein Schlupfloch: Bei einer harten Schwelle („produziert ja/nein")
genügte es, jede Werkstätte mit einem Arbeiter mitlaufen zu lassen, um den Posten vollständig zu
umgehen. Anteilig gerechnet kostet ein Betrieb auf 10 % Auslastung 90 % des Unterhalts — es gibt nichts
auszunutzen, und jede Verbesserung der Auslastung zahlt sich sofort aus.

**Progressiv in der Zahl der brachliegenden Betriebe.** Summiert wird über die ungenutzten Anteile,
und der *n*-te davon kostet `Grundunterhalt × n` — für N vollständig stillgelegte Betriebe also
`Grundunterhalt × N(N+1)/2`. Bewusst linear-progressiv statt geometrisch:
`HandelsManager.SteigerungProzent` arbeitet beim Kaufpreis mit 125 % je Betrieb; über zwanzig Stufen
wäre das Faktor 86 und würde Horten nicht bremsen, sondern verbieten.

- Neuer Posten `AbrechnungsErgebnis.Unterhalt`, in `Gesamtkosten` enthalten.
- Gegenwehr des Spielers: auslasten oder abstoßen — beides jederzeit möglich, beides sofort wirksam.
- Ein Einsteiger, der seine ein bis zwei Betriebe ohnehin bespielt, zahlt strukturell null.

## Baustein B — Hofhaltung als Entscheidung

Der Spieler wählt, wie aufwendig er Hof hält, als Vielfaches des standesgemäßen Aufwands seines Titels
(`Adelstitel.GetJahresaufwand()`) in drei Stufen: sparsam, standesgemäß, aufwendig.
Gespeichert wird die Abweichung von der Mitte, also −1 bis +1.

- **Über standesgemäß**: Der Mehraufwand wird in `PermaAnsehen` umgemünzt, mit einer Jahresobergrenze —
  Geltung wächst über Jahre, nicht in einem Zug.
- **Unter standesgemäß**: Ansehensverlust in derselben Währung. Ohne diesen Druck wählt jeder „sparsam".
- Titel sinken weiterhin nie (`VersuchTitelVerleihen` vergibt nur nach oben). Das bleibt bewusst so.

`ErhoehePermaAnsehen(int)` ist die etablierte Währung für Taten — Kirchenaustritt −100, Kerker,
Mätressenskandal, Pranger, Zufallsereignisse — und wird hier wiederverwendet.

## Baustein C — Das Geldansehen entfällt

Voraussetzung für Baustein B, und für sich genommen die Korrektur einer Schieflage.

`HumSpieler.AnsehenAktualisieren()` setzt heute
`Ansehen = PermaAnsehen + Taler / GetAnsehenProTaler() + Amtsbonus + Häuserbonus`, mit
`AnsehenProTaler = 2500`. Die Größenordnungen:

| Quelle | Wert |
|---|---:|
| Geldbestand bei 2,9 Mio. | **1 160** |
| Ämter-Bonus (Ratsherr … Bürgermeister) | 3 … 12 |
| KI-Ansehen insgesamt (`bonans × 5`) | 15 … 60 |
| Landhaus | 5 |
| Kirchenaustritt | −100 |

**Ansehen ist heute faktisch der Kontostand.** In der Wahl (`AemterManager:272`,
`ansehbon = GetAnsehen() / 10`) bedeutet das +116 für einen wohlhabenden Menschen gegen +1,5 bis +6 für
jeden KI-Gegner. Der Geldanteil entfällt daher ersatzlos.

Bemerkenswert: `AnsehenAktualisieren()` existiert **nur auf `HumSpieler`** und läuft nur für den aktiven
Menschen (`RundenManager:27`). Das Geldansehen ist also ein reiner Spielervorteil, den die KI nie hatte —
sein Wegfall macht das Feld eben, statt den Menschen zu benachteiligen.

Nach der Änderung speist sich Ansehen aus `PermaAnsehen` (Taten, künftig auch Hofhaltung), Amt und
Wohnsitzen und liegt damit in derselben Größenordnung wie das der KI. Daraus folgt die Zielgröße für die
Umrechnung in Baustein B: **Zehnerbeträge pro Jahr, nicht Hunderter.**

### Was davon betroffen ist

Alle Stellen, die `GetAnsehen()` lesen, sehen künftig kleinere Werte. Sie sind zu prüfen, nicht
zwangsläufig zu ändern:

| Stelle | Wirkung |
|---|---|
| `AemterManager:272` | Wahlbonus `Ansehen / 10` |
| `ZugNachrichtenManager:219` | Schuldenprozess-Schwelle `GetMaxSchulden() - Ansehen * 10` |
| `ZugNachrichtenManager:248` | Zugbeginn-Auswertung |
| `GerichtsverhandlungManager:127, 541` | Urteilsfindung |
| `Stuetzpunkt:649` | Militärischer Bonus `Ansehen / 10` |

Besondere Aufmerksamkeit verdient die Schuldenprozess-Schwelle: Sie toleriert heute umso mehr Schulden,
je höher das Ansehen ist — und das Ansehen kam aus dem Geld. Wer viel besaß, war also doppelt geschützt.
Nach der Änderung greift der Schuldenprozess früher; das ist beabsichtigt, muss aber gegen die
E2E-Grundlinie geprüft werden, weil der Schuldturm ein absorbierender Zustand ist.

## Spielstandverträglichkeit

Genau **ein** neues serialisiertes Feld: die Hofhaltungsstufe auf `HumSpieler`.

Serialisierung ist feldbasiert und umgeht Konstruktoren, ein `int` kommt aus einem alten Spielstand also
als `0` an. Gespeichert wird deshalb die **Abweichung** vom standesgemäßen Aufwand (−1 bis +1), nicht die
Stufe selbst: Dann bedeutet die 0 aus dem alten Stand „standesgemäß", also exakt das heutige Verhalten.
Keine Migration, kein Lazy-Init-Accessor nötig.

`PermaAnsehen` existiert bereits. Der Wegfall des Geldansehens ist eine reine Formeländerung;
`AnsehenAktualisieren()` rechnet ohnehin bei jedem Zugbeginn neu, alte Stände korrigieren sich selbst.
Spieler eines laufenden Spielstands verlieren dabei sichtbar Ansehen — das ist die beabsichtigte
Wirkung und gehört in den CHANGELOG.

## Umsetzung und Prüfung

Lib zuerst, Godot danach; der Godot-Commit nennt die Lib-Version im Betreff.

**Lib.** Neuer Abrechnungsposten, Hofhaltungsstufe samt Umrechnung, Wegfall des Geldanteils. Neue Tests
neben `HofhaltungTests`:

- Eine voll ausgelastete Werkstätte kostet keinen Unterhalt, eine stillgelegte den vollen.
- Halbe Auslastung kostet den halben Unterhalt — der Test, der das Schwellen-Schlupfloch ausschliesst.
- Unterhaltsstaffel: der zwanzigste brachliegende Betrieb kostet das Zwanzigfache des ersten.
- Ein Einsteiger, der seine Betriebe bespielt, zahlt null (die Randbedingung als Test).
- Aufwand über/unter standesgemäß ändert `PermaAnsehen` in der erwarteten Richtung, gedeckelt pro Jahr.
- Die Abweichung 0 aus einem alten Spielstand bedeutet „standesgemäß" — der Savegame-Test.
- Das Geldansehen wirkt nicht mehr: gleicher Spieler, zehnfache Barschaft, gleiches Ansehen.

**Godot.** Ein Bildschirm zur Wahl der Hofhaltungsstufe (Vorbild `ProzentwertFestlegenDialog`, der für
den Zehnten dasselbe Muster bedient) und die neuen Posten in `AbrechnungDialog`. CHANGELOG bilingual.

**Messung.** Dieselbe Harness misst die Kurve erneut. Zielbild: Die Jahreslast fällt über die
Vermögensskala **nicht mehr auf 1,7 %**, ohne dass die Spitze im Frühspiel über die heutigen 10 % steigt.
Zusätzlich ein E2E-Lauf über 40 Jahre gegen die Grundlinie (Schicht 5, Band +3 900 bis +53 000 über
15 Jahre, +38 000 bis +178 000 über 40 Jahre): Der Schuldenprozess darf nicht häufiger auslösen als
heute, sonst kippt der Treiber wieder in die Schuldturm-Spirale.

## Offene Zahlen

Bewusst nicht festgelegt, weil sie kalibriert und nicht geraten gehören. Die Harness kann alle drei
messen:

1. `Grundunterhalt` je vollständig brachliegender Werkstätte (Ausgangspunkt der Staffel).
2. Ansehenskurs je Taler Mehraufwand bei der Hofhaltung.
3. Jahresobergrenze des Ansehensgewinns aus der Hofhaltung.

## Messgrundlage

Die Zahlen dieser Spec stammen aus einem Wegwerf-Konsolenprojekt gegen `Conspiratio.Lib` 4.4.1, das je
Rasterzelle einen frischen Spielzustand aufbaut (`NewGameManager` + `PlayerSetupManager`, wie
`TestSpielwelt.Starte`), Titel und Vermögen setzt und anschließend
`AbrechnungsManager.ErstelleAbrechnungFuerAktivenSpieler()` sowie
`ZugNachrichtenManager.PruefeVerbrechen()` auswertet. Beide **verbuchen**, deshalb der frische Zustand je
Zelle. Gemessen wird ausschließlich die **Kostenseite**; die Einnahmenseite hängt an Marktsättigung und
Preisen und lässt sich über ein Raster nicht ehrlich mitteln.

## Nicht Teil dieser Spec

- Die 3-Mio.-Klippe des Vermögensgesetzes zu glätten. Sinnvoll, aber eine eigene Entscheidung: Sobald der
  Korridor darunter belastet ist, sieht die Klippe anders aus und sollte danach neu bewertet werden.
- Steigende Grenzkosten aus Marktdominanz (Löhne, Zunftwiderstand) als dritter Baustein. Konzeptionell
  die eleganteste Bremse, greift aber tief in die Preis- und Kostenformeln und gehört separat geplant.
