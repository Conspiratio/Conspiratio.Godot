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
(was besitze ich und wie gut lasse ich es aus, wie repräsentiere ich), nicht an seinem Kontostand.

Randbedingung aus früheren Runden: **Einsteiger sollen davon nichts merken.**

## Baustein A — Unterhalt auf jede besessene Werkstätte

Jede besessene Werkstätte kostet jährlichen Unterhalt, **unabhängig davon, ob sie produziert**. Das hat
zwei Wirkungen zugleich: Expansion wird grundsätzlich teurer, und schlechte Auslastung bestraft sich von
selbst — ein Betrieb, der nichts erwirtschaftet, kostet trotzdem. Der Spieler muss ihn auslasten oder
abstoßen; eine eigene Auslastungsregel braucht es dafür nicht.

Das ist die eigentliche Umkehrung gegenüber heute: `AbrechnungsManager` summiert `Betriebskosten` über
`produktionsslot.GetProduktionStaetten()`, also Stätten *in Produktion*. Genau deshalb ist Horten
derzeit gratis und Wirtschaften teuer.

**Progressiv in der Zahl der Betriebe.** Der *n*-te Betrieb kostet `Grundunterhalt × n`, für N Betriebe
also `Grundunterhalt × N(N+1)/2` — quadratisch in der Betriebszahl. Bewusst linear-progressiv statt
geometrisch: `HandelsManager.SteigerungProzent` arbeitet beim Kaufpreis mit 125 % je Betrieb; über
zwanzig Stufen wäre das Faktor 86 und würde Expansion nicht bremsen, sondern verbieten.

- Neuer Posten `AbrechnungsErgebnis.Unterhalt`, in `Gesamtkosten` enthalten.
- Gegenwehr des Spielers: auslasten oder abstoßen — beides jederzeit möglich, beides sofort wirksam.
- **Die Einsteiger-Randbedingung wird hier zur Kalibrierungsfrage.** Anders als bei einer Bemessung auf
  ungenutzte Kapazität zahlt auch ein Anfänger, der seine zwei Betriebe voll auslastet
  (`Grundunterhalt × 3`). Die Progression hält den Betrag klein, aber „strukturell null" ist er nicht
  mehr — `Grundunterhalt` muss deshalb so gewählt werden, dass die ersten Betriebe im Rauschen
  verschwinden, und der Einsteiger-Test unten wacht darüber.

### Die Gegenrichtung: gut geführte Werke bringen Geltung

Der Unterhalt allein bestraft nur — wer gut wirtschaftet, vermeidet damit lediglich Verlust. Damit gutes
Wirtschaften auch *gewinnt*, bekommt hohe Auslastung eine eigene Belohnung, und zwar in derselben
Währung, die Baustein B verbraucht: `PermaAnsehen`. Der Ruf eines Kaufmanns, dessen Werke laufen.

Als Auslastung dient das Verhältnis, das `Produktionsslot.GetProduktion` ohnehin verwendet — gesetzte zu
benötigten Arbeitern (`Staetten × Arbeiter / Werkstaetten` der Ware), gedeckelt bei 1 und über die
Werkstätten des Spielers gemittelt. Oberhalb einer Schwelle (Vorschlag: 80 % im Jahresmittel) gibt es
einen Ansehensgewinn, gestaffelt nach Auslastung und Betriebszahl, mit derselben Jahresobergrenze wie
die Hofhaltung.

Damit schließt sich der Kreis, den das ganze Vorhaben braucht: **Geltung lässt sich erwirtschaften oder
erkaufen** — das eine kostet Können, das andere Geld. Ein reicher, aber schlecht geführter Betrieb muss
seine Geltung teuer kaufen; ein kleiner, exzellent geführter verdient sie sich. Genau das ist die
Antwort auf „ein Moneysink, gegen den man anwirtschaften kann": Der Spieler kann die Kosten nicht
wegzaubern, aber er kann sich das, wofür er sonst zahlen müsste, durch Können verdienen.

Die Schwelle ist bewusst hoch angesetzt: Sie soll eine Auszeichnung sein, kein Grundzustand. Ein Betrieb
knapp unter der Schwelle bekommt nichts — anders als beim Unterhalt gibt es hier nichts auszunutzen,
weil die Belohnung nur nach oben wirkt.

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
| Landhaus (`StatischeSpieldaten:1193`) | 5 |
| **Schloss** (`StatischeSpieldaten:1197`, oberster Eintrag derselben Tabelle) | **50** |
| **Vier Schlösser** (Anwesengesetz im Standard, `GesetzDefUntergrenze[2] = 4`) | **200** |
| **Vierzehn Schlösser** (Anwesengesetz an seiner Obergrenze, `GesetzDefObergrenze[2] = 14`) | **700** |
| **Bauwerksstiftung** (`BauwerkStiftenManager:66`, `Preis / 1000` für 5 000 Taler) | **5** je Stiftung, ungedeckelt und im selben Zug wiederholbar |
| Kirchenaustritt | −100 |

**Ansehen ist heute faktisch der Kontostand.** In der Wahl (`AemterManager:272`,
`ansehbon = GetAnsehen() / 10`) bedeutet das +116 für einen wohlhabenden Menschen gegen +1,5 bis +6 für
jeden KI-Gegner. Der Geldanteil entfällt daher ersatzlos.

Bemerkenswert: `AnsehenAktualisieren()` existiert **nur auf `HumSpieler`** und läuft nur für den aktiven
Menschen (`RundenManager:27`). Das Geldansehen ist also ein reiner Spielervorteil, den die KI nie hatte —
sein Wegfall macht das Feld eben, statt den Menschen zu benachteiligen.

Nach der Änderung speist sich Ansehen aus `PermaAnsehen` (Taten, künftig auch Hofhaltung), Amt und
Wohnsitzen.

> **Berichtigung nach der Schlussdurchsicht (2026-08-29).** Die Tabelle oben tastete ursprünglich nur das
> *Landhaus* (5) ab und schloss daraus, Ansehen liege nach der Änderung „in derselben Größenordnung wie
> das der KI". Das ist so nicht richtig, und die daraus abgeleitete Zielgröße für Baustein B
> („Zehnerbeträge pro Jahr, nicht Hunderter") steht auf einer zu schmalen Stichprobe — sie war die
> Grundlage von zwei der drei kalibrierten Konstanten (`AnsehenJeTalerHofhaltung`, `AnsehenMaxProJahr`).
>
> Was tatsächlich geliefert wurde: Der **Barbestand** als Ansehensquelle ist entfallen, sonst nichts.
> Käuflich bleibt Geltung über zwei Wege, die beide unangetastet sind:
>
> - **Wohnsitze.** Dieselbe Haustabelle endet beim Schloss mit 50 Punkten. Ein Spieler bei 2,9 Mio.
>   Talern kann sich die vier Anwesen leisten, die das Gesetz im Standard erlaubt, und hält damit
>   **200 Punkte** allein aus Wohnsitzen — an der Gesetzesobergrenze 700.
> - **Stiftungen.** `BauwerkStiftenManager.FuehreStiftungAus` gibt `Preis / 1000` Punkte für einen festen
>   Preis von 5 000 Talern, ohne Deckel und ohne Sperre für den laufenden Zug. **60 000 Taler kaufen
>   60 Punkte** — die Spitze der gesamten KI-Spanne, in einem einzigen Zug.
>
> Die Größenordnung nach der Änderung ist also nicht 15–60 wie bei der KI, sondern **200+ und in einem
> Zug beliebig erweiterbar**. Damit ist Baustein B kein Haupthebel, sondern ein Aufschlag auf diese
> Quellen: fünf Punkte im Jahr neben 200, die schon dastehen, und 60, die für den Preis eines guten
> Handelsjahres dazukommen. Die Konstanten sind trotzdem so belassen worden, wie die Kalibrierung sie
> ergeben hat — sie im Nachhinein zu vergrößern hieße, die falsche Seite zu bewegen.
>
> **Offene Entscheidung für den Nutzer** (bewusst nicht in der Fix-Welle erledigt, weil es eine
> Balancing- und keine Wahrheitsfrage ist): ob Wohnsitzansehen und Stiftungen gedeckelt gehören — etwa
> eine Stiftung je Zug, ein Jahresdeckel auf gestifteter Geltung, oder ein Wohnsitzbonus, der nicht mit
> jedem weiteren Anwesen voll addiert. Erst danach ist die Aussage „Ansehen liegt in derselben
> Größenordnung wie das der KI" belegbar.

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

**Gerichtsverhandlung: nachträglich richtiggestellt und entschieden.** Hier stand zuvor, die Schwellen
des Verteidigungsplädoyers (`AnsehenHoch = 80`, `AnsehenMittel = 30`) seien kalibriert worden, als sich
das Ansehen überwiegend aus dem Kontostand speiste. **Das war falsch.** Ein Blick auf die Titeltabelle
zeigt, woran sie tatsächlich hängen: an der **Titelleiter**. `Adelstitel.BonusAnsehen` vergibt Bürger 5,
Edelmann 10, Ritter 20, Landherr 35, Freiherr 55, Baron 75, Graf 100, Fürst 125, Herzog 150 — **80
liegt genau zwischen Baron (75) und Graf (100), 30 genau zwischen Ritter (20) und Landherr (35)**. Das
Feld war nur nirgends gelesen worden, weshalb die Schwellen ins Leere liefen.

**Entschieden und umgesetzt (Conspiratio.Lib, Folgearbeit):** `BonusAnsehen` ist aktiviert — über den
neuen Zugriff `Spieler.GetStandesAnsehen()` = Ansehen + Titelbonus. Damit greift die ursprünglich
gemeinte Regel wieder: Graf und darüber erhalten den vollen Plädoyerbonus, Landherr bis Baron den
halben, darunter keinen. Umgestellt sind genau drei Stellen, alle drei Beurteilungen einer **Person**:
`GerichtsverhandlungManager` (Plädoyerbonus und Plädoyertext) und der Geschworenenspruch des
Schuldenprozesses (`ZugNachrichtenManager:248`).

**Nicht** umgestellt sind `AemterManager:272` (Wahlbonus), `ZugNachrichtenManager:219`
(Schuldentoleranz) und `Stuetzpunkt:649` (Militärbonus). Der Grund ist derselbe, aus dem das
Geldansehen weggefallen ist: Titel werden nach Talerschwellen verliehen, ein Titelbonus im Wahlbonus
würde den Weg von Geld zu Einfluss sofort wieder öffnen. **Der Titel öffnet Türen bei Hofe, er kauft
keine Stimmen.**

Der Zugriff sitzt bewusst auf `Spieler` und nicht als Summand in `HumSpieler.AnsehenAktualisieren`:
Jene Methode gibt es nur auf `HumSpieler` und sie läuft nur für den aktiven Menschen — dort eingebaut
hätte der Titelbonus wieder nur Menschen gestärkt und die KI nie, also genau die Asymmetrie
wiederhergestellt, die beim Geldansehen der Fehler war. Auf `Spieler` gilt er für beide, denn auch
KI-Spieler tragen Titel. Der Vermerk über den beiden Konstanten im Code ist entsprechend
richtiggestellt.

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

- Unterhaltsstaffel: der zwanzigste Betrieb kostet das Zwanzigfache des ersten, die Summe stimmt.
- Der Unterhalt haengt nicht an der Produktion: ein stillgelegter und ein voll ausgelasteter Betrieb
  kosten denselben Unterhalt (die Umkehrung gegenueber `Betriebskosten` als Test).
- Ein Einsteiger mit zwei Betrieben zahlt einen vernachlaessigbaren Betrag (die Randbedingung als Test,
  und die Schranke fuer die Kalibrierung von `Grundunterhalt`).
- Hohe Auslastung bringt `PermaAnsehen`, knapp unter der Schwelle nichts, und der Gewinn ist auf die
  Jahresobergrenze gedeckelt.
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

1. `Grundunterhalt` je Werkstätte (Ausgangspunkt der Staffel). Untere Schranke: Zwei Betriebe
   dürfen einen Einsteiger nicht spürbar belasten. Obere Schranke: Der Unterhalt muss klar unter dem
   Deckungsbeitrag einer ausgelasteten Werkstätte liegen, sonst lohnt Expansion nie.
2. Ansehenskurs je Taler Mehraufwand bei der Hofhaltung.
3. Jahresobergrenze des Ansehensgewinns aus der Hofhaltung.
4. Auslastungsschwelle und Ansehenskurs der Werkstatt-Belohnung.

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
