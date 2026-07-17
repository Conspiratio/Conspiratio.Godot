# Changelog Conspiratio Godot

## 0.1.0.0-godot

_Unreleased_

### [DE]

#### Hinzugefügt
- Erste Version des MVP (Minimum Viable Product)
- Neuer Dialog "TextDialog" zur Anzeige von Text- und Fehlermeldungen
- Neues Spiel erstellen: Spieleranzahl, Cheatmodus, Testmodus und Todesfälle anzeigen werden übernommen; Fehlermeldungen werden angezeigt; Enter bestätigt die Eingabe
- Neues Menü "Spieler erstellen" (Migration von SpielerHinzufuegen): Name, Geschlecht, Religion, Banner-Auswahl (bereits vergebene Banner ausgeblendet) sowie Heimatstadt (zufällig vorausgewählt und kostenlos, Wechsel kostet Taler) und Rohstoff (gezielte Wahl kostet Taler, zufällig ist kostenlos)
- Erste Version der Kontor-Szene: Spielerleiste (Name mit Titel und Amt, Jahr, Taler), Hot-Seat-Rundenablauf mit Spieler-Ankündigung, "Runde beenden" mit Spieler- und Jahreswechsel sowie Schuldturm-Behandlung; die sechs Kontor-Bereiche (Handel, Hinterzimmer, Schreibstube, Kirche, Söldner & Räuber, Geld zum Fenster rauswerfen) sind wie im Original beschriftete Klickbereiche
- Jahresabrechnung beim Zugende (Migration von Abrechnung): alle Kostenpositionen von Arbeitern bis Sold werden berechnet, abgezogen und mit Münzen-Sound im neuen Abrechnungsdialog angezeigt
- Neue Stadtansicht im Design der WinForms-Vorlage: Stadtgrafik als Hintergrund, Werkstätten-Symbole (kaufen per Klick, verkaufen per Rechtsklick), Rohstoff-Icons (Klick verkauft den Bestand), Lagerbestände mit Ziffern-Klick zum Ein- und Verkaufen (obere Hälfte kauft, untere verkauft, die Ziffernposition bestimmt die Menge), Preise, Haus- und Karawanen-Symbol sowie beide Produktionszeilen im Original-Stil
- Neues Control "NumericButtonWithSounds" (Portierung des NumericButtons): Wertänderung per Klick mit Zehnerpotenz je Ziffernposition, Plus-/Minus-Cursor und Hover-Effekt
- Jahresbuch zu Zugbeginn ab dem zweiten Jahr: Exporte werden verkauft und gutgeschrieben, die Produktion wird mit Qualitätsmeldung eingelagert — die Core-Gameplay-Loop (produzieren, lagern, verkaufen) ist damit spielbar
- Zugnachrichten beim Zugende (erster Teil): Strafen bei Gesetzesverstößen (Höchstzahl Anwesen, maximale Taler), Amtseinkommen, Anwesen-Meldungen, Sterbeprüfung mit Todesursache und Ausscheiden des Spielers (inkl. Spielende, wenn niemand mehr lebt) sowie Schuldenprozess mit Geschworenen-Abstimmung und Schuldturm
- Kontor-Beschriftungen erscheinen in Gold und nur noch bei MouseOver über dem jeweiligen Bereich
- Das UI skaliert jetzt mit der Fenstergröße (canvas_items-Stretch mit fester Design-Auflösung 1600×900 und beibehaltenem Seitenverhältnis) — annähernd beliebige Auflösungen werden unterstützt
- Speichern und Laden: automatisches Speichern zu Beginn jedes Zugs (mit Aufräumen alter Autosaves), Speichern-Abfrage beim Verlassen des Spiels, "Spiel laden" mit Spielstandsliste (inkl. Löschen) und "Spiel fortsetzen" für den letzten Spielstand
- Spielstände werden in einem offenen, von Hand editierbaren JSON-Format gespeichert; alte WinForms-Spielstände (*.dat) können hier nicht geladen werden (im WinForms-Client laden und neu speichern konvertiert sie)
- Projekt auf Godot 4.7.1 angehoben

#### Geändert

#### Behoben

### [EN]

#### Added
- First version of MVP (Minimum Viable Product)
- New dialog "TextDialog" for showing text and error messages
- Create new game: player count, cheat mode, test mode and show deaths are applied; error messages are shown; Enter confirms the input
- New menu "Spieler erstellen" (migration of SpielerHinzufuegen): name, gender, religion, banner selection (already taken banners are hidden) as well as home town (randomly preselected and free, changing it costs Taler) and resource (deliberate choice costs Taler, random is free)
- First version of the Kontor scene: player bar (name with title and office, year, Taler), hot seat turn flow with player announcement, "end turn" with player and year change as well as debtor's tower handling; the six Kontor areas (trade, back room, writing room, church, mercenaries & robbers, throwing money out of the window) are labeled click areas like in the original
- Yearly settlement at the end of the turn (migration of Abrechnung): all cost positions from workers to military pay are calculated, deducted and shown in the new settlement dialog with a coin sound
- New town view in the design of the WinForms original: town graphic as background, workshop symbols (buy per click, sell per right click), resource icons (click sells the stock), stock displays with digit clicking for buying and selling (upper half buys, lower half sells, the digit position determines the amount), prices, house and caravan symbol as well as both production rows in the original style
- New control "NumericButtonWithSounds" (port of the NumericButton): value change per click with a power of ten per digit position, plus/minus cursor and hover effect
- Yearly book at the start of the turn from the second year on: exports are sold and credited, the production is stored with a quality message — the core gameplay loop (produce, store, sell) is now playable
- Turn messages at the end of the turn (first part): fines for law violations (maximum estates, maximum Taler), office income, estate messages, death check with cause of death and elimination of the player (including game over when nobody is left) as well as the debt trial with juror voting and debtor's tower
- Kontor captions appear in gold and only on mouse over of the respective area
- The UI now scales with the window size (canvas_items stretch with a fixed design resolution of 1600×900 and kept aspect ratio) — almost arbitrary resolutions are supported
- Saving and loading: automatic saving at the start of each turn (with cleanup of old autosaves), save prompt when leaving the game, "Spiel laden" with a savegame list (including deleting) and "Spiel fortsetzen" for the last savegame
- Savegames are stored in an open, hand-editable JSON format; old WinForms savegames (*.dat) cannot be loaded here (loading and saving them once in the WinForms client converts them)
- Project upgraded to Godot 4.7.1

#### Changed

#### Fixed