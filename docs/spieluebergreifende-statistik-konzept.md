# Konzept: Spielübergreifende Statistik mit Profilen

Stand: 02.08.2026 · Status: **Plan (freigegeben, noch nicht umgesetzt)**

Ein lokales **Profil** bündelt die Lebenswerk-Statistik einer Person über beliebig
viele Spiele hinweg. Man legt Profile an, wechselt zwischen ihnen und sieht die
kumulierten Werte. Kein Netzwerk, keine Cloud, keine Konten – passt zum reinen
Hot-Seat-Stand des Spiels.

## 1. Getroffene Entscheidungen

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Hot-Seat-Mehrspieler | **v2:** ein Profil pro Spieler-Slot (jeder Mensch hat sein eigenes Profil) |
| 2 | Wertungs-Trigger | **Delta beim Speichern** (Autosave + manuell), immer aktuell, doppelzähl-sicher |
| 3 | `ProfilMeta`-Umfang | Spiele, Jahre, Höchstvermögen, Höchstamt. `SpieleGewonnen`/`SpieleVerloren` **architektonisch vorsehen, aber v1 nicht befüllen/anzeigen** (kommt mit dem Auftrags-System) |
| 4 | Ansicht | **Nur Profil-Ansicht**, keine Profil-Vergleichs-Bestenliste in v1 |

**Nicht in v1:** Online-Ranglisten, Achievements, „Spiele gewonnen/verloren"-Wertung,
Profilvergleich.

## 2. Datenmodell (Lib)

```csharp
class Profil {
    string Id;               // GUID – stabil auch bei Umbenennung
    string Name;
    DateTime ErstelltAm;
    DateTime ZuletztGespielt;
    SpielerStatistik Gesamt; // Summe der additiven Zähler aller gewerteten Spiele
    ProfilMeta Meta;
}

class ProfilMeta {
    int SpieleGesamt;         // gezählte Spiele
    int GespielteJahre;       // Summe der Spieljahre
    int HoechstesVermoegen;   // je erreichter Höchstwert (max)
    int HoechstesAmt;         // je erreichtes höchstes Amt (max)

    // Architektonisch vorgesehen, in v1 NICHT befüllt und NICHT angezeigt
    // (werden mit dem künftigen Auftrags-System aktiviert):
    int SpieleGewonnen;
    int SpieleVerloren;
}
```

`Gesamt` verwendet **dieselbe `SpielerStatistik`-Klasse** wie das laufende Spiel.
Spielübergreifend = Summe der Pro-Spiel-Endwerte – damit fällt die komplette
bestehende Kennzahlen-Liste (inkl. der ergänzten Militär-/Haus-Werte) ohne
Zusatzarbeit an.

## 3. Speicherung

Eine Datei **`profile.json`** im selben Verzeichnis wie die Savegames
(`%APPDATA%\Conspiratio\`), verwaltet von einem neuen **`ProfilManager`** (Lib),
der – wie `NewGameManager`/`SpeicherManager` – den Pfad vom Client bekommt.
Inhalt: Profilliste + Zeiger auf das aktive Profil (Default für den primären
lokalen Spieler). Field-basiertes Newtonsoft-JSON wie die Savegames → offen,
editierbar, später auch im Savegame-Editor bearbeitbar.

## 4. Verknüpfung Spiel ↔ Profil (v2)

- Neues Feld **`ProfilId` je `HumSpieler`** (im Savegame). Jeder menschliche
  Spieler trägt seine eigene Profil-Zuordnung.
- **Profilauswahl im Player-Setup:** Bei der Spielererstellung
  (`NewPlayerMenu` / `PlayerSetupManager.ErstelleSpieler`) wählt jeder
  menschliche Spieler sein Profil (oder „ohne Profil"). Der primäre lokale
  Spieler bekommt das aktive Profil als Default.
- **Dublettenprüfung:** Ein Profil darf innerhalb eines Spiels nur einem Slot
  zugeordnet werden. Der Picker blendet bereits vergebene Profile für die
  restlichen Slots aus.
- Spieler „ohne Profil" werden nie gewertet.

## 5. Aggregation – Delta beim Speichern

Die Pro-Spiel-`SpielerStatistik` wächst monoton. Damit nichts doppelt zählt,
speichert das Savegame **je Mensch** einen Snapshot des zuletzt gewerteten
Standes:

- `GewerteteStatistik` (Snapshot der `SpielerStatistik`),
- `GewerteteJahre` (bereits gutgeschriebene Spieljahre),
- `WurdeGezaehlt` (ob dieses Spiel schon `SpieleGesamt` erhöht hat).

Bei **jedem Speichern** (Autosave + manuell) läuft je Mensch mit Profil-Zuordnung:

```
delta = aktuelleStatistik − GewerteteStatistik   // additive Felder
Profil.Gesamt += delta
GewerteteStatistik = aktuelleStatistik
```

### Aggregationsregeln je Feldtyp (wichtig!)

`SpielerStatistik` enthält fast nur additive Zähler – **außer `SoHoechstesAmt`,
das ein Max-Feld ist** und nicht summiert werden darf.

| Feld / Wert | Aggregation |
|-------------|-------------|
| Alle additiven Zähler (Handel, Militär, Kirche, Hinterzimmer, Kinder, …) | Delta-Summe in `Gesamt` |
| `SoHoechstesAmt` | **Max** → `Meta.HoechstesAmt` (nicht additiv; im Gesamt-Sum ausgenommen) |
| Gesamtvermögen (nicht in `SpielerStatistik`, live berechnet) | **Max** → `Meta.HoechstesVermoegen` bei jedem Fold |
| Spieljahre | Delta → `Meta.GespielteJahre` |
| `SpieleGesamt` | einmalig +1, sobald ein Spiel erstmals gewertet wird (`WurdeGezaehlt`) |

**Warum Delta-beim-Speichern:** immer aktuell (auch bei nie formal beendeten
Spielen), durch Konstruktion doppelzähl-sicher, keine brüchige „Spielende"-
Erkennung nötig, piggybackt auf den vorhandenen Save-Flow.

### Einhängepunkt

Der Client ruft im Save-Flow (autospeichern im Kontor + `SaveGameDialog`) vor
dem Schreiben des Spielstands `ProfilManager.WerteLaufendesSpiel()` auf, das über
`SW.Dynamisch` alle menschlichen Spieler mit Profil-Zuordnung faltet und
`profile.json` schreibt.

## 6. UI/UX (Godot)

- **Profil-Verwaltung** (`ProfilDialog`), erreichbar aus dem Hauptmenü:
  Profile auflisten, anlegen (Name), umbenennen, löschen, aktiv setzen.
  Pergament-/`DialogBase`-Muster.
- **Profil-Picker pro Spieler** im Setup (`NewPlayerMenu`): Liste/Dropdown der
  vorhandenen Profile + „ohne Profil"; bereits vergebene ausgegraut.
- **Spielübergreifende Statistik** (`ProfilStatistikDialog`): das
  Zwei-Spalten-Pergament-Layout des `StatistikDialog` wiederverwenden (gleiche
  `StatistikSeite`/`StatistikEintrag`-Ausgabe), gespeist von einem
  `ProfilStatistikManager`, der `Gesamt` + `Meta` formatiert. Aufruf aus der
  Profil-Verwaltung („Lebenswerk anzeigen"). `SoHoechstesAmt` wird dort nicht
  aus `Gesamt`, sondern aus `Meta.HoechstesAmt` gezeigt.

## 7. Architektur-Einordnung

**Lib (neu):**
- `Profil`, `ProfilMeta` (Modelle)
- `ProfilManager` – `profile.json` laden/schreiben, CRUD, aktives Profil,
  `WerteLaufendesSpiel()` (Delta-Fold)
- `ProfilStatistikManager` – Anzeige-Formatierung (`Gesamt` + `Meta`)
- Neue Felder: `ProfilId`, `GewerteteStatistik`, `GewerteteJahre`,
  `WurdeGezaehlt` an `HumSpieler` (Savegame-serialisiert)

**Godot (neu):**
- `ProfilDialog` (Verwaltung), `ProfilStatistikDialog` (Ansicht)
- Verdrahtung in `Main`/Hauptmenü, Profil-Picker in `NewPlayerMenu`,
  Fold-Aufruf im Save-Flow (`Kontor`-Autosave + `SaveGameDialog`)
- `ClientSettings` cacht optional die aktive Profil-ID

**SW.UI:** unverändert – die Profil-UI ist rein Client-getrieben.

## 8. Kompatibilität & Datensicherheit

- Neue Datei; fehlt sie → noch keine Profile, Hinweis „Profil anlegen" oder
  „ohne Profil spielen".
- Alte Savegames ohne `ProfilId` → ungeknüpft, werden nie gewertet. Neue Felder
  defaulten auf null/0, kein Bruch bestehender Stände.
- Offenes JSON, konsistent mit der Savegame-Philosophie.

## 9. Umsetzung in Phasen

1. **Lib-Fundament:** `Profil`/`ProfilMeta`/`ProfilManager` (Storage + CRUD) +
   `HumSpieler`-Verknüpfungsfelder. Konsolentest (Profile anlegen/laden/wechseln).
2. **Godot-Verwaltung:** `ProfilDialog` (anlegen/umbenennen/löschen/aktiv) aus dem
   Hauptmenü; aktives Profil in `ClientSettings`.
3. **Setup-Verknüpfung:** Profil-Picker je Spieler in `NewPlayerMenu` +
   Dublettenprüfung; `ProfilId` in die Spielererstellung durchreichen.
4. **Wertung:** `WerteLaufendesSpiel()` (Delta-Fold + Meta-Regeln) in den
   Save-Flow einhängen. Test: Speichern/Neuladen/Fortsetzen zählt nicht doppelt;
   zwei Menschen falten in getrennte Profile.
5. **Anzeige:** `ProfilStatistikManager` + `ProfilStatistikDialog`
   (spielübergreifende Werte), Aufruf aus der Profil-Verwaltung.

## 10. Künftige Erweiterungen (vorgesehen, nicht v1)

- **Auftrags-System** → `SpieleGewonnen`/`SpieleVerloren` befüllen und anzeigen
  (Felder sind bereits im Modell reserviert).
- **Profil-Bestenliste** (mehrere Profile vergleichen) – mit v2 naheliegend, da
  nun mehrere Profile pro Spiel aufeinandertreffen.
- **Zusätzliche Meta-Werte** (längste Dynastie, meiste Generationen).
