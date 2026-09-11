using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Einstellungen;
using Conspiratio.Lib.Gameplay.Kampf;
using Conspiratio.Lib.Gameplay.Niederlassung;
using Conspiratio.Lib.Gameplay.Rohstoffe;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Spielaufbau, Speicherprobe und Bildnachweis - alles, was den Lauf umgibt.
///
/// Das Spiel entsteht wie beim Spieler über die Menüs; nur das ruft die zwölf
/// Einstiegsschirme überhaupt auf. <c>--ohne-menue</c> fällt auf die Lib-Manager zurück.
/// </summary>
public partial class E2eTreiber
{
	/// <summary>
	/// Speichern und Laden mit dem Spielstand, der gerade durchgespielt wurde. Das ist die schärfste
	/// Probe auf die Serialisierung: Nach etlichen Jahren steckt im Stand deutlich mehr als in einem
	/// frischen Spiel (Ämter, Familie, Erpressungen, Statistik).
	/// </summary>
	private void PruefeSpeichernUndLaden()
	{
		var speicher = new SpeicherManager(SpielstandOrdner);

		int jahrVorher = SW.Dynamisch.GetAktuellesJahr();
		string nameVorher = SW.Dynamisch.GetHumWithID(1).GetName();
		int talerVorher = SW.Dynamisch.GetHumWithID(1).GetTaler();

		if (!speicher.Speichern("e2e-probe", out string speicherFehler))
		{
			_fehler.Add("Der Spielstand ließ sich nicht speichern: " + speicherFehler);
			return;
		}

		if (!speicher.Laden("e2e-probe", out string ladeFehler))
		{
			_fehler.Add("Der gespeicherte Spielstand ließ sich nicht laden: " + ladeFehler);
			return;
		}

		if (SW.Dynamisch.GetAktuellesJahr() != jahrVorher)
			_fehler.Add("Nach dem Laden steht das Jahr auf " + SW.Dynamisch.GetAktuellesJahr() + " statt " + jahrVorher + ".");

		if (SW.Dynamisch.GetHumWithID(1).GetName() != nameVorher)
			_fehler.Add("Nach dem Laden heißt der Spieler " + SW.Dynamisch.GetHumWithID(1).GetName() + " statt " + nameVorher + ".");

		if (SW.Dynamisch.GetHumWithID(1).GetTaler() != talerVorher)
			_fehler.Add("Nach dem Laden hat der Spieler " + SW.Dynamisch.GetHumWithID(1).GetTaler() + " statt " + talerVorher + " Taler.");

		GD.Print("Speichern und Laden geprüft (Jahr " + jahrVorher + ").");
	}

	/// <summary>
	/// Legt das Spiel über die Menüs an, wie ein Spieler es tut: Hauptmenü → „Lokales Spiel" → neues
	/// Spiel benennen und Spielerzahl wählen → je Spieler Name, Geschlecht, Religion und Banner.
	///
	/// Der Umweg lohnt, weil der Einstieg sonst gar nicht geprüft wird: <see cref="LegeSpielAn"/> baut
	/// das Spiel direkt über die Lib-Manager und überspringt damit Hauptmenü, Spielanlage und
	/// Spielererstellung – ausgerechnet die Bildschirme, die jeder Spieler als erstes sieht.
	///
	/// Hier wird gezielt geklickt statt generisch: Die Menüs bestehen aus Eingabefeldern, Ankreuzfeldern
	/// und Auswahllisten, bei denen „irgendein sichtbarer Knopf" nicht weiterführt.
	/// </summary>
	private async Task<bool> LegeSpielUeberMenueAn(int spieler, int seed)
	{
		if (!await WarteAufSichtbar(nameof(Mainmenu), brauchtEingabe: false))
		{
			_fehler.Add("Das Hauptmenü wurde nicht bedienbereit.");
			return false;
		}

		await Schiesse(nameof(Mainmenu));
		// Das Hauptmenü ist als Kindknoten eingehängt, aber – anders als die Dialoge – kein Feld an Main.
		Druecke(_main.GetNode<Control>(nameof(Mainmenu)), "ButtonLocalGame");

		// „Lokales Spiel" ist die Stelle, an der der Client die statischen Spieldaten aufbaut – erst
		// danach lässt sich der Zufallsgenerator festlegen (Initialisieren legt ihn neu an).
		if (!await WarteAufSichtbar(nameof(LocalGameDialog), brauchtEingabe: false))
		{
			_fehler.Add("Der Dialog für das lokale Spiel ging nicht auf.");
			return false;
		}

		SW.Statisch.SetRnd(seed);

		await Schiesse(nameof(LocalGameDialog));
		Druecke(_main.LocalGameDialog, "ButtonStartGame");

		if (!await WarteAufSichtbar(nameof(NewLocalGameMenu), brauchtEingabe: false))
		{
			_fehler.Add("Das Menü für ein neues Spiel ging nicht auf.");
			return false;
		}

		var neuesSpiel = _main.NewLocalGameMenu;
		Finde<LineEdit>(neuesSpiel, "LineEditGameName").Text = "E2E";
		Finde<BaseButton>(neuesSpiel, "CheckBox" + spieler + "Player").ButtonPressed = true;

		await Schiesse(nameof(NewLocalGameMenu));
		Druecke(neuesSpiel, "ButtonCreateGame");

		// Je Spieler eine Runde Spielererstellung. Heimatstadt und Rohstoffplatz bleiben ungewählt –
		// das ist der kostenlose Weg, den das Menü mit einer zufälligen Zuteilung beantwortet.
		for (int i = 1; i <= spieler; i++)
		{
			if (!await WarteAufSichtbar(nameof(NewPlayerMenu), brauchtEingabe: false))
			{
				_fehler.Add("Die Spielererstellung für Spieler " + i + " ging nicht auf.");
				return false;
			}

			var spielerMenue = _main.NewPlayerMenu;
			Finde<LineEdit>(spielerMenue, "LineEditPlayerName").Text = "Testspieler" + i;
			Finde<BaseButton>(spielerMenue, i % 2 == 1 ? "CheckBoxMale" : "CheckBoxFemale").ButtonPressed = true;
			Finde<BaseButton>(spielerMenue, "CheckBoxReligion1").ButtonPressed = true;
			Finde<BaseButton>(spielerMenue, "CheckBoxBanner" + i).ButtonPressed = true;

			await Schiesse(nameof(NewPlayerMenu));
			Druecke(spielerMenue, "ButtonCreatePlayer");
			await NaechsterFrame();
		}

		if (!await WarteBisKontorBereit())
		{
			_fehler.Add("Nach der Spielererstellung wurde der Kontor nicht bedienbereit. " + Zustandsbericht());
			return false;
		}

		GD.Print("Spiel über die Menüs angelegt (" + spieler + " Spieler).");
		return true;
	}

	/// <summary>
	/// Sucht ein Bedienelement des Menüs über seinen Namen, egal wie tief es liegt. Feste Knotenpfade
	/// wären hier die schlechtere Wahl: Sie brechen still, sobald jemand in der Szene eine Ebene einzieht.
	/// </summary>
	private T Finde<T>(Node menue, string name) where T : Node
	{
		var knoten = menue.FindChild(name, true, false) as T;

		if (knoten == null)
			_fehler.Add("Im Menü " + menue.Name + " fehlt das Bedienelement " + name + ".");

		return knoten;
	}

	private void Druecke(Node menue, string name)
		=> Finde<BaseButton>(menue, name)?.EmitSignal(BaseButton.SignalName.Pressed);

	private static void LegeSpielAn(int spieler, int seed)
	{
		// Die statischen Spieldaten legt der Client sonst beim Klick auf „Lokales Spiel" an (Mainmenu).
		SW.Statisch.Initialisieren();

		// Erst nach Initialisieren, denn dieses legt den Zufallsgenerator neu an.
		SW.Statisch.SetRnd(seed);

		string pfad = Path.Combine(Path.GetTempPath(), "conspiratio-e2e");
		Directory.CreateDirectory(pfad);

		var neuesSpiel = new NewGameManager(pfad);
		neuesSpiel.CreateNewGame("E2E", spieler, false, true, false, out string fehler);

		if (!string.IsNullOrEmpty(fehler))
			throw new Exception("Spiel konnte nicht angelegt werden: " + fehler);

		var setup = new PlayerSetupManager();
		setup.Starte();

		for (int i = 1; i <= spieler; i++)
		{
			// Feste Heimatstädte statt Zufall: Ein fehlgeschlagener Durchlauf lässt sich sonst kaum
			// nachstellen. Geschlecht und Banner wechseln durch, damit beides mit durchlaufen wird.
			int stadtId = Math.Min(4 + i, SW.Statisch.GetMaxStadtID());
			setup.ErstelleSpieler("Testspieler" + i, i % 2 == 1, i, SW.Statisch.GetRelKathID(), stadtId, true, 1);
		}

		setup.Beende();
		SW.Dynamisch.SetAktiverSpieler(1);
	}

	/// <summary>
	/// Wertet die Bild-Schalter aus und legt den Zielordner an. Ohne Renderer bleibt die Aufnahme aus:
	/// Der Headless-Treiber zeichnet nicht, ein Bild von ihm wäre leer und die Prüfung damit wertlos –
	/// besser eine deutliche Meldung als hunderte schwarze PNGs.
	/// </summary>
	private void BereiteBilderVor(string[] argumente)
	{
		string ordner = LiesText(argumente, "--bilder=");
		bool gewuenscht = ordner != null || Array.IndexOf(argumente, "--screenshots") >= 0;

		if (!gewuenscht)
			return;

		if (DisplayServer.GetName() == "headless")
		{
			GD.PrintErr("Bilder wurden angefordert, aber Godot läuft headless – dort gibt es keinen "
			            + "Renderer. Ohne --headless starten (in der CI über xvfb-run).");
			return;
		}

		_bilderOrdner = ProjectSettings.GlobalizePath(ordner ?? "user://e2e-bilder");

		try
		{
			Directory.CreateDirectory(_bilderOrdner);
		}
		catch (Exception ex)
		{
			_fehler.Add("Der Bildordner " + _bilderOrdner + " ließ sich nicht anlegen: " + ex.Message);
			return;
		}

		_mitBildern = true;
		GD.Print("Bilder je Ansicht: " + MaxBilderProAnsicht + " → " + _bilderOrdner);
	}

	/// <summary>
	/// Legt ein Bild der aktuellen Ansicht ab, solange von ihr noch nicht genug vorliegen. Vor der
	/// Aufnahme vergehen zwei Frames, damit ein gerade gesetzter Text auch wirklich gezeichnet ist.
	/// </summary>
	private async Task Schiesse(string ansicht)
	{
		if (!_mitBildern)
			return;

		_bilderJeAnsicht.TryGetValue(ansicht, out int bisher);

		if (bisher >= MaxBilderProAnsicht)
			return;

		_bilderJeAnsicht[ansicht] = bisher + 1;

		await NaechsterFrame();
		await NaechsterFrame();

		string datei = Path.Combine(_bilderOrdner, $"{++_bilder:D3}_{ansicht}_{bisher + 1}.png");
		var fehler = GetViewport().GetTexture().GetImage().SavePng(datei);

		if (fehler != Error.Ok)
			_fehler.Add("Das Bild " + datei + " ließ sich nicht schreiben (" + fehler + ").");
	}
}
