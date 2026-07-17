# Changelog Conspiratio Godot

## 0.1.0.0-godot

_Unreleased_

### [DE]

#### Hinzugefügt
- Erste Version des MVP (Minimum Viable Product)
- Neuer Dialog "TextDialog" zur Anzeige von Text- und Fehlermeldungen
- Neues Spiel erstellen: Spieleranzahl, Cheatmodus, Testmodus und Todesfälle anzeigen werden übernommen; Fehlermeldungen werden angezeigt; Enter bestätigt die Eingabe
- Neues Menü "Spieler erstellen" (Migration von SpielerHinzufuegen): Name, Geschlecht, Religion, Banner-Auswahl (bereits vergebene Banner ausgeblendet) sowie Heimatstadt (zufällig vorausgewählt und kostenlos, Wechsel kostet Taler) und Rohstoff (gezielte Wahl kostet Taler, zufällig ist kostenlos)
- Erste Version der Kontor-Szene: Spielerleiste (Name mit Titel und Amt, Jahr, Taler), Hot-Seat-Rundenablauf mit Spieler-Ankündigung, "Runde beenden" mit Spieler- und Jahreswechsel sowie Schuldturm-Behandlung
- Jahresabrechnung beim Zugende (Migration von Abrechnung): alle Kostenpositionen von Arbeitern bis Sold werden berechnet, abgezogen und mit Münzen-Sound im neuen Abrechnungsdialog angezeigt
- Neue Stadtansicht "Handel & Produktion" (Kontor-Bereiche Handel und Werkstatt): Werkstätten kaufen/verkaufen, Rohstoffe direkt kaufen/verkaufen, beide Produktionsslots einstellen (Produzieren mit Arbeitern und Stätten oder Verkaufen/permanenter Verkauf mit Menge und Zielstadt) sowie Karawane wählen
- Jahresbuch zu Zugbeginn ab dem zweiten Jahr: Exporte werden verkauft und gutgeschrieben, die Produktion wird mit Qualitätsmeldung eingelagert — die Core-Gameplay-Loop (produzieren, lagern, verkaufen) ist damit spielbar
- Zugnachrichten beim Zugende (erster Teil): Strafen bei Gesetzesverstößen (Höchstzahl Anwesen, maximale Taler), Amtseinkommen, Anwesen-Meldungen, Sterbeprüfung mit Todesursache und Ausscheiden des Spielers (inkl. Spielende, wenn niemand mehr lebt) sowie Schuldenprozess mit Geschworenen-Abstimmung und Schuldturm
- Projekt auf Godot 4.7.1 angehoben

#### Geändert

#### Behoben

### [EN]

#### Added
- First version of MVP (Minimum Viable Product)
- New dialog "TextDialog" for showing text and error messages
- Create new game: player count, cheat mode, test mode and show deaths are applied; error messages are shown; Enter confirms the input
- New menu "Spieler erstellen" (migration of SpielerHinzufuegen): name, gender, religion, banner selection (already taken banners are hidden) as well as home town (randomly preselected and free, changing it costs Taler) and resource (deliberate choice costs Taler, random is free)
- First version of the Kontor scene: player bar (name with title and office, year, Taler), hot seat turn flow with player announcement, "end turn" with player and year change as well as debtor's tower handling
- Yearly settlement at the end of the turn (migration of Abrechnung): all cost positions from workers to military pay are calculated, deducted and shown in the new settlement dialog with a coin sound
- New town view "Handel & Produktion" (Kontor areas trade and workshop): buy/sell workshops, buy/sell resources directly, configure both production slots (producing with workers and sites or selling/permanent selling with amount and target town) as well as choosing the caravan
- Yearly book at the start of the turn from the second year on: exports are sold and credited, the production is stored with a quality message — the core gameplay loop (produce, store, sell) is now playable
- Turn messages at the end of the turn (first part): fines for law violations (maximum estates, maximum Taler), office income, estate messages, death check with cause of death and elimination of the player (including game over when nobody is left) as well as the debt trial with juror voting and debtor's tower
- Project upgraded to Godot 4.7.1

#### Changed

#### Fixed