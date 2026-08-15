using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Automatischer Durchlauf des Clients (End-to-End): legt ein Spiel an, spielt eine Reihe von Jahren im
/// Hot-Seat, besucht dabei die Bereiche des Kontors und prüft am Ende, dass sich der Spielstand
/// speichern und wieder laden lässt. Geprüft werden Ablauf und Spielzustand; das genügt headless und
/// macht den Durchlauf CI-tauglich.
///
/// <code>godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=10 --spieler=2 --verbose</code>
///
/// Weitere Schalter: <c>--ohne-bereiche</c> überspringt den Rundgang durch Stadt, Schreibstube usw.,
/// <c>--ohne-speichern</c> die Speicher-/Ladeprobe, <c>--mit-ton</c> lässt den sonst stummen Ton an.
///
/// Mit <c>--screenshots</c> legt der Durchlauf zusätzlich von jeder Ansicht ein Bild ab und wird damit
/// zur visuellen Abnahme (Zielordner über <c>--bilder=&lt;pfad&gt;</c>, sonst <c>user://e2e-bilder</c>).
/// Das setzt einen laufenden Renderer voraus, geht also **nicht** headless – dessen Dummy-Treiber
/// zeichnet nicht. In der CI läuft dafür ein virtueller Bildschirm:
///
/// <code>xvfb-run -a godot --path . --rendering-method gl_compatibility --rendering-driver opengl3 \
///   "res://scenes/E2eTest.tscn" -- --screenshots</code>
///
/// Der Kompatibilitätsmodus ist nachgemessen pixelgleich zu Vulkan – der Client ist reines 2D.
///
/// Der Treiber beendet den Prozess mit Code 1, sobald etwas schiefgeht – so schlägt der CI-Schritt fehl.
///
/// Wie er den Client bedient: Das Spiel wird über dieselben Lib-Manager angelegt, die auch die Menüs
/// benutzen, dann übernimmt <see cref="Kontor.StartGame"/>. Alle Overlay-Dialoge liegen in der Gruppe
/// „Dialogs", sodass er jeden offenen Dialog findet, ohne ihn zu kennen: Ist ein Knopf da, drückt er
/// ihn, sonst schickt er ui_next_or_close. Neue Dialoge werden dadurch automatisch mitbedient.
/// </summary>
public partial class E2eTreiber : Node
{
	private const int StandardJahre = 5;
	private const int StandardSpieler = 2;

	/// <summary>
	/// Startwert des Zufallsgenerators. Fest, damit ein Durchlauf reproduzierbar ist – ohne ihn ließe
	/// sich ein fehlgeschlagener CI-Lauf nicht nachstellen. Mit <c>--seed=0</c> läuft es wieder zufällig
	/// (die Uhrzeit als Startwert), was für gelegentliche Streuungsläufe nützlich ist.
	/// </summary>
	private const int StandardSeed = 20250815;

	/// <summary>
	/// Obergrenze an Frames je Warteschritt – schützt vor einem Ablauf, der nie weiterläuft. Großzügig
	/// bemessen, weil inszenierte Sequenzen (allen voran das Duell mit seinen Sprüchen) über eine Minute
	/// laufen können; ein echter Hänger fällt trotzdem auf, spätestens am Zeitlimit des CI-Schritts.
	/// </summary>
	private const int MaxSchritte = 6000;

	/// <summary>
	/// Mindestabstand in Frames zwischen zwei Klicks auf denselben Dialog. Ohne diese Bremse würde der
	/// Treiber jeden Frame einen Rechtsklick schicken – Sequenzen, die auf das Loslassen der Taste
	/// warten, kämen dann nie voran.
	/// </summary>
	private const int KlickAbstand = 5;

	/// <summary>So viele Frames ohne offenen Dialog gelten als „der Ablauf ist zur Ruhe gekommen".</summary>
	private const int RuheFrames = 20;

	/// <summary>
	/// Die anklickbaren Bereiche des Kontors. „AreaFenster" fehlt bewusst – der Blick aus dem Fenster
	/// beendet den Zug und würde den Rundgang abbrechen.
	/// </summary>
	private static readonly string[] Bereiche =
	{
		"AreaHandel", "AreaSchreibstube", "AreaKirche", "AreaHinterzimmer", "AreaKampf"
	};

	/// <summary>
	/// So viele Bilder werden je Ansicht höchstens abgelegt. Ohne Deckel entstünden aus einem
	/// Nachrichtenschirm mit dutzenden Meldungen ebenso viele nahezu gleiche Bilder; ein paar Zustände
	/// je Ansicht sind aussagekräftig, alles darüber ist nur Ballast im Artefakt.
	/// </summary>
	private const int MaxBilderProAnsicht = 3;

	private readonly List<string> _fehler = new();

	/// <summary>Zählt je Ansicht die schon abgelegten Bilder (siehe <see cref="MaxBilderProAnsicht"/>).</summary>
	private readonly Dictionary<string, int> _bilderJeAnsicht = new();

	private Main _main;
	private string _letzterDialog = "";
	private int _framesSeitKlick;
	private int _dialogKlicks;
	private int _knopfKlicks;
	private int _besuchteBereiche;
	private int _gespielteZuege;
	private int _bilder;

	private bool _ausfuehrlich;
	private bool _mitBereichen = true;
	private bool _mitSpeicherprobe = true;
	private bool _mitBildern;
	private string _bilderOrdner;

	public override async void _Ready()
	{
		var argumente = OS.GetCmdlineUserArgs();
		int jahre = LiesZahl(argumente, "--jahre=", StandardJahre);
		int spieler = LiesZahl(argumente, "--spieler=", StandardSpieler);
		int seed = LiesZahl(argumente, "--seed=", StandardSeed, 0);

		// --seed=0 heißt „streuen": ein zufälliger Startwert, der aber ausgegeben wird – damit lässt
		// sich ein auffällig gewordener Durchlauf anschließend gezielt wiederholen.
		if (seed == 0)
			seed = System.Environment.TickCount & int.MaxValue;

		_ausfuehrlich = Array.IndexOf(argumente, "--verbose") >= 0;
		_mitBereichen = Array.IndexOf(argumente, "--ohne-bereiche") < 0;
		_mitSpeicherprobe = Array.IndexOf(argumente, "--ohne-speichern") < 0;

		// Ton aus: Ein Durchlauf im Fenstermodus lärmt sonst minutenlang (Musikwechsel, Klickgeräusche,
		// Duellstimmen). Stummgeschaltet wird nur der Master-Bus zur Laufzeit – die gespeicherten
		// Lautstärken des Spielers bleiben unangetastet. Mit --mit-ton bleibt der Ton an.
		if (Array.IndexOf(argumente, "--mit-ton") < 0)
			AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), true);

		BereiteBilderVor(argumente);

		GD.Print("=== E2E-Durchlauf: " + jahre + " Jahre, " + spieler + " Spieler, Startwert " + seed + " ===");

		try
		{
			await Spiele(jahre, spieler, seed);
		}
		catch (Exception ex)
		{
			_fehler.Add("Unerwarteter Fehler: " + ex);
		}

		Bericht(jahre);
		GetTree().Quit(_fehler.Count == 0 ? 0 : 1);
	}

	private async Task Spiele(int jahre, int spieler, int seed)
	{
		_main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
		AddChild(_main);
		await NaechsterFrame();

		LegeSpielAn(spieler, seed);
		_main.Kontor.StartGame();

		int startJahr = SW.Dynamisch.GetAktuellesJahr();
		var alterVorJahr = new Dictionary<int, int>();

		for (int jahr = 0; jahr < jahre; jahr++)
		{
			int jahrVorher = SW.Dynamisch.GetAktuellesJahr();
			MerkeAlter(alterVorJahr);

			// Ein Jahr besteht aus je einem Zug pro menschlichem Spieler (Hot-Seat).
			for (int zug = 0; zug < spieler; zug++)
			{
				if (!await SpieleEinenZug(jahrVorher))
					return;

				_gespielteZuege++;

				if (SW.Dynamisch.GetAktuellesJahr() != jahrVorher)
					break;
			}

			if (SW.Dynamisch.GetAktuellesJahr() == jahrVorher)
			{
				_fehler.Add("Nach den Zügen aller Spieler stand das Jahr weiterhin auf " + jahrVorher + ".");
				return;
			}

			PruefeZustand(jahrVorher, alterVorJahr);
			GD.Print("Jahr " + jahrVorher + " abgeschlossen → " + SW.Dynamisch.GetAktuellesJahr());
		}

		int gespielt = SW.Dynamisch.GetAktuellesJahr() - startJahr;

		if (gespielt != jahre)
			_fehler.Add("Es sollten " + jahre + " Jahre vergehen, tatsächlich waren es " + gespielt + ".");

		if (_mitSpeicherprobe)
			PruefeSpeichernUndLaden();
	}

	/// <summary>Ein Zug: warten bis bedienbar, optional die Bereiche besuchen, dann den Zug beenden.</summary>
	private async Task<bool> SpieleEinenZug(int jahrVorher)
	{
		if (!await WarteBisKontorBereit())
		{
			_fehler.Add("Der Kontor wurde im Jahr " + SW.Dynamisch.GetAktuellesJahr() + " nicht bedienbereit.");
			return false;
		}

		if (_mitBereichen && !await BesucheBereiche())
			return false;

		return await BeendeZug(jahrVorher);
	}

	/// <summary>
	/// Betritt nacheinander die Bereiche des Kontors und kehrt jeweils zurück. Das ist der Teil, der die
	/// Szenen-Verdrahtung abseits des Kontors abdeckt – Stadt, Schreibstube, Kirche, Hinterzimmer,
	/// Militär. Gehandelt oder gekauft wird bewusst nichts; es geht um Betreten und Verlassen.
	/// </summary>
	private async Task<bool> BesucheBereiche()
	{
		foreach (string name in Bereiche)
		{
			var knopf = _main.Kontor.GetNodeOrNull<BaseButton>(name);

			if (knopf == null || knopf.Disabled)
				continue;

			knopf.EmitSignal(BaseButton.SignalName.Pressed);

			string bildschirm = await WarteAufBildschirm();

			if (bildschirm == null)
			{
				_fehler.Add("Der Bereich " + name + " hat keinen Bildschirm geöffnet.");
				return false;
			}

			if (_ausfuehrlich)
				GD.Print("  Bereich " + name + " → " + bildschirm);

			await Schiesse(bildschirm);

			if (!await KehreZumKontorZurueck(name))
				return false;

			_besuchteBereiche++;
		}

		return true;
	}

	/// <summary>
	/// Wartet darauf, dass nach dem Klick auf einen Bereich tatsächlich ein Bildschirm aufgeht, und gibt
	/// dessen Namen zurück. Ohne diese Prüfung würde der Rundgang auch dann als bestanden gelten, wenn
	/// der Klick ins Leere ginge – er sähe nur einen Kontor, der sofort wieder bedienbar ist.
	/// </summary>
	private async Task<string> WarteAufBildschirm()
	{
		for (int schritt = 0; schritt < MaxSchritte; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				// Manche Bereiche melden sich erst mit einem Dialog (z. B. „Kein Stützpunkt vorhanden").
				return dialog.Name;
			}

			foreach (Node knoten in _main.GetChildren())
			{
				if (knoten is Control bildschirm && knoten != _main.Kontor
				    && bildschirm.IsVisibleInTree() && bildschirm.IsProcessingInput())
				{
					return bildschirm.Name;
				}
			}

			if (_main.Kontor.IsProcessingInput())
				return null;   // der Kontor ist gleich wieder da: der Klick ist ins Leere gelaufen

			await NaechsterFrame();
		}

		return null;
	}

	/// <summary>
	/// Verlässt den gerade betretenen Bereich wieder: bedient offene Dialoge und schickt sonst
	/// ui_next_or_close (Rechtsklick), bis der Kontor erneut Eingaben annimmt.
	/// </summary>
	private async Task<bool> KehreZumKontorZurueck(string bereich)
	{
		int ruhig = 0;

		for (int schritt = 0; schritt < MaxSchritte; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				await VersucheZuBedienen(dialog);
				ruhig = 0;
			}
			else if (_main.Kontor.IsProcessingInput())
			{
				if (++ruhig >= 3)
					return true;
			}
			else
			{
				ruhig = 0;

				// Kein Dialog offen und der Kontor ruht: Wir stehen im Bereich – zurück per Rechtsklick.
				if (schritt % 4 == 0)
					SchickeAbbruch();
			}

			await NaechsterFrame();
		}

		_fehler.Add("Aus dem Bereich " + bereich + " ging es nicht zum Kontor zurück.");
		return false;
	}

	private async Task<bool> BeendeZug(int jahrVorher)
	{
		var zugEnde = _main.Kontor.GetNodeOrNull<BaseButton>("ButtonEndTurn");

		if (zugEnde == null)
		{
			_fehler.Add("Der Knopf „Zug beenden\" wurde im Kontor nicht gefunden.");
			return false;
		}

		int spielerVorher = SW.Dynamisch.GetAktiverSpieler();
		zugEnde.EmitSignal(BaseButton.SignalName.Pressed);

		// Der Handler läuft asynchron weiter (async void); es genügt also nicht, gleich danach
		// nachzusehen. Gewartet wird, bis der Zug erkennbar gewechselt hat – entweder ist das Jahr
		// umgesprungen oder der nächste Spieler ist dran. Alles, was unterwegs aufgeht, wird bedient.
		for (int schritt = 0; schritt < MaxSchritte; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				await VersucheZuBedienen(dialog);
			}
			else if (SW.Dynamisch.GetAktuellesJahr() != jahrVorher
			         || SW.Dynamisch.GetAktiverSpieler() != spielerVorher)
			{
				return true;
			}

			await NaechsterFrame();
		}

		_fehler.Add("Der Zug im Jahr " + jahrVorher + " ließ sich nicht beenden (Dialog blieb offen?).");
		return false;
	}

	/// <summary>
	/// Speichern und Laden mit dem Spielstand, der gerade durchgespielt wurde. Das ist die schärfste
	/// Probe auf die Serialisierung: Nach etlichen Jahren steckt im Stand deutlich mehr als in einem
	/// frischen Spiel (Ämter, Familie, Erpressungen, Statistik).
	/// </summary>
	private void PruefeSpeichernUndLaden()
	{
		var speicher = new SpeicherManager(Path.Combine(Path.GetTempPath(), "conspiratio-e2e"));

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

	private async Task<bool> WarteBisKontorBereit()
	{
		int ruhig = 0;

		for (int schritt = 0; schritt < MaxSchritte; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				await VersucheZuBedienen(dialog);
				ruhig = 0;
			}
			else
			{
				// Der Zugbeginn ist eine Kette asynchroner Schritte; zwischen zwei Dialogen ist kurz
				// keiner offen. Erst eine anhaltende Ruhephase bei bedienbarem Kontor heißt „fertig".
				ruhig++;

				if (ruhig >= RuheFrames && _main.Kontor.IsProcessingInput())
				{
					await Schiesse("Kontor");
					return true;
				}
			}

			await NaechsterFrame();
		}

		return false;
	}

	private Control FindeOffenenDialog()
	{
		Control offen = null;

		foreach (Node knoten in GetTree().GetNodesInGroup("Dialogs"))
		{
			// Sichtbarkeit allein genügt nicht: Der Rundennachrichten-Schirm bleibt zwischen zwei
			// Meldungen bewusst stehen (damit nichts durchblitzt) und schaltet dabei nur die Eingabe ab.
			// Wartend – und damit zu bedienen – ist ein Dialog erst, wenn er auch Eingaben verarbeitet.
			if (knoten is Control dialog && dialog.IsVisibleInTree() && dialog.IsProcessingInput())
				offen = dialog;   // der zuletzt gefundene liegt oben
		}

		return offen;
	}

	/// <summary>
	/// Bedient den Dialog, sofern seit dem letzten Klick auf denselben Dialog genug Zeit vergangen ist.
	/// Ein neuer Dialog wird sofort bedient; bei demselben wird gewartet (<see cref="KlickAbstand"/>).
	/// Liefert, ob geklickt wurde.
	/// </summary>
	private async Task<bool> VersucheZuBedienen(Control dialog)
	{
		_framesSeitKlick++;

		if (dialog.Name != _letzterDialog)
		{
			_letzterDialog = dialog.Name;
		}
		else if (_framesSeitKlick < KlickAbstand)
		{
			return false;
		}

		// Erst das Bild, dann der Klick – sonst hielte es bereits den Folgezustand fest.
		await Schiesse(dialog.Name);

		_framesSeitKlick = 0;
		Bediene(dialog);
		_dialogKlicks++;
		return true;
	}

	private void Bediene(Control dialog)
	{
		var knopf = FindeSichtbarenKnopf(dialog);

		if (knopf != null)
		{
			if (_ausfuehrlich)
				GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → Knopf \"" + BeschriftungVon(knopf) + "\"");

			_knopfKlicks++;
			knopf.EmitSignal(BaseButton.SignalName.Pressed);
			return;
		}

		if (_ausfuehrlich)
			GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → ui_next_or_close");

		SchickeAbbruch();
	}

	/// <summary>
	/// Schickt einen Rechtsklick/Esc. Drücken **und** loslassen: Die Dialoge prüfen in _Input über
	/// Input.IsActionPressed. Bliebe die Aktion gedrückt, würde sie bei jedem weiteren Ereignis erneut
	/// auslösen und den Ablauf zerlegen.
	/// </summary>
	private static void SchickeAbbruch()
	{
		Input.ParseInputEvent(new InputEventAction { Action = "ui_next_or_close", Pressed = true });
		Input.ParseInputEvent(new InputEventAction { Action = "ui_next_or_close", Pressed = false });
	}

	private static string BeschriftungVon(BaseButton knopf)
	{
		return knopf switch
		{
			Button b => b.Text,
			LinkButton l => l.Text,
			_ => knopf.Name
		};
	}

	private static BaseButton FindeSichtbarenKnopf(Node knoten)
	{
		foreach (Node kind in knoten.GetChildren())
		{
			if (kind is BaseButton knopf && knopf.IsVisibleInTree() && !knopf.Disabled)
				return knopf;

			var tiefer = FindeSichtbarenKnopf(kind);

			if (tiefer != null)
				return tiefer;
		}

		return null;
	}

	private static void MerkeAlter(Dictionary<int, int> alter)
	{
		alter.Clear();

		for (int i = 1; i <= SW.Dynamisch.GetAktivSpielerAnzahl(); i++)
			alter[i] = SW.Dynamisch.GetHumWithID(i).GetAlter();
	}

	/// <summary>Prüft nach jedem Jahr, dass der Spielzustand plausibel geblieben ist.</summary>
	private void PruefeZustand(int jahr, Dictionary<int, int> alterVorJahr)
	{
		for (int i = 1; i <= SW.Dynamisch.GetAktivSpielerAnzahl(); i++)
		{
			var spieler = SW.Dynamisch.GetHumWithID(i);

			if (spieler == null)
			{
				_fehler.Add("Jahr " + jahr + ": Spieler " + i + " existiert nicht mehr.");
				continue;
			}

			if (spieler.GetGesundheit() < 0 || spieler.GetGesundheit() > SW.Statisch.GetMaxGesundheit())
				_fehler.Add("Jahr " + jahr + ": Gesundheit von Spieler " + i + " außerhalb des gültigen Bereichs (" + spieler.GetGesundheit() + ").");

			if (spieler.GetAlter() <= 0)
				_fehler.Add("Jahr " + jahr + ": Spieler " + i + " ist " + spieler.GetAlter() + " Jahre alt.");

			if (string.IsNullOrWhiteSpace(spieler.GetName()))
				_fehler.Add("Jahr " + jahr + ": Spieler " + i + " hat keinen Namen mehr.");

			// Das Alter steigt jedes Jahr – außer beim Erbfall, dann übernimmt ein jüngerer Erbe.
			if (alterVorJahr.TryGetValue(i, out int vorher) && spieler.GetAlter() == vorher)
				_fehler.Add("Jahr " + jahr + ": Spieler " + i + " ist nicht gealtert (" + vorher + ").");
		}
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

	private static string LiesText(string[] argumente, string praefix)
	{
		foreach (string argument in argumente)
		{
			if (argument.StartsWith(praefix))
				return argument.Substring(praefix.Length);
		}

		return null;
	}

	private static int LiesZahl(string[] argumente, string praefix, int standard, int minimum = 1)
	{
		foreach (string argument in argumente)
		{
			if (argument.StartsWith(praefix) && int.TryParse(argument.Substring(praefix.Length), out int wert) && wert >= minimum)
				return wert;
		}

		return standard;
	}

	private SignalAwaiter NaechsterFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

	private void Bericht(int jahre)
	{
		GD.Print("--- Ergebnis ---");
		GD.Print("Gespielte Jahre:   " + (SW.Dynamisch.GetAktuellesJahr() - SW.Statisch.StartJahr) + " von " + jahre + " geplant");
		GD.Print("Züge:              " + _gespielteZuege);
		// Rechtsklicks fallen bei inszenierten Sequenzen (Duell) reichlich an, ohne etwas zu bewirken –
		// aussagekräftig ist vor allem, wie oft wirklich ein Knopf gedrückt wurde.
		GD.Print("Klicks in Dialogen:" + _dialogKlicks + " (davon Knöpfe: " + _knopfKlicks + ")");
		GD.Print("Besuchte Bereiche: " + _besuchteBereiche);

		if (_mitBildern)
			GD.Print("Bilder:            " + _bilder + " von " + _bilderJeAnsicht.Count + " Ansichten");

		if (_fehler.Count == 0)
		{
			GD.Print("E2E-Durchlauf bestanden.");
			return;
		}

		GD.PrintErr("E2E-Durchlauf fehlgeschlagen (" + _fehler.Count + "):");

		foreach (string fehler in _fehler)
			GD.PrintErr("  - " + fehler);
	}
}
