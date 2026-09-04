# Konzept: Den Sättigungsrabatt für den Spieler sichtbar machen

**Stand:** 2026-08-28 · **Betrifft:** nur den Godot-Client (Anzeige). Keine Regeländerung, keine Lib.

## Das Problem in einem Satz

Der Sättigungsrabatt steuert die zentrale Handelsentscheidung des Spiels — weiterziehen oder einen Markt
entwickeln —, und der Spieler sieht ausschließlich **seine Wirkung**, nie **seine Ursache**.

`assets/scripts/Stadt.cs:259` schreibt in die Preiszeile das Ergebnis von
`Stadt.GetRohstoffPreisVonIDX`, also den **bereits gerabatteten** Preis. Wer über die Jahre zusieht, wie
sein Korn von 8 auf 4 Taler fällt, erfährt an keiner Stelle, dass er den Markt selbst vollgeschüttet hat
— und erst recht nicht, dass Warten nichts hilft, Ausweichen dagegen alles.

## Die Mechanik, in Spielersprache

Aus `Conspiratio.Lib/Gameplay/Gebiete/Stadt.cs:131`:

```
Jahresbedarf     = max(1, Einwohner / 10)
Abschlag Prozent = min(50, Stadtvorrat × 10 / Jahresbedarf)
Preis            = Grundpreis × (100 − Abschlag) / 100
```

In Worten: **Jeder Jahresbedarf, der unverkauft im Stadtlager liegt, kostet zehn Prozent des Preises,
höchstens die Hälfte.** Eine Stadt mit 2 000 Einwohnern verbraucht 200 Einheiten im Jahr; liegen dort
600, sind das drei Jahresbedarfe und damit 30 % Abschlag.

Zwei Eigenschaften, die der Spieler kennen müsste und heute nicht erfahren kann:

- **Der Abschlag hängt an der Einwohnerzahl.** Eine große Stadt verkraftet mehr Absatz als eine kleine.
  Damit ist „wohin verkaufe ich" eine echte Entscheidung und nicht bloß eine Wegstrecke.
- **Er greift unter `preisMin`.** Der Kommentar bei `MaxAbschlagProzent` sagt es ausdrücklich: Die alte
  Preisuntergrenze machte den Abschlag folgenlos, deshalb bindet ihn heute nur der 50-Prozent-Deckel.
  Ein gesättigter Markt kann also unter jeden Preis fallen, den die Warentabelle nennt.

## Was der Client heute schon zeigt — und warum es nicht zusammenfindet

Die Zutaten sind alle vorhanden, aber auf zwei Bildschirme verteilt und nirgends verknüpft:

| Was | Wo | Anmerkung |
|---|---|---|
| Der gerabattete Preis | Stadtansicht, `LabelPreis<n>` | Die Wirkung, ohne Ursache |
| Der eigene Lagerbestand | Stadtansicht, `LabelBestand<n>` | `GetLagerbestand` — **der des Spielers**, nicht der der Stadt |
| Einwohner | Stadtinformationen-Dialog | Der eine Faktor des Jahresbedarfs |
| Nachfrage (Symbolreihe) | Stadtinformationen-Dialog | Welche Waren die Stadt braucht, nicht wie viel davon schon daliegt |
| Lagerstand (farbig) | Stadtinformationen-Dialog | **Anteil des Stadtvorrats am Landesvorrat**, rot/orange/grün |

Die letzte Zeile ist die gefährlichste: Es *gibt* eine farbige Lagerstandsanzeige, aber sie beantwortet
eine andere Frage. „Viel im Verhältnis zum ganzen Land" ist nicht dasselbe wie „viel im Verhältnis zu
dem, was diese Stadt im Jahr verbraucht". Ein Spieler, der darin das Sättigungssignal vermutet, liegt
falsch — und wird es nie merken.

**Es fehlt also nichts an Daten. Es fehlt die Verknüpfung.**

## Vorschlag in vier Stufen

Bewusst gestaffelt: Jede Stufe steht für sich, jede ist einzeln lieferbar, und die erste trägt den
Großteil des Nutzens.

### Stufe A — Tooltip an der Preiszeile *(empfohlen als Erstes)*

Ein Tooltip auf `LabelPreis<n>`, der die Rechnung in Spielersprache aufmacht:

> Korn: Grundpreis 8 Taler.
> Altenfeld verbraucht 200 im Jahr und lagert 600 — drei Jahresbedarfe.
> Marktabschlag 30 %, Ihr erhaltet **5 Taler**.

Alles darin steht bereits zur Verfügung (`GetEinwohner`, `GetRohstoffIDXVorrat`, die beiden Konstanten).
Es gibt einen Präzedenzfall im selben Bildschirm: Der Warenknopf hat seit kurzem einen Tooltip mit dem
Arbeiter-pro-Werkstätte-Verhältnis und dem Abgleich zur aktuellen Einstellung — dieselbe Machart, dieselbe
Stelle, dieselbe Absicht.

Beim Höchstwert muss der Text das auch sagen („mehr Vorrat drückt den Preis nicht weiter"), sonst
schließt der Spieler aus einem stehenden Wert, die Mechanik sei ausgesetzt.

### Stufe B — Der Preis sieht man an, dass er gedrückt ist

Die Preiszeile steht in Gold auf der Steinwand. Ein gerabatteter Preis bekommt eine abweichende Färbung,
deren Stärke mit dem Abschlag wächst. Damit erkennt der Spieler eine gesättigte Ware, **ohne** zu hovern
— und das ist der Unterschied zwischen „nachschlagbar" und „auffällig".

Vorsicht bei der Farbwahl: Die Stadtansicht nutzt Rot bereits für ungültige Nullwerte in den
Produktionszeilen. Die Sättigung braucht ein eigenes Zeichen, sonst kollidieren zwei Bedeutungen.

**Umgesetzt — und die Farbwahl entschied sich am Kontrast, nicht am Geschmack.** Zwei Messungen an
gerenderten Bildern haben je einen ersten Entwurf verworfen:

- **Auf der Steinwand darf die Farbe nicht dunkler werden.** Der erste Entwurf dunkelte das Gold zu
  einem stumpfen Bronze ab. Gemessen: Gold trägt 2,08:1 gegen die Steinwand, das stumpfe Bronze nur
  noch **1,26:1** — die Zahl wäre ausgerechnet beim vollen Abschlag fast in der Wand verschwunden.
  Jetzt wird das Gold **entsättigt statt abgedunkelt** (bis `rgb(204, 189, 158)`); der Kontrast bleibt
  über die ganze Skala zwischen 2,08:1 und 1,87:1, während der Blaukanal von 38 auf 158 steigt — das
  Gold bleicht sichtbar aus, ohne unleserlich zu werden.
- **Auf dem Pergament darf die Schriftfarbe gar nicht angetastet werden.** Dunkelbraun trägt dort rund
  5:1, derselbe Ocker als Füllung nur noch rund 1,7:1 — dasselbe Problem. Die Zielstadt bekommt das
  Signal deshalb als **Kontur** um die unverändert dunkle Schrift; das kostet keinen Kontrast.

Beides läuft über `ErmittleSaettigungsAnteil`, das auf dem **gekappten** Abschlag rechnet: Oberhalb des
Deckels ändert sich am Preis nichts mehr, also darf sich auch an der Farbe nichts mehr ändern — sonst
verspräche sie eine Verschlechterung, die nicht eintritt.

### Stufe C — Beim Export die Zielstadt vergleichbar machen

Der Export ist die Stelle, an der die Entscheidung tatsächlich fällt: Der Spieler wählt eine Zielstadt.
Ist die Sättigung dort unsichtbar, liefert er blind in einen vollen Markt — und weil die Menge seit der
Korrektur im `BuchManager` in den Vorrat der **Ziel**stadt gebucht wird, sättigt er ihn dabei weiter.

Vorschlag: im Auswahlschritt je Zielstadt den zu erwartenden Preis und den Abschlag nennen, damit die
Wahl eine informierte ist. Das ist inhaltlich der größte Gewinn der vier Stufen und zugleich der
aufwendigste, weil er die Exportauswahl berührt.

**Umgesetzt als Tooltip am Zielstadt-Knopf**, nicht als sichtbare Spalte — aus zwei Gründen, die erst
der Blick in den Client zeigte:

- **Es gibt keinen Auswahlschritt mit Liste.** Die Zielstadt wird in der Verkaufszeile über einen
  `NumericButton` durchgeschaltet (`Stadt.OnStaetteChanged` → `SetzeVerkaufsStadtAusWert`). Eine Liste,
  die man je Stadt annotieren könnte, existiert nicht.
- **Die Zeile hat keinen Platz.** Gemessen mit der echten Schrift endet die Verkaufszeile im
  ungünstigsten Fall (längster Stadtname „Frozen Castle", fünfstellige Fuhrkosten) bei x = 1017, das
  Pergament von `BackgroundStadt.png` bei x = 1101. Es bleiben **84 px, rund sechs Zeichen**. Schon
  „zu je 20 Taler" braucht 180 px, mit Abschlagsangabe 407 px. Der `HBoxContainer` reicht zwar bis
  1360, aber das Pergament ist Teil des Hintergrundbildes — die Zeile liefe auf die Steinwand hinaus.

Was damit ausfällt, ist die Auffälligkeit ohne Hovern. Wer sie will, muss bei der **Farbe** ansetzen
(Stufe B), denn Farbe kostet keine Breite — oder das Hintergrundbild ändern.

### Stufe D — Die Sättigung im Stadtinformationen-Dialog benennen

Dort stehen Einwohner, Nachfrage und Lagerstand bereits nebeneinander. Eine Zeile oder Spalte
„Sättigung: 3 Jahresbedarfe → −30 %" macht aus drei Einzelwerten eine Aussage — und entschärft zugleich
die oben beschriebene Verwechslungsgefahr mit dem landesweiten Lagerstand.

## Was ich ausdrücklich **nicht** vorschlage

- **Keine Regeländerung.** Die Mechanik ist gemessen und kalibriert; hier geht es allein darum, dass der
  Spieler sie sehen kann. Wer sie beim Sichtbarmachen zugleich verändert, weiß hinterher nicht, was
  gewirkt hat.
- **Keine Prozentzahl ohne Erklärung.** „−30 %" allein erzeugt Ratlosigkeit; die Ursache („drei
  Jahresbedarfe liegen dort") ist der eigentliche Inhalt.
- **Keine Empfehlung an den Spieler.** Das Spiel soll die Lage zeigen, nicht die beste Stadt vorschlagen
  — sonst nimmt es genau die Entscheidung ab, die es interessant macht.

## Empfehlung

**Stufe A zuerst.** Ein Tooltip, eine Textzeile, alle Daten zur Hand, ein Präzedenzfall im selben
Bildschirm — und danach kann kein Spieler mehr über einen fallenden Preis rätseln. **Stufe C als
Zweites**, weil dort die Entscheidung wirklich fällt. B und D sind Verfeinerungen.

## Offene Fragen

1. **Wo genau sitzt die Exportauswahl im Client?** *Beantwortet:* in der Verkaufszeile der Stadtansicht,
   als `NumericButton`, der die Städte durchschaltet — es gibt weder einen Auswahlschritt noch eine
   Liste. Eine zusätzliche Angabe je Stadt verträgt die Zeile nicht (84 px Spielraum, siehe Stufe C);
   der Aufwand lag deshalb nicht im Umbau, sondern im Messen.
2. **Soll der Tooltip den Grundpreis nennen?** *Beantwortet: ja.* Ohne Bezugsgröße bleibt der Abschlag
   abstrakt; dass er dabei die Warentabelle verrät, ist der geringere Preis. Zurückrechnen lässt er sich
   allerdings nicht: `GetRohstoffPreisVonIDX` teilt ganzzahlig ab, sodass mehrere Grundpreise auf
   denselben Marktpreis fallen (über Grundpreis 1–60 × Abschlag 0–50 gemessen: 60 % der Rückrechnungen
   falsch — und die angezeigten Zahlen gehen dann beim Nachrechnen nicht auf). Seit Lib 4.7.0 liefert
   ihn `Stadt.GetRohstoffBasispreisVonIDX` direkt. Wer ihn anderswo anzeigt, nimmt diesen Zugriff.
3. **Braucht der Kaufpreis dieselbe Behandlung?** Der Abschlag wirkt auf `GetRohstoffPreisVonIDX`, und
   das ist derselbe Wert, zu dem der Spieler *einkauft*. Eine gesättigte Stadt ist also zugleich eine
   billige Einkaufsquelle — ein Zusammenhang, den ein Tooltip nebenbei mit erklären könnte.
