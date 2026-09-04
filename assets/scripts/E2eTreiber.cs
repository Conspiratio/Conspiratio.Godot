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
/// Automatischer Durchlauf des Clients (End-to-End): legt ein Spiel an, spielt eine Reihe von Jahren im
/// Hot-Seat, besucht dabei die Bereiche des Kontors und prüft am Ende, dass sich der Spielstand
/// speichern und wieder laden lässt. Geprüft werden Ablauf und Spielzustand; das genügt headless und
/// macht den Durchlauf CI-tauglich.
///
/// <code>godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=10 --spieler=2 --verbose</code>
///
/// Weitere Schalter: <c>--ohne-bereiche</c> überspringt den Rundgang durch Stadt, Schreibstube usw.,
/// <c>--ohne-speichern</c> die Speicher-/Ladeprobe, <c>--mit-ton</c> lässt den sonst stummen Ton an.
/// <c>--ohne-menue</c> legt das Spiel wieder direkt über die Lib-Manager an statt über die Menüs.
///
/// In den Bereichen werden auch deren Knöpfe betätigt (Handel, Bewerbung, Kredit, Spionage …), nicht nur
/// der Bereich betreten – das erreicht den Großteil der Spiellogik. <c>--ohne-aktionen</c> beschränkt den
/// Rundgang wieder aufs Betreten und Verlassen.
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
/// Mit <c>--zustaende</c> stellt der Treiber ab einem festen Jahr je Zug einen Spielzustand her, den
/// bloße Spielzeit nicht hervorbringt, und prüft anschließend, dass die zugehörige Ansicht wirklich
/// zu sehen war (siehe <see cref="StelleZustaendeHer"/>). Bleibt eine davon aus, schlägt der Lauf fehl.
/// Er braucht dafür Platz: Die Zustände beginnen im sechsten Jahr und brauchen je einen Zug, ein
/// kürzerer Lauf als etwa zwölf Jahre scheitert also an der eigenen Zusicherung.
///
/// <code>godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=20 --spieler=1 --zustaende</code>
///
/// <b>Der Schalter gehört in keine Messung</b>: Er verschafft dem Spieler Amt, Taler, einen Prozess
/// und einen Stützpunkt, die er sich nicht erspielt hat, und verzerrt damit genau das Vermögensband,
/// gegen das dieses Projekt kalibriert. Aus demselben Grund steht er standardmäßig aus.
///
/// Der Treiber beendet den Prozess mit Code 1, sobald etwas schiefgeht – so schlägt der CI-Schritt fehl.
///
/// Wie er den Client bedient: Das Spiel entsteht wie beim Spieler über Hauptmenü, Spielanlage und
/// Spielererstellung (<see cref="LegeSpielUeberMenueAn"/>). Alle Overlay-Dialoge liegen in der Gruppe
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
	/// Obergrenze an Frames je Warteschritt – schützt vor einem Ablauf, der nie weiterläuft.
	///
	/// Sehr großzügig bemessen, und das mit Grund: Ein Duell unter zwei menschlichen Spielern läuft über
	/// eine Minute, und headless zählt die Engine deutlich mehr Frames pro Sekunde als auf dem Bildschirm.
	/// Mit 6000 Frames brach der Treiber mitten im Wortgefecht ab und meldete einen Hänger, den es nicht
	/// gab. Ein echter Hänger fällt trotzdem auf – spätestens am Zeitlimit des CI-Schritts.
	/// </summary>
	private const int MaxSchritte = 20000;

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

	/// <summary>
	/// So viele Knöpfe werden je Bereichsbesuch betätigt. Alle bei jedem Besuch zu drücken bläht einen
	/// einzelnen Zug auf; über die Züge eines Durchlaufs kommt so trotzdem jeder an die Reihe.
	/// </summary>
	private const int MaxAktionenProBereich = 2;

	/// <summary>
	/// Untergrenze des Talerpolsters. Ein Kaufmann investiert aus Überschuss, nicht bis an den Rand der
	/// Zahlungsunfähigkeit – sonst reißt der nächste Kostenblock ihn ins Minus. Die tatsächliche
	/// Rücklage wächst mit dem Betrieb, siehe <see cref="BerechneRuecklage"/>.
	/// </summary>
	private const int RuecklageMindestens = 1500;

	/// <summary>
	/// So oft wird in einem Dialog ein Knopf gedrückt, bevor der Treiber ihn per Rechtsklick verlässt.
	/// Ohne diese Grenze bliebe er in Dialogen mit Reitern hängen: Dort führt kein Knopf hinaus, und der
	/// immer gleiche erste Knopf (der erste Reiter) bringt den Ablauf nicht weiter.
	/// </summary>
	private const int MaxKlicksProDialog = 3;

	/// <summary>
	/// Obergrenze an Klicks, die eine einzelne Handlung nach sich ziehen darf, bevor der Treiber
	/// abbricht und schließt. Notwendig, weil <see cref="MaxKlicksProDialog"/> bei jedem Dialogwechsel
	/// neu beginnt: Pendeln zwei Dialoge gegeneinander – ein Kaufversuch ohne Geld und die Meldung
	/// „Ihr habt nicht genügend Taler" – erreicht jener Zähler sein Limit nie, und der Treiber klickt
	/// bis zum Zeitablauf im Kreis. Diese Grenze zählt über alle Dialoge einer Handlung hinweg.
	/// </summary>
	private const int MaxKlicksProAktion = 15;

	/// <summary>
	/// Ab diesem Spieljahr (gezählt ab <c>SW.Statisch.StartJahr</c>) stellt <c>--zustaende</c> die
	/// fehlenden Zustände her. Fest statt sofort, aus zwei Gründen: Der gewöhnliche Ablauf soll ein
	/// paar Jahre ungestört laufen, und der letzte Schritt (der erfüllte Auftrag) beendet das Spiel –
	/// begonnen im ersten Jahr wäre der Durchlauf nach vier Zügen vorbei.
	/// </summary>
	private const int ZustaendeAbJahr = 5;

	/// <summary>
	/// Aufschlag auf den Schätzwert, mit dem der Treiber einen Stützpunkt kauft. Der Besitzer nimmt
	/// nicht schon an, weil das Geld reicht: <c>Stuetzpunkt.KaufangebotAbgeben</c> würfelt gegen
	/// <c>value = Beziehung + Ansehen/10 + Religionssympathie</c> und braucht mindestens 100 – bei
	/// einem knappen Angebot ist das eine Chance von 1 zu 6. Erst ein Angebot über dem Wert zählt mit
	/// <c>(Preis − Wert) / 1000</c> in <c>value</c> hinein; eine Million Aufschlag hebt die Annahme damit
	/// auf rund 90 %. Der Betrag bleibt bewusst unter der Gesetzesgrenze „Maximale Taler“ (ab zwei
	/// Millionen), damit der Kauf nicht nebenbei eine Straftat bucht.
	/// </summary>
	private const int AngebotsAufschlag = 1000000;

	/// <summary>
	/// Die vier Ansichten, die kein Durchlauf je erreicht hat, weil sie einen Spielzustand voraussetzen
	/// und nicht mehr Spielzeit: eine Anklage, eine Wahl mit menschlicher Beteiligung, ein eigener
	/// Stützpunkt, ein erfüllter Auftrag. Die Reihenfolge ist die Reihenfolge der Schritte in
	/// <see cref="StelleZustaendeHer"/> – der Auftragssieg steht zuletzt, weil er das Spiel beendet.
	/// </summary>
	private static readonly string[] Zustandsansichten =
	{
		nameof(GerichtDialog), nameof(WahlDialog), nameof(StuetzpunktVerwalten), nameof(AuftragSiegDialog)
	};

	/// <summary>
	/// Knöpfe, die der Treiber nicht drückt, weil sie den Durchlauf beenden statt ihn zu prüfen.
	/// Bewusst kurz gehalten: Eine Handlung, die nur scheitert oder Geld kostet, ist erwünscht – auch
	/// der Weg in den Schuldturm ist ein Pfad, der geprüft gehört.
	/// </summary>
	/// <summary>Dialoge, die der Treiber über einen bestimmten Knopf verlässt statt frei zu klicken.</summary>
	private static readonly Dictionary<string, string> Ausstiegsknopf = new()
	{
		// Das Ingame-Menü geht auf, sobald ein Rechtsklick den Kontor trifft – also regelmäßig, sobald der
		// Treiber einen Bildschirm verlässt. Es ist Verwaltung, kein Spielinhalt: „Spiel laden" und
		// „Spieler hinauswerfen" führen aus dem Durchlauf heraus. Ein Rechtsklick schließt es nicht,
		// deshalb wird gezielt „Weiter" gedrückt.
		{ "IngameMenuDialog", "Rahmen/VBox/ButtonWeiter" }
	};

	private static readonly HashSet<string> Gesperrt = new()
	{
		"AreaFenster",   // „Geld zum Fenster rauswerfen": nimmt den Spieler aus dem Spiel
		"ButtonBeenden",
		"ButtonHauptmenue",

		// „Fehler melden" packt ein ZIP und öffnet das Mailprogramm des Systems. Beides hat in einem
		// automatischen Lauf nichts zu suchen: Es prüft nicht das Spiel, sondern die Umgebung, und
		// verhält sich je nach Betriebssystem anders.
		"ButtonFeedback",
		"ButtonFehlerMelden"
	};

	private readonly List<string> _fehler = new();

	/// <summary>Welche Knöpfe im Lauf betätigt wurden – die Abdeckung, die der Bericht ausweist.</summary>
	private readonly HashSet<string> _bedienteKnoepfe = new();

	/// <summary>
	/// Eigener Zufall für die Auswahl der Handlungen. Bewusst getrennt vom Spielzufall: Sonst würde
	/// schon die Wahl eines Knopfes den weiteren Spielverlauf verschieben und zwei Läufe mit gleichem
	/// Startwert liefen auseinander, sobald sich an der Auswahl etwas ändert.
	/// </summary>
	private Random _auswahl;

	/// <summary>Zählt je Ansicht die schon abgelegten Bilder (siehe <see cref="MaxBilderProAnsicht"/>).</summary>
	private readonly Dictionary<string, int> _bilderJeAnsicht = new();

	private Main _main;
	private string _letzterDialog = "";
	private int _klicksAmSelbenDialog;

	/// <summary>
	/// Wurde im gerade offenen Dialog schon einmal ein Eingabefeld abgeschickt? Siehe
	/// <see cref="Bediene"/>: Ohne diese Merkung verdrängt der Eingabe-Ausweg den Rechtsklick für
	/// immer.
	/// </summary>
	private bool _eingabeGeschickt;
	private int _klicksSeitAktion;

	/// <summary>
	/// Läuft gerade eine Handlung aus einem Bereich? Nur dann wird gewürfelt und gedeckelt. Außerhalb –
	/// beim Zugbeginn oder in einem Duell – gilt die schlichte Bedienung des Standardpfads: Ein Duell
	/// verlangt eine Auswahl, und ein Treiber, der dort nur noch Rechtsklicks schickt, bringt es nie
	/// zu Ende.
	/// </summary>
	private bool _inAktion;

	/// <summary>
	/// Läuft gerade das Zugende? Dann werden Dialoge bestätigt statt zufällig bedient: Der Kontor fragt
	/// „Wollt Ihr Euren Zug wirklich beenden?", und ein gewürfeltes „Nein" nähme dem Treiber genau die
	/// Absicht, die er eben gefasst hat – der Zug endete nie und der Durchlauf liefe in den Zeitablauf.
	/// </summary>
	private bool _imZugende;
	private int _framesSeitKlick;
	private int _dialogKlicks;
	private int _knopfKlicks;
	private int _besuchteBereiche;
	private int _besuchteStaedte;
	private int _verkaufteWaren;
	private int _exportierteWaren;
	private int _exportAbgelehnt;
	private int _gespielteZuege;
	private int _bilder;

	private bool _ausfuehrlich;
	private bool _mitBereichen = true;
	private bool _mitAktionen = true;
	private bool _ueberMenue = true;

	/// <summary>Ist <c>--zustaende</c> gesetzt? Ohne den Schalter ist der ganze Block wirkungslos.</summary>
	private bool _mitZustaenden;

	/// <summary>Welche der <see cref="Zustandsansichten"/> tatsächlich sichtbar waren.</summary>
	private readonly HashSet<string> _erreichteAnsichten = new();

	/// <summary>Der nächste herzustellende Zustand als Index in <see cref="Zustandsansichten"/>.</summary>
	private int _zustandSchritt;

	/// <summary>
	/// Wovon die Zustandsherstellung schon einmal berichtet hat. Siehe
	/// <see cref="MeldeZustandsfehler"/>: Die Schritte laufen jedes Jahr erneut, ihre Fehler dürfen es
	/// nicht.
	/// </summary>
	private readonly HashSet<string> _zustandsfehler = new();

	/// <summary>Steht ein Spielzustand bereit? Erst dann darf der Bericht ihn auswerten.</summary>
	private bool _spielLaeuft;

	/// <summary>
	/// Ist das Spiel regulär zu Ende gegangen? Für den Treiber sieht das zunächst genauso aus wie ein
	/// Hänger – es kommt kein Kontor mehr –, ist aber ein gültiger Ausgang und kein Fehler.
	/// </summary>
	private bool _spielBeendet;

	/// <summary>Jahr des regulären Spielendes; nur gültig, wenn <see cref="_spielBeendet"/> gesetzt ist.</summary>
	private int _endeJahr;

	/// <summary>Stand von <c>_verkaufteWaren</c> am Ende des Vorjahres, für die Jahresdifferenz.</summary>
	private int _verkauftVorjahr;

	/// <summary>
	/// Das Polster, das der Treiber im laufenden Zug nicht antastet. Wird von der Handelsrunde aus der
	/// Betriebsgröße gesetzt und danach auch außerhalb der Stadt beachtet – etwa bei der Kupplerin, die
	/// sonst genau das Geld ausgibt, das die Handelsrunde als Reserve stehen gelassen hat.
	/// </summary>
	private int _ruecklage = RuecklageMindestens;

	private bool _mitSpeicherprobe = true;
	private bool _mitBildern;
	private string _bilderOrdner;
	private int _aggressivitaet;

	/// <summary>
	/// Ordner für alles, was der Durchlauf schreibt: Autosaves, Profile, Bestenlisten und die
	/// Speicherprobe. Liegt im Temp-Bereich, damit der Lauf die echten Spielstände nicht anfasst.
	/// </summary>
	private static string SpielstandOrdner => Path.Combine(Path.GetTempPath(), "conspiratio-e2e");

	public override async void _Ready()
	{
		// Als Allererstes umlenken – noch vor dem Anlegen von Main, dessen Kontor, Profil- und
		// Bestenlisten-Manager sonst am echten Spielstandordner hängen. Warum das sein muss, steht bei
		// ClientSettings.UeberschreibeSavegamePath: Der Autosave räumt gleichnamige Stände des Spielers weg.
		ClientSettings.UeberschreibeSavegamePath(SpielstandOrdner);

		var argumente = OS.GetCmdlineUserArgs();
		int jahre = LiesZahl(argumente, "--jahre=", StandardJahre);
		int spieler = LiesZahl(argumente, "--spieler=", StandardSpieler);
		int seed = LiesZahl(argumente, "--seed=", StandardSeed, 0);

		// --seed=0 heißt „streuen": ein zufälliger Startwert, der aber ausgegeben wird – damit lässt
		// sich ein auffällig gewordener Durchlauf anschließend gezielt wiederholen.
		if (seed == 0)
			seed = System.Environment.TickCount & int.MaxValue;

		_aggressivitaet = LiesZahl(argumente, "--aggressivitaet=", 0, 0);
		_ausfuehrlich = Array.IndexOf(argumente, "--verbose") >= 0;
		_mitBereichen = Array.IndexOf(argumente, "--ohne-bereiche") < 0;
		_mitSpeicherprobe = Array.IndexOf(argumente, "--ohne-speichern") < 0;

		_mitAktionen = Array.IndexOf(argumente, "--ohne-aktionen") < 0;
		_ueberMenue = Array.IndexOf(argumente, "--ohne-menue") < 0;

		// Aus statt an: Der Schalter erkauft die Abdeckung mit Cheats und hat in einer Messung nichts
		// verloren. Er meldet sich deshalb auch deutlich im Protokoll.
		_mitZustaenden = Array.IndexOf(argumente, "--zustaende") >= 0;

		// Ton aus: Ein Durchlauf im Fenstermodus lärmt sonst minutenlang (Musikwechsel, Klickgeräusche,
		// Duellstimmen). Stummgeschaltet wird nur der Master-Bus zur Laufzeit – die gespeicherten
		// Lautstärken des Spielers bleiben unangetastet. Mit --mit-ton bleibt der Ton an.
		if (Array.IndexOf(argumente, "--mit-ton") < 0)
			AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), true);

		_auswahl = new Random(seed);

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

		if (_ueberMenue)
		{
			if (!await LegeSpielUeberMenueAn(spieler, seed))
				return;
		}
		else
		{
			LegeSpielAn(spieler, seed);
			_main.Kontor.StartGame();
		}

		_spielLaeuft = true;

		// Den Startwert ein zweites Mal setzen, jetzt wo das Spiel steht. Damit haengt der Verlauf ab dem
		// ersten Zug nur noch am Startwert, nicht mehr daran, wie viele Zufallszahlen die Spielanlage
		// unterwegs verbraucht hat. Ohne das entwertet jede Aenderung am Anlageweg saemtliche notierten
		// Startwerte - genau das ist beim Umbau auf die Menuefuehrung passiert.
		//
		// Was es nicht leistet: Der Ausgangszustand (Bosheit der KI, Heimatstaedte, Lebensjahre) stammt
		// aus der Anlage und bleibt zwischen den beiden Wegen verschieden. Gleich wird der Zufallsstrom,
		// nicht die Ausgangslage.
		SW.Statisch.SetRnd(seed);

		// KI-Aggressivität (1–100). Ohne Angabe bleibt es beim Standard 50, also dem Verhalten, das
		// dieser Treiber seit jeher misst. Mit --aggressivitaet=N lässt sich prüfen, ob das Spiel bei
		// hohen Werten noch spielbar bleibt – die Einstellung wirkt sofort, weil die Bosheit beim Lesen
		// moduliert wird.
		if (_aggressivitaet > 0)
		{
			SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = _aggressivitaet;
			GD.Print("KI-Aggressivitaet: " + _aggressivitaet + " %");
		}

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

			// Der Talerstand gehört in die Jahreszeile, nicht nur in den Schlussbericht: Ein Lauf, der
			// negativ endet, verrät sonst nicht, ob er langsam abrutschte oder in einem einzigen Jahr
			// einbrach. Genau diese Frage liess sich bei der zweigipfligen Grundlinie aus den Logs nicht
			// beantworten, obwohl beide Läufe reproduzierbar vorlagen.
			GD.Print("Jahr " + jahrVorher + " abgeschlossen → " + SW.Dynamisch.GetAktuellesJahr()
			         + " (" + Talerstand() + ")");
		}

		int gespielt = SW.Dynamisch.GetAktuellesJahr() - startJahr;

		// Mindestens, nicht genau: Sitzt ein Spieler im Schuldturm, überspringt der Client seinen Zug –
		// dann laufen in einem Schleifendurchlauf zwei Jahreswechsel. Über 30 Jahre kamen so 33 heraus,
		// jedes Mal unmittelbar nach einem Schuldenprozess. Das ist richtiges Spielverhalten; zu wenige
		// Jahre wären dagegen ein Zeichen, dass der Durchlauf irgendwo stecken geblieben ist.
		if (gespielt < jahre)
			_fehler.Add("Es sollten mindestens " + jahre + " Jahre vergehen, tatsächlich waren es " + gespielt + ".");

		if (_mitSpeicherprobe)
			PruefeSpeichernUndLaden();
	}

	/// <summary>Ein Zug: warten bis bedienbar, optional die Bereiche besuchen, dann den Zug beenden.</summary>
	private async Task<bool> SpieleEinenZug(int jahrVorher)
	{
		if (!await WarteBisKontorBereit())
		{
			// Kein Kontor mehr kann zweierlei heißen. Ist das Spiel regulär zu Ende, ist der Durchlauf
			// fertig und nicht kaputt – gemeldet wird er im Bericht, nicht als Fehler.
			if (_spielBeendet)
				return false;

			_fehler.Add("Der Kontor wurde im Jahr " + SW.Dynamisch.GetAktuellesJahr()
			            + " nicht bedienbereit. " + Zustandsbericht());
			return false;
		}

		// Vor dem Rundgang, damit der herzustellende Zustand noch in diesen Zug wirkt: Die
		// Gerichtsverhandlung fällt am Zugende, die Wahl am Jahresende.
		if (_mitZustaenden && !await StelleZustaendeHer(nachRundgang: false))
			return false;

		if (_mitBereichen && !await BesucheBereiche())
			return false;

		// Der Auftragssieg dagegen erst danach: Sein Ziel ist ein Talerstand, und der Rundgang gibt im
		// selben Zug wieder Geld aus. Warum das die Prüfung sonst verfehlt, steht bei
		// BereiteAuftragssiegVor.
		if (_mitZustaenden && !await StelleZustaendeHer(nachRundgang: true))
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

			// Aus demselben Grund wie die Heimatstadt laufen Brautwerbung und Testament immer mit: ohne
			// Ehe keine Kinder, ohne bestimmten Erben keine Dynastie ueber den ersten Todesfall hinaus.
			if (bildschirm == nameof(Kirche))
			{
				await WirbUmPartner();
				await BestimmeErben();
			}

			// Die Heimatstadt wird immer angesteuert: Produktion und Verkauf sind der Kern des Spiels,
			// keine der zufaelligen Handlungen, die --ohne-aktionen abschaltet.
			if (_mitAktionen || bildschirm == nameof(Weltkarte))
				await BedieneBildschirm(bildschirm);

			if (!await KehreZumKontorZurueck(name))
				return false;

			_besuchteBereiche++;
		}

		return true;
	}

	/// <summary>
	/// Sucht in der Kirche die Kupplerin auf und wirbt um einen Ehepartner. Läuft wie die Handelsrunde
	/// **immer** mit, auch mit <c>--ohne-aktionen</c>: Ohne Ehe gibt es keinen Erben, und ohne Erben
	/// beendet der erste Todesfall die Dynastie (<c>Kontor.cs</c>, Pfad <c>testament.SpielVorbei</c>).
	/// Gemessen kam deshalb kein Lauf über rund 25 Jahre hinaus, und eine Spätspiel-Messung war
	/// unmöglich. Der Fortbestand der Dynastie ist Kern des Spiels, keine Zufallshandlung.
	///
	/// Die Kirche lehnt von sich aus ab, wenn nichts zu werben ist – schon verheiratet, bereits werbend,
	/// kein passender Vorschlag, zu teuer. Jeder dieser Wege endet in einer schlichten Textmeldung, die
	/// die übliche Dialogbedienung wegklickt; Sonderfälle braucht es hier also nicht. Die Vorabfrage
	/// spart nur den Weg, damit die Kupplerin nicht jedes Jahr aufs Neue behelligt wird.
	/// </summary>
	private async Task WirbUmPartner()
	{
		if (!new FamilieManager().KannPartnerSuchen(out _))
			return;

		// Nur aus echtem Überschuss werben. Die Handelsrunde läuft im selben Zug vor der Kirche und hat
		// bis auf die Rücklage alles investiert; die Kupplerin nähme ihren Lohn also genau aus dem
		// Polster. Gemessen war das der Tropfen, der drei von zehn Läufen in den Schuldturm kippte.
		// Der doppelte Betrag, weil der Preis der Kupplerin vorher nicht feststeht – ihn abzufragen
		// hieße, den Vorschlag zweimal zu ziehen und damit den Zufallsstrom zu verschieben.
		if (SW.Dynamisch.GetAktHum().GetTaler() < _ruecklage * 2)
			return;

		var kirche = _main.GetNodeOrNull<Control>(nameof(Kirche));
		var hochzeit = kirche?.GetNodeOrNull<BaseButton>("AreaHochzeit");

		if (hochzeit == null)
		{
			_fehler.Add("In der Kirche wurde der Bereich „AreaHochzeit\" nicht gefunden.");
			return;
		}

		if (_ausfuehrlich)
			GD.Print("    Brautwerbung: Kupplerin aufgesucht");

		_bedienteKnoepfe.Add("Kirche.Brautwerbung");
		hochzeit.EmitSignal(BaseButton.SignalName.Pressed);

		// Die Zusage selbst faellt in der ueblichen Dialogbedienung: Ausserhalb einer Aktion drueckt die
		// den ersten sichtbaren Knopf, und das ist im Ja/Nein-Dialog „Ja". Ebenso beim jaehrlichen
		// Werbegeschenk, wo der erste Knopf die billigste echte Gabe ist – „nichts schenken" haengt der
		// BrautwerbungDialog bewusst hinten an, und es wuerde die Werbung nie voranbringen.
		await WarteBisBildschirmZurueck(nameof(Kirche), "Kirche.Brautwerbung");
	}

	/// <summary>
	/// Bestimmt im Testament einen Erben. Ohne diesen Schritt bleibt die Ehe wirkungslos:
	/// <c>FamilieManager.FuehreTestamentAus</c> liest <c>GetErbeSpielerID()</c>, und steht die auf 0,
	/// endet die Dynastie beim ersten Todesfall – **Kinder erben nicht von allein**. Gemessen: Mit Ehe,
	/// aber ohne Testament kam ein 40-Jahre-Lauf auf 25 bzw. 33 Jahre statt auf 40.
	///
	/// Gewählt wird die letzte Option, denn <c>GetErbeOptionen</c> listet in fester Reihenfolge das
	/// Erzbistum (Id 0, also kein Erbe), dann den Ehepartner, dann die Kinder. Ein Kind trägt die
	/// Dynastie weiter; der etwa gleichaltrige Ehepartner nur eine Generation.
	/// </summary>
	private async Task BestimmeErben()
	{
		var familie = new FamilieManager();
		var optionen = familie.GetErbeOptionen();

		// Nur das Erzbistum zur Wahl: unverheiratet und kinderlos, es gibt nichts zu bestimmen.
		if (optionen.Count <= 1)
			return;

		int ziel = optionen[optionen.Count - 1].ErbeId;

		if (familie.GetAktuellerErbeId() == ziel)
			return;

		var kirche = _main.GetNodeOrNull<Control>(nameof(Kirche));
		var bereich = kirche?.GetNodeOrNull<BaseButton>("AreaTestament");

		if (bereich == null)
		{
			_fehler.Add("In der Kirche wurde der Bereich „AreaTestament\" nicht gefunden.");
			return;
		}

		bereich.EmitSignal(BaseButton.SignalName.Pressed);

		if (!await WarteAufSichtbar(nameof(TestamentDialog)))
		{
			_fehler.Add("Das Testament ging nicht auf. " + Zustandsbericht());
			return;
		}

		// Ein einziger Knopf blaettert durch die Erben und setzt dabei jedes Mal den neuen. Hoechstens so
		// oft druecken, wie es Optionen gibt – dann ist die Liste einmal ganz herum.
		var knopf = _main.TestamentDialog.GetNodeOrNull<BaseButton>(_main.TestamentDialog.ButtonErbePath);

		for (int i = 0; knopf != null && i < optionen.Count && familie.GetAktuellerErbeId() != ziel; i++)
		{
			knopf.EmitSignal(BaseButton.SignalName.Pressed);
			await NaechsterFrame();
		}

		_bedienteKnoepfe.Add("Kirche.Testament");

		// Ueber den eigenen Schliessknopf, nicht per Rechtsklick: Der wirkt erst im naechsten Frame, und
		// bis dahin uebernimmt die allgemeine Dialogbedienung den noch offenen Dialog – sie drueckt
		// denselben Blaetterknopf weiter und stellte den eben bestimmten Erben gemessen bis zurueck aufs
		// Erzbistum. Der Schliessknopf ist jeder Knopf ausser dem Blaetterknopf.
		var knoepfe = new List<BaseButton>();
		SammleKnoepfe(_main.TestamentDialog, knoepfe);
		var schliessen = knoepfe.Find(k => k != knopf);

		if (schliessen != null)
			schliessen.EmitSignal(BaseButton.SignalName.Pressed);
		else
			SchickeAbbruch();

		// Vor dem Warten pruefen, solange der Stand noch der eben gesetzte ist.
		int erbe = familie.GetAktuellerErbeId();

		if (erbe == 0)
			_fehler.Add("Im Testament blieb der Erbe auf „kein Erbe\", obwohl " + optionen.Count
			            + " Optionen zur Wahl standen.");
		else if (_ausfuehrlich)
			GD.Print("    Testament: Erbe bestimmt – " + familie.GetErbeBezeichnung(erbe));

		await WarteBisBildschirmZurueck(nameof(Kirche), "Kirche.Testament");
	}

	/// <summary>
	/// Betätigt im gerade offenen Bereichsbildschirm ein paar seiner Knöpfe – das ist der Teil, der die
	/// eigentlichen Spielhandlungen erreicht: Handel, Bewerbung, Kredit, Beichte, Spionage. Vorher wurde
	/// jeder Bereich nur betreten und sofort wieder verlassen, sodass alles dahinter ungeprüft blieb.
	///
	/// Nicht alle Knöpfe je Besuch, sondern <see cref="MaxAktionenProBereich"/> zufällig gewählte: Über
	/// die Züge eines Durchlaufs kommt trotzdem alles an die Reihe, ohne dass ein einzelner Zug ausufert.
	/// Der Zufall stammt aus einem eigenen Generator, damit die Auswahl den Spielzufall nicht verschiebt.
	///
	/// Eine abgelehnte Handlung ist ausdrücklich kein Fehler: „Ihr habt nicht genug Taler" ist richtiges
	/// Verhalten. Gemeldet werden nur Abstürze, Hänger und unplausibler Zustand.
	/// </summary>
	private async Task BedieneBildschirm(string bildschirm)
	{
		// Die Karten kennen keine Knöpfe – sie werden über die Mausposition bedient.
		if (bildschirm == nameof(Weltkarte))
		{
			await BesucheHeimatstadt();
			return;
		}

		if (bildschirm == nameof(SoeldnerRaeuberKarte))
		{
			await BesucheStuetzpunkt();
			return;
		}

		var knoten = _main.GetNodeOrNull<Control>(bildschirm);

		if (knoten == null)
			return;

		var knoepfe = new List<BaseButton>();
		SammleKnoepfe(knoten, knoepfe);
		knoepfe.RemoveAll(k => Gesperrt.Contains(k.Name.ToString()));

		for (int i = 0; i < MaxAktionenProBereich && knoepfe.Count > 0; i++)
		{
			var knopf = knoepfe[_auswahl.Next(knoepfe.Count)];
			knoepfe.Remove(knopf);

			string bezeichnung = bildschirm + "." + knopf.Name;
			_bedienteKnoepfe.Add(bezeichnung);

			if (_ausfuehrlich)
				GD.Print("    Aktion: " + bezeichnung);

			_klicksSeitAktion = 0;
			_inAktion = true;
			knopf.EmitSignal(BaseButton.SignalName.Pressed);

			bool zurueck = await WarteBisBildschirmZurueck(bildschirm, bezeichnung);
			_inAktion = false;

			if (!zurueck)
				return;
		}
	}

	/// <summary>
	/// Klickt auf der Handelskarte die Heimatstadt an und bedient die Stadtansicht. Ohne diesen Umweg
	/// erreicht der Durchlauf die Stadt nie – und damit weder Handel noch Werkstätten noch Anwesen.
	/// Die Karte wertet echte Mausereignisse aus, also werden Bewegung und Klick auch so geschickt.
	/// </summary>
	private async Task BesucheHeimatstadt()
	{
		// Die Stadt mit dem eigenen Wohnsitz: Dort stehen die eigenen Werkstätten, der Besuch führt also
		// in die aussagekräftigste Stadtansicht. Ohne Wohnsitz (nach einem Erbfall möglich) irgendeine.
		int stadtId = SW.Dynamisch.GetAktHum().GetFirstStadtIDMitWohnsitz();

		if (stadtId <= 0)
			stadtId = SW.Statisch.GetMinStadtID();

		var mitte = _main.Weltkarte.GetStadtMitte(stadtId);

		if (mitte == Vector2.Zero)
		{
			_fehler.Add("Die Heimatstadt " + stadtId + " hat auf der Karte keine Position.");
			return;
		}

		// Erst bewegen (die Karte merkt sich die überfahrene Stadt), dann klicken.
		Input.ParseInputEvent(new InputEventMouseMotion { Position = mitte, GlobalPosition = mitte });
		await NaechsterFrame();
		Input.ParseInputEvent(new InputEventMouseButton
		{
			Position = mitte, GlobalPosition = mitte, ButtonIndex = MouseButton.Left, Pressed = true
		});
		Input.ParseInputEvent(new InputEventMouseButton
		{
			Position = mitte, GlobalPosition = mitte, ButtonIndex = MouseButton.Left, Pressed = false
		});

		if (!await WarteAufSichtbar(nameof(Stadt), 60))
		{
			// Headless gibt es kein Fenster und kein Mausgerät, synthetische Mausereignisse erreichen die
			// Karte dort nicht. Damit die Stadtansicht trotzdem geprüft wird, öffnet der Treiber sie dann
			// direkt – denselben Aufruf macht die Karte beim echten Klick auch (Weltkarte.StadtAngeklickt).
			_main.Weltkarte.Hide();
			_main.Weltkarte.SetProcessInput(false);
			_main.Stadt.ShowStadt(stadtId);

			if (!await WarteAufSichtbar(nameof(Stadt), 60))
			{
				_fehler.Add("Die Stadtansicht für Stadt " + stadtId + " ließ sich nicht öffnen.");
				return;
			}
		}

		_besuchteStaedte++;
		await Schiesse(nameof(Stadt));
		await FuehreHandelsrundeDurch(stadtId);

		if (_mitAktionen)
			await BedieneBildschirm(nameof(Stadt));

		// Zurück auf die Karte, damit der Aufrufer von dort aus zum Kontor findet.
		SchickeAbbruch();
		await NaechsterFrame();
	}

	/// <summary>
	/// Spielt in der offenen Stadt eine Handelsrunde: produzieren lassen und den vorhandenen Bestand
	/// verkaufen. Das ist der eigentliche Kern des Spiels – ohne ihn erwirtschaftet der Durchlauf nichts,
	/// und alles, was am Vermögen hängt (Abrechnung, Steuern, Kredite, Schuldturm, Auftragsziele), läuft
	/// mit unrealistischen Zahlen oder gar nicht.
	///
	/// Bewusst gezielt statt zufällig: Produktion braucht Tätigkeit, Ware, Arbeiter und Stätten in
	/// sinnvoller Kombination – erwürfelt kommt dabei nichts heraus.
	///
	/// Die Stättenzahl wird nicht mehr fest vorgegeben, sondern wie bei einem Kaufmann aus den beiden
	/// Grenzen abgeleitet, die tatsächlich binden (siehe <see cref="ErmittleSinnvolleStaetten"/>).
	/// Gemessen gegen die Lib brachte das über 15 Jahre 33 311 statt 4 979 Taler – die alte feste
	/// Vorgabe „eine Stätte" ließ über 40 % des Lagers und 92 der 99 möglichen Arbeiter brachliegen.
	///
	/// Bedient wird über die Steuerelemente des Bildschirms, damit der Client-Pfad läuft.
	/// </summary>
	private async Task FuehreHandelsrundeDurch(int stadtId)
	{
		var handel = new HandelsManager();
		var stadt = _main.Stadt;

		// Ohne eigene Werkstätte gibt es nichts zu produzieren. Die Plätze sind 1-basiert (ButtonWs1…6).
		int werkstattNr = -1;

		for (int nr = 1; nr <= 6 && werkstattNr < 0; nr++)
		{
			if (handel.HatWerkstatt(stadtId, nr))
				werkstattNr = nr;
		}

		if (werkstattNr < 0)
			return;

		// Slot 0 auf „Produzieren" schalten. Der Knopf rotiert durch die vier Tätigkeiten, es wird also
		// so oft gedrückt, bis die gewünschte erreicht ist.
		var taetigkeitsKnopf = stadt.GetNodeOrNull<BaseButton>("ButtonTaetigkeit0");

		for (int versuch = 0; versuch < 4; versuch++)
		{
			if ((EnumProduktionsslotAktionsart)handel.GetProduktionsslot(stadtId, 0).GetTaetigkeit()
			    == EnumProduktionsslotAktionsart.Produzieren)
				break;

			taetigkeitsKnopf?.EmitSignal(BaseButton.SignalName.Pressed);
			await NaechsterFrame();
		}

		int rohstoffId = handel.RohstoffIdAnPlatz(stadtId, werkstattNr);
		handel.SetzeProduktionsRohstoff(stadtId, 0, rohstoffId);

		// Genau so viele Arbeiter wie die Ware verlangt: Produktionsslot.GetProduktion rechnet mit
		// benArbeiter = Staetten * (Arbeiter / Werkstaetten) je Rohstoff. Darunter sinkt der Ertrag
		// anteilig, darueber steigt er nicht mehr – nur die Loehne. Ein fester Wert ist deshalb entweder
		// zu knapp oder verbrennt Geld; gemessen war eine Ueberbesetzung klar defizitaer.
		var ware = SW.Dynamisch.GetRohstoffwithID(rohstoffId);
		int proStaette = ware.GetWerkstaetten() > 0 ? ware.GetArbeiter() / ware.GetWerkstaetten() : 1;
		int staetten = ErmittleSinnvolleStaetten(stadtId, rohstoffId, ware, proStaette, out bool lagerIstDieGrenze);
		int arbeiter = Math.Max(1, staetten * proStaette);

		SetzeZahl(stadt, "HBoxDetail0/NumericStaette", staetten);
		SetzeZahl(stadt, "HBoxDetail0/NumericMenge", arbeiter);
		await NaechsterFrame();

		_ruecklage = BerechneRuecklage(ware, staetten, arbeiter);

		// Lager nur erweitern, solange es die bindende Grenze ist. Ist stattdessen die Arbeiterzahl am
		// Anschlag, waere zusaetzlicher Lagerraum totes Kapital – gemessen kostete ein blindes
		// Weiterkaufen ueber 15 Jahre rund 31 000 Taler und drehte das Ergebnis ins Minus.
		if (lagerIstDieGrenze)
			KaufeLagerraumAusUeberschuss(stadtId, werkstattNr);

		// Absetzen der Ware. Beide Wege werden gespielt, im Wechsel je Jahr, damit sie sich nicht in die
		// Quere kommen: Der Export zieht erst zum Rundenende, waehrend der Verkauf vor Ort sofort raeumt.
		int bestand = handel.GetLagerbestand(stadtId, rohstoffId);

		if (SW.Dynamisch.GetAktuellesJahr() % 2 == 0)
			await StelleExportEin(handel, stadt, stadtId, rohstoffId, bestand);
		else if (bestand > 0)
			VerkaufeVorOrt(stadt, werkstattNr, bestand);

		_bedienteKnoepfe.Add("Stadt.Handelsrunde");
	}

	/// <summary>
	/// Wie viele Produktionsstätten ein Kaufmann hier sinnvollerweise besetzt. Zwei Grenzen binden,
	/// und beide zu kennen ist der Unterschied zwischen Gewinn und Ruin:
	///
	/// <para>Das <b>Lager</b>: Was nicht hineinpasst, wird zwar produziert und bezahlt, geht aber
	/// verloren (<c>BuchManager</c>: „Was nicht eingelagert werden konnte, geht verloren").</para>
	///
	/// <para>Die <b>Arbeiterzahl</b>: <c>HandelsManager.SetzeProduktionsArbeiter</c> kappt bei
	/// <c>GetMaxArbeiterAnzahl</c> (99). Mehr Stätten als dieses Kontingent bedienen kann, senken den
	/// Ertrag je Stätte anteilig, während die Betriebskosten voll weiterlaufen – gemessen brach die
	/// Produktion so auf ein Drittel ein, während die Kosten das Vierfache erreichten.</para>
	///
	/// <paramref name="lagerIstDieGrenze"/> sagt, welche der beiden bindet: Nur wenn es das Lager ist,
	/// lohnt ein Ausbau.
	/// </summary>
	private static int ErmittleSinnvolleStaetten(int stadtId, int rohstoffId, Rohstoff ware, int proStaette,
	                                             out bool lagerIstDieGrenze)
	{
		double effizienz = SW.Dynamisch.GetStadtwithID(stadtId).GetEffizienzVonRohstoffMitIDX(rohstoffId);
		int ertragJeStaette = Math.Max(1, (int)(ware.GetWSProdProWS() * 0.9 * effizienz));

		int lagerplatzStueck = SW.Dynamisch.GetAktHum().ErmittleLagerplatzInStadt(stadtId, rohstoffId)
		                       * ware.GetLagermengeProQMeter();

		int nachLager = Math.Max(1, lagerplatzStueck / ertragJeStaette);
		int nachArbeitern = Math.Max(1, SW.Statisch.GetMaxArbeiterAnzahl() / proStaette);

		lagerIstDieGrenze = nachLager < nachArbeitern;

		return Math.Min(nachLager, nachArbeitern);
	}

	/// <summary>
	/// Das Polster, das eine Handelsrunde stehen lässt: die Produktionskosten, die sie mit dieser
	/// Einstellung im laufenden Jahr selbst auslöst, verdoppelt. Der Aufschlag deckt die übrigen
	/// Kostenblöcke der Jahresabrechnung ab, die der Treiber nicht vorausberechnen kann, ohne sie zu
	/// verbuchen – Verkaufssteuern, Kirchenzehnt, Zoll, Kreditzinsen, Hofhaltung.
	///
	/// Die Formel spiegelt <c>AbrechnungsManager</c>: Arbeiterkosten sind Arbeiter mal
	/// <c>GetWSArbeiterpreis</c>, Betriebskosten Stätten mal <c>GetWSEinzelpreis</c>.
	///
	/// Vorher stand hier eine feste Zahl, unabhängig von der Betriebsgröße. Gemessen stand der Treiber
	/// damit jedes Jahr am Rand der Zahlungsunfähigkeit: In den ersten sieben Jahren war er bei beiden
	/// untersuchten Seeds durchgehend negativ, und in drei von zehn Läufen fiel er in den Schuldturm –
	/// einen absorbierenden Zustand, denn ein Kerkerjahr kostet den Zug, damit die Handelsrunde, damit
	/// die Einnahme, die ihn herausholen würde.
	/// </summary>
	private static int BerechneRuecklage(Rohstoff ware, int staetten, int arbeiter)
	{
		int produktionskosten = arbeiter * ware.GetWSArbeiterpreis() + staetten * ware.GetWSEinzelpreis();

		return Math.Max(RuecklageMindestens, produktionskosten * 2);
	}

	/// <summary>
	/// Kauft Lagerraum, aber nur aus dem Überschuss: Es bleibt <see cref="_ruecklage"/> stehen, und es
	/// wird das größte Angebot genommen, das dieses Polster nicht antastet. Gekauft wird über den
	/// <c>LagerraumManager</c> der Lib, nicht über den Bildschirm – der Lagerraum-Dialog gehört nicht zur
	/// Handelsrunde, und ihn hier aufzuziehen würde die Zugsteuerung durcheinanderbringen.
	/// </summary>
	private void KaufeLagerraumAusUeberschuss(int stadtId, int werkstattNr)
	{
		var lager = new LagerraumManager(stadtId, werkstattNr);
		int taler = SW.Dynamisch.GetAktHum().GetTaler();

		for (int angebot = LagerraumManager.AnzahlAngebote - 1; angebot >= 0; angebot--)
		{
			int preis = lager.GetPreis(angebot);

			if (preis > 0 && taler - preis >= _ruecklage && lager.Kaufe(angebot))
			{
				_bedienteKnoepfe.Add("Stadt.Lagerausbau");
				return;
			}
		}
	}

	/// <summary>
	/// Verkauft den Lagerbestand in der Stadt selbst – ein Klick auf das Rohstoffsymbol genuegt.
	/// Bequem, aber schlecht bezahlt: Wo die Ware herkommt, ist sie wenig wert.
	/// </summary>
	private void VerkaufeVorOrt(Node stadt, int werkstattNr, int bestand)
	{
		stadt.GetNodeOrNull<BaseButton>("ButtonRoh" + werkstattNr)?.EmitSignal(BaseButton.SignalName.Pressed);
		_verkaufteWaren += bestand;
	}

	/// <summary>
	/// Stellt den Export in eine andere Stadt ein – der Weg, mit dem im Spiel tatsaechlich verdient wird.
	/// Im Verkaufsmodus deutet der Bildschirm dieselben Zahlenknoepfe um: Aus der Staettenzahl wird die
	/// Zielstadt, aus der Menge die Verkaufsmenge.
	///
	/// Die Karawane kostet einen Grundpreis plus einen Betrag je angefangene 100 Stueck
	/// (<c>BerechneProdKosten</c>). Kleine Restbestaende lohnen den Weg daher nicht; unterhalb einer
	/// vollen Fuhre bleibt die Ware liegen und wird im naechsten Jahr vor Ort verkauft.
	/// </summary>
	private async Task StelleExportEin(HandelsManager handel, Node stadt, int stadtId, int rohstoffId, int bestand)
	{
		if (bestand < 100)
			return;

		int zielStadt = FindeBesteAbsatzstadt(stadtId, rohstoffId);

		if (zielStadt == 0)
			return;

		// Rechnet sich die Fuhre überhaupt? Die Karawane kostet einen Sockel plus einen Betrag je
		// angefangene 100 Stück – bei der billigsten 100 Taler Sockel und effektiv 1 Taler je Stück.
		// Dem steht nur der Preisunterschied zur eigenen Stadt gegenüber. Ohne diese Prüfung exportierte
		// der Durchlauf auch dann, wenn die Gebühr den Mehrerlös übersteigt, und die Spieler blieben
		// gemessen ärmer als ganz ohne Export.
		var karawane = handel.GetKarawane(stadtId);
		int fuhren = (bestand + 99) / 100;
		int kosten = karawane.Fixpreis + karawane.PreisProStueck * fuhren;
		int mehrerloes = (SW.Dynamisch.GetStadtwithID(zielStadt).GetRohstoffPreisVonIDX(rohstoffId)
		                  - SW.Dynamisch.GetStadtwithID(stadtId).GetRohstoffPreisVonIDX(rohstoffId)) * bestand;

		if (mehrerloes <= kosten)
		{
			if (_ausfuehrlich)
				GD.Print("    Export unterbleibt: " + mehrerloes + " Mehrerlös gegen " + kosten + " Karawane");

			_exportAbgelehnt++;
			return;
		}

		var taetigkeitsKnopf = stadt.GetNodeOrNull<BaseButton>("ButtonTaetigkeit1");

		for (int versuch = 0; versuch < 4; versuch++)
		{
			if ((EnumProduktionsslotAktionsart)handel.GetProduktionsslot(stadtId, 1).GetTaetigkeit()
			    == EnumProduktionsslotAktionsart.Verkaufen)
				break;

			taetigkeitsKnopf?.EmitSignal(BaseButton.SignalName.Pressed);
			await NaechsterFrame();
		}


		handel.SetzeVerkaufsRohstoff(stadtId, 1, rohstoffId);
		handel.SetzeVerkaufsStadt(stadtId, 1, zielStadt);

		SetzeZahl(stadt, "HBoxDetail1/NumericStaette", zielStadt);
		SetzeZahl(stadt, "HBoxDetail1/NumericMenge", bestand);
		await NaechsterFrame();

		// Eigener Ansichtsname, damit die eingerichtete Verkaufszeile ein eigenes Bildbudget
		// bekommt: Der Schuss beim Betreten der Stadt (Schiesse(nameof(Stadt))) faellt vor diese
		// Einrichtung und zeigt die Zeile deshalb immer leer.
		await Schiesse("Stadt_Verkauf");

		_exportierteWaren += bestand;
	}

	/// <summary>
	/// Setzt einen Zahlenknopf und meldet die Änderung. Das Setzen von <c>Wert</c> allein löst das Signal
	/// nicht aus – nur die Ziffern-Eingabe tut das –, und ohne Signal erfährt die Stadt nichts davon.
	/// </summary>
	private void SetzeZahl(Node bildschirm, string pfad, int wert)
	{
		var knopf = bildschirm.GetNodeOrNull<controls.NumericButtonWithSounds>(pfad);

		if (knopf == null)
		{
			_fehler.Add("Der Zahlenknopf " + pfad + " wurde in der Stadt nicht gefunden.");
			return;
		}

		knopf.Wert = wert;
		knopf.EmitSignal(controls.NumericButtonWithSounds.SignalName.WertChanged, knopf.Wert);
	}

	/// <summary>
	/// Klickt auf der Militärkarte einen Stützpunkt an. Ohne diesen Umweg bleibt das gesamte
	/// Räuber-/Söldner-System ungeprüft: Kauf und Verwaltung eines Stützpunkts hängen an einem Klick auf
	/// die Karte, und die kennt – wie die Weltkarte – keine Knöpfe, sondern nur Mauspositionen.
	/// </summary>
	private async Task BesucheStuetzpunkt()
	{
		var karte = _main.SoeldnerRaeuberKarte;
		int anzahl = karte.AnzahlStuetzpunkte;

		if (anzahl <= 0)
			return;

		// Reihum ein anderer Stützpunkt: eigene und fremde führen zu verschiedenen Bildschirmen
		// (Verwaltung beim eigenen, Kaufangebot beim fremden).
		int id = 1 + _auswahl.Next(anzahl);
		var mitte = karte.GetStuetzpunktMitte(id);

		if (mitte == Vector2.Zero)
			return;

		KlickeAufPosition(mitte);
		await NaechsterFrame();

		// Headless kommen synthetische Mausereignisse bei den Karten nicht an (dieselbe Beobachtung wie
		// bei der Weltkarte). Bleibt die Karte danach stehen, wird der Stützpunkt direkt gewählt – über
		// denselben Weg, den auch der echte Klick nimmt.
		if (karte.IsProcessingInput())
			karte.WaehleStuetzpunkt(id);

		if (_ausfuehrlich)
			GD.Print("    Stuetzpunkt " + id + " angeklickt");

		// Was aufgeht, haengt vom Besitzer ab – deshalb wird hier nichts Bestimmtes erwartet, sondern
		// bedient, was erscheint, und anschliessend zur Karte zurueckgekehrt.
		_bedienteKnoepfe.Add("SoeldnerRaeuberKarte.Stuetzpunkt");
		_klicksSeitAktion = 0;
		_inAktion = true;
		await WarteBisBildschirmZurueck(nameof(SoeldnerRaeuberKarte), "Stuetzpunkt " + id);
		_inAktion = false;
	}

	/// <summary>Schickt Mausbewegung und Klick an eine Bildschirmposition (fuer die Karten).</summary>
	private static void KlickeAufPosition(Vector2 punkt)
	{
		Input.ParseInputEvent(new InputEventMouseMotion { Position = punkt, GlobalPosition = punkt });
		Input.ParseInputEvent(new InputEventMouseButton
		{
			Position = punkt, GlobalPosition = punkt, ButtonIndex = MouseButton.Left, Pressed = true
		});
		Input.ParseInputEvent(new InputEventMouseButton
		{
			Position = punkt, GlobalPosition = punkt, ButtonIndex = MouseButton.Left, Pressed = false
		});
	}

	/// <summary>
	/// Sucht die Stadt, in der die Ware am meisten einbringt. Genau das zeigen die Stadtinformationen als
	/// „Nachfrage" an: Sie ergibt sich aus dem Aufschlag des dortigen Preises auf den Standardpreis
	/// (<c>Stadt.GetBedarf</c> sortiert danach). Vorher wurde die Zielstadt beliebig gewählt, und der
	/// Export lohnte sich deshalb messbar nicht – landete die Ware in einer Stadt, die sie selbst
	/// herstellt, war der Erlös so mager wie zu Hause.
	/// </summary>
	private static int FindeBesteAbsatzstadt(int eigeneStadtId, int rohstoffId)
	{
		int beste = 0;
		int besterPreis = SW.Dynamisch.GetStadtwithID(eigeneStadtId).GetRohstoffPreisVonIDX(rohstoffId);

		for (int id = SW.Statisch.GetMinStadtID(); id < SW.Statisch.GetMaxStadtID(); id++)
		{
			if (id == eigeneStadtId)
				continue;

			int preis = SW.Dynamisch.GetStadtwithID(id).GetRohstoffPreisVonIDX(rohstoffId);

			if (preis > besterPreis)
			{
				besterPreis = preis;
				beste = id;
			}
		}

		// Bringt keine andere Stadt mehr als die eigene, lohnt der Weg nicht – dann bleibt die Ware da.
		return beste;
	}

	/// <summary>
	/// Wartet, bis der genannte Bildschirm sichtbar und bedienbar ist. Die Menüs arbeiten allein über
	/// Knopfsignale und schalten <c>_Input</c> nie ein – für sie genügt Sichtbarkeit
	/// (<paramref name="brauchtEingabe"/> = false), sonst wartete der Treiber vergeblich.
	/// </summary>
	private async Task<bool> WarteAufSichtbar(string bildschirm, int maxSchritte = MaxSchritte,
	                                          bool brauchtEingabe = true)
	{
		var knoten = _main.GetNodeOrNull<Control>(bildschirm);

		for (int schritt = 0; knoten != null && schritt < maxSchritte; schritt++)
		{
			if (knoten.IsVisibleInTree() && (!brauchtEingabe || knoten.IsProcessingInput()))
				return true;

			await NaechsterFrame();
		}

		return false;
	}

	/// <summary>
	/// Wartet nach einer Handlung, bis der Bereichsbildschirm wieder bedienbar ist – dazwischen werden
	/// alle Dialoge bedient, die die Handlung ausgelöst hat (Bestätigungen, Meldungen, Auswahlkarten).
	/// </summary>
	private async Task<bool> WarteBisBildschirmZurueck(string bildschirm, string bezeichnung)
	{
		var knoten = _main.GetNodeOrNull<Control>(bildschirm);
		int ruhig = 0;

		for (int schritt = 0; schritt < MaxSchritte; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				await VersucheZuBedienen(dialog);
				ruhig = 0;
			}
			else if (knoten != null && knoten.IsVisibleInTree() && knoten.IsProcessingInput())
			{
				if (++ruhig >= 3)
					return true;
			}
			else if (_main.Kontor.IsProcessingInput())
			{
				// Manche Handlungen enden im Kontor statt im Bereich (das Testament etwa schließt die
				// Kirche). Das ist kein Fehler – der Rundgang macht dort einfach weiter.
				if (_ausfuehrlich)
					GD.Print("      (" + bezeichnung + " endete im Kontor)");

				return false;
			}
			else
			{
				ruhig = 0;

				// Die Handlung führte auf einen anderen Bildschirm (etwa eine Auswahlkarte) – zurück.
				if (schritt % 8 == 0)
					SchickeAbbruch();
			}

			await NaechsterFrame();
		}

		_fehler.Add("Nach der Aktion " + bezeichnung + " kehrte der Ablauf nicht zu " + bildschirm
		            + " zurück. " + Zustandsbericht());
		return false;
	}

	private static void SammleKnoepfe(Node knoten, List<BaseButton> ziel)
	{
		foreach (Node kind in knoten.GetChildren())
		{
			if (kind is BaseButton knopf && knopf.IsVisibleInTree() && !knopf.Disabled)
				ziel.Add(knopf);

			SammleKnoepfe(kind, ziel);
		}
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
		_imZugende = true;

		if (_ausfuehrlich)
			GD.Print("  Zug beenden (Spieler " + spielerVorher + ", Knopf "
			         + (zugEnde.Disabled ? "gesperrt" : "frei") + ")");

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
				_imZugende = false;
				return true;
			}
			else if (ErkenneSpielende())
			{
				// Der Zug endete das Spiel (erloschene Dynastie, erfüllter Auftrag): Jahr und Spieler
				// wechseln dann nicht mehr, weil es keinen nächsten Zug gibt.
				_imZugende = false;
				return false;
			}

			await NaechsterFrame();
		}

		_imZugende = false;

		if (_spielBeendet)
			return false;

		_fehler.Add("Der Zug im Jahr " + jahrVorher + " ließ sich nicht beenden. " + Zustandsbericht());
		return false;
	}

	/// <summary>
	/// Stellt je Zug einen der Spielzustände her, die ein Durchlauf von sich aus nie erreicht – in
	/// fester Reihenfolge und immer nur den nächsten, dessen Ansicht noch aussteht. Nur mit
	/// <c>--zustaende</c>; ohne den Schalter wird nichts davon aufgerufen.
	///
	/// Warum diese vier Ansichten sonst unerreichbar bleiben, obwohl sie im Client fertig sind: Sie
	/// hängen nicht an Spielzeit, sondern an einem Zustand, den der Treiber nie herbeiführt – eine
	/// Anklage gegen ihn, eine Wahl, an der er beteiligt ist, ein eigener Stützpunkt, ein erfüllter
	/// Auftrag. Gemessen an einem 15-Jahre-Lauf: <c>BewerbDialog</c> viermal, <c>StuetzpunktKaufenDialog</c>
	/// 116-mal – und keine dieser vier Ansichten ein einziges Mal.
	///
	/// Erst weitergeschaltet wird, wenn die Ansicht des laufenden Schritts wirklich zu sehen war
	/// (<see cref="NotiereZustandsansichten"/>). Ein Schritt, der nicht gegriffen hat, wird also im
	/// nächsten Zug wiederholt, statt still übersprungen zu werden.
	///
	/// Aufgerufen wird zweimal je Zug, und das ist kein Schmuck: Die ersten drei Schritte müssen
	/// <b>vor</b> den Rundgang, weil der Zug danach nichts mehr Passendes tut – der Prozess fällt am
	/// Zugende, die Wahl am Jahresende, und das Kaufangebot ist auf eines je Jahr begrenzt. Der
	/// Auftragssieg muss <b>danach</b>, weil sein Ziel ein Talerstand ist, den der Rundgang im selben
	/// Zug wieder ausgibt (Handelsrunde, Lagerausbau, Zufallskäufe).
	///
	/// Liefert false, wenn der Treiber dabei hängen geblieben ist und der Zug abgebrochen gehört –
	/// dieselbe Zusage wie bei <see cref="BesucheBereiche"/>.
	/// </summary>
	private async Task<bool> StelleZustaendeHer(bool nachRundgang)
	{
		if (SW.Dynamisch.GetAktuellesJahr() - SW.Statisch.StartJahr < ZustaendeAbJahr)
			return true;

		// Alle vier Zustände gehören demselben Kaufmann. Im Hot-Seat sonst zweimal je Jahr, und der
		// zweite Anlauf schüge fehl: Das Wähleramt gehört dann bereits einem menschlichen Mitspieler,
		// und <c>UebernehmeAmt</c> lehnt genau das ab.
		if (SW.Dynamisch.GetAktiverSpieler() != 1)
			return true;

		while (_zustandSchritt < Zustandsansichten.Length
		       && _erreichteAnsichten.Contains(Zustandsansichten[_zustandSchritt]))
			_zustandSchritt++;

		if (nachRundgang)
		{
			if (_zustandSchritt == 3)
				BereiteAuftragssiegVor();

			return true;
		}

		switch (_zustandSchritt)
		{
			case 0:
				BereiteGerichtVor();
				break;
			case 1:
				BereiteWahlVor();
				break;
			case 2:
				return await BereiteStuetzpunktVor();
		}

		return true;
	}

	/// <summary>
	/// Meldet einen Fehler aus der Zustandsherstellung genau einmal. Ein Schritt wird jedes Jahr
	/// wiederholt, solange seine Ansicht aussteht – ist der Zustand dauerhaft blockiert, schriebe er
	/// sonst je Jahr dieselbe Zeile in die Fehlerliste, und der eigentliche Befund (die unerreichte
	/// Ansicht aus <see cref="BerichteZustaende"/>) ginge zwischen zwölf Wiederholungen unter.
	/// </summary>
	private void MeldeZustandsfehler(string schluessel, string meldung)
	{
		if (_zustandsfehler.Add(schluessel))
			_fehler.Add(meldung);
	}

	/// <summary>
	/// Schritt 1: sich verklagen lassen. Die Verhandlung fällt noch am Ende dieses Zuges an –
	/// <c>Kontor.ZeigeZugnachrichten</c> ruft <c>GerichtDialog.ShowGericht</c> auf, und der prüft nur,
	/// ob eine Verhandlung mit dem aktiven Spieler vorliegt, nicht auf welches Jahr sie datiert.
	/// Bedient wird sie von der gewöhnlichen Dialogbedienung.
	/// </summary>
	private void BereiteGerichtVor()
	{
		string meldung = new CheatManager().LasseVerklagen();

		if (_ausfuehrlich)
			GD.Print("  Zustand: Anklage – " + meldung.Replace("\n", " "));
	}

	/// <summary>
	/// Schritt 2: die Wahl. Der Bildschirm erscheint nur für Wahlen mit menschlicher Beteiligung
	/// (<c>Kontor.HalteWahlenAb</c>), und Kandidat wird der Treiber praktisch nie. Bleibt der zweite
	/// Weg: Wähler sein. Wer wen wählt, steht in der Amtstafel (<c>Amt.GetWaehler1AmtID</c> und
	/// Geschwister); der Treiber übernimmt also ein Amt, das andere Ämter wählt.
	///
	/// Zwei Dinge, die dabei leicht schiefgehen:
	/// <list type="bullet">
	/// <item>Amt und Gebiet gehören zusammen. <c>CheatManager.UebernehmeAmt</c> tut das richtig – es
	/// reicht über <c>AmtAufStufeXGebietYidZanWvergeben</c> bis zu <c>SetAmt(amtId, gebietId)</c> und
	/// gibt auch der verdrängten KI ihr altes Amt samt Gebiet (nachgesehen, nicht angenommen).</item>
	/// <item>Ein Amt wählt nur in seinem eigenen Gebiet mit (<c>AemterManager.ErmittleWaehlerSpielerIds</c>):
	/// ein Stadtamt nur in seiner Stadt, ein Landesamt in seinem Land, ein Reichsamt überall. Deshalb
	/// fällt die Wahl bei Gleichstand auf das höchstgelegene Amt.</item>
	/// </list>
	///
	/// Frei wird ein Amt durch den Tod seines Inhabers, und KI-Spieler sterben seit der
	/// wiederhergestellten KI-Jahreswende jedes Jahr. Nur trifft es selten gerade eines der wenigen
	/// Ämter, die der Treiber wählt – grob gerechnet ein Sechstel der Jahre. Für eine Zusicherung ist
	/// das zu wenig, deshalb schafft der Treiber die Vakanz notfalls selbst, und zwar über denselben
	/// Lib-Aufruf, den auch der Tod nimmt (<see cref="ErzwingeVakanz"/>).
	/// </summary>
	private void BereiteWahlVor()
	{
		int amt = WaehleWaehleramt();

		if (amt == 0)
		{
			MeldeZustandsfehler("KeinWaehleramt", "In der Amtstafel stand kein Amt, das andere Ämter wählt.");
			return;
		}

		int stufe = SW.Dynamisch.GetStufeVonAmtmitIDx(amt);

		// Gebiet 1 der Stufe: beim Reich das einzige, sonst schlicht das erste – ein Amt hängt nicht am
		// Wohnsitz. Der Combobox-Index des Cheats ist 0-basiert, die Gebiets-ID 1-basiert.
		const int gebiet = 1;
		string meldung = new CheatManager().UebernehmeAmt(stufe, gebiet - 1, AmtsIndexInStufe(stufe, amt));

		if (_ausfuehrlich)
			GD.Print("  Zustand: Wähleramt – " + meldung);

		if (SW.Dynamisch.GetAktHum().GetAmtID() != amt)
		{
			MeldeZustandsfehler("Waehleramt", "Das Wähleramt " + SW.Statisch.GetAmtwithID(amt).GetAmtsname(true)
								     + " ließ sich nicht übernehmen: " + meldung);
			return;
		}

		// Steht ohnehin schon eine Wahl an, an der ein Mensch beteiligt ist, bleibt es beim natürlichen
		// Verlauf – gecheatet wird nur, was sonst ausbliebe.
		if (new AemterManager().GetWahlenMitMenschlicherBeteiligung().Count > 0)
			return;

		if (!ErzwingeVakanz(amt, stufe, gebiet) && _ausfuehrlich)
			GD.Print("  Zustand: keine KI-besetzte Stelle gefunden, die dieses Amt wählt");
	}

	/// <summary>
	/// Das Amt, mit dem der Spieler am ehesten zu einer Wahl kommt. Ausschlaggebend sind drei Dinge,
	/// in dieser Reihenfolge – und das zweite ist teuer erkauft:
	///
	/// <list type="number">
	/// <item>Wie oft das Amt in der Amtstafel als Wähler eingetragen ist. Je mehr Ämter es wählt, desto
	/// eher fällt eines davon vakant.</item>
	/// <item><b>Ob es selbst abgesetzt werden kann.</b> Über eine Amtsenthebung stimmen die Wähler des
	/// betroffenen Amtes ab (<c>AmtsenthebungsManager</c>); hat ein Amt gar keine Wähler, verfällt der
	/// Antrag ungeprüft. Genau drei Ämter sind so gestellt – Regent, Erzbischof, Feldmarschall – und
	/// eines davon muss es sein. Gemessen mit dem Justizminister, dessen einziger Wähler der Regent
	/// ist: Sobald diese eine KI den Spieler nicht mehr mag, genügt ihre Stimme (1 von 1), und
	/// <c>Kontor.FuehreAmtsenthebungenDurch</c> läuft <b>unmittelbar vor</b>
	/// <c>HalteWahlenAb</c>. Der Spieler verlor sein Amt also jedes Jahr einen Schritt vor der
	/// Auszählung und war nie Wähler; in einem 20-Jahre-Lauf zu zweit kam so in neun Anläufen keine
	/// einzige Wahl zustande.</item>
	/// <item>Die Amtsstufe: Ein Reichsamt wählt in jedem Land und jeder Stadt mit, ein Stadtamt nur in
	/// seiner eigenen (<c>AemterManager.ErmittleWaehlerSpielerIds</c>).</item>
	/// </list>
	/// </summary>
	private static int WaehleWaehleramt()
	{
		int[] nennungen = new int[SW.Statisch.GetMaxAmtID()];

		for (int amt = 1; amt < SW.Statisch.GetMaxAmtID(); amt++)
		{
			var eintrag = SW.Statisch.GetAmtwithID(amt);

			foreach (int waehler in new[] { eintrag.GetWaehler1AmtID(), eintrag.GetWaehler2AmtID(), eintrag.GetWaehler3AmtID() })
				if (waehler > 0 && waehler < nennungen.Length)
					nennungen[waehler]++;
		}

		int bestes = 0;

		for (int amt = 1; amt < nennungen.Length; amt++)
		{
			if (nennungen[amt] > 0 && (bestes == 0 || IstBesseresWaehleramt(amt, bestes, nennungen)))
				bestes = amt;
		}

		return bestes;
	}

	/// <summary>Der Vergleich zu <see cref="WaehleWaehleramt"/>: Nennungen, dann Absetzbarkeit, dann Stufe.</summary>
	private static bool IstBesseresWaehleramt(int amt, int bisher, int[] nennungen)
	{
		if (nennungen[amt] != nennungen[bisher])
			return nennungen[amt] > nennungen[bisher];

		if (IstAbsetzbar(amt) != IstAbsetzbar(bisher))
			return !IstAbsetzbar(amt);

		return SW.Dynamisch.GetStufeVonAmtmitIDx(amt) > SW.Dynamisch.GetStufeVonAmtmitIDx(bisher);
	}

	/// <summary>
	/// Ob über die Absetzung dieses Amtes überhaupt jemand abstimmen kann. Ohne einen einzigen Wähler
	/// verwirft <c>AmtsenthebungsManager.ErmittleVerfahren</c> den Antrag – das Amt ist unantastbar.
	/// </summary>
	private static bool IstAbsetzbar(int amtId)
	{
		var eintrag = SW.Statisch.GetAmtwithID(amtId);

		return eintrag.GetWaehler1AmtID() != 0 || eintrag.GetWaehler2AmtID() != 0 || eintrag.GetWaehler3AmtID() != 0;
	}

	/// <summary>
	/// Rechnet eine Amts-ID in den stufenrelativen Index um, den <c>CheatManager.UebernehmeAmt</c>
	/// erwartet (dessen <c>AmtIdAusIndex</c> rechnet genau so zurück).
	/// </summary>
	private static int AmtsIndexInStufe(int stufe, int amtId)
	{
		if (stufe == 1)
			return amtId - SW.Statisch.GetMaxAmtStadtID();

		if (stufe == 2)
			return amtId - SW.Statisch.GetMaxAmtLandID();

		return amtId;
	}

	/// <summary>
	/// Macht eine Stelle frei, die der Spieler mit seinem Amt wählt, und legt damit eine Wahl an.
	/// Genommen wird <c>AmtVonXfreigeben</c> – derselbe Aufruf, mit dem der Tod einer KI ein Amt räumt
	/// und die Wahl des nächsten Jahres vorbereitet; es entsteht also nichts, was das Spiel nicht auch
	/// von selbst hervorbrächte, es entsteht nur zuverlässig.
	/// </summary>
	private bool ErzwingeVakanz(int waehlerAmt, int waehlerStufe, int waehlerGebiet)
	{
		for (int amt = 1; amt < SW.Statisch.GetMaxAmtID(); amt++)
		{
			var eintrag = SW.Statisch.GetAmtwithID(amt);

			if (eintrag.GetWaehler1AmtID() != waehlerAmt && eintrag.GetWaehler2AmtID() != waehlerAmt
			    && eintrag.GetWaehler3AmtID() != waehlerAmt)
				continue;

			int stufe = SW.Dynamisch.GetStufeVonAmtmitIDx(amt);

			for (int gebiet = 1; gebiet < AnzahlGebiete(stufe); gebiet++)
			{
				if (!WaehltImGebiet(waehlerStufe, waehlerGebiet, stufe, gebiet))
					continue;

				int inhaber = SW.Dynamisch.GetGebietwithID(gebiet, stufe).GetAmtX(amt);

				if (inhaber < SW.Statisch.GetMinKIID())
					continue;

				SW.Dynamisch.AmtVonXfreigeben(inhaber);

				if (_ausfuehrlich)
					GD.Print("  Zustand: Stelle " + SW.Statisch.GetAmtwithID(amt).GetAmtsname(true)
					         + " in Gebiet " + gebiet + " freigemacht – daraus wird eine Wahl");

				return true;
			}
		}

		return false;
	}

	/// <summary>Anzahl der Gebiete einer Amtsstufe (Stadt/Land/Reich), jeweils als Obergrenze der IDs.</summary>
	private static int AnzahlGebiete(int stufe)
	{
		if (stufe == 1)
			return SW.Statisch.GetMaxLandID();

		if (stufe == 2)
			return SW.Statisch.GetMaxReichID();

		return SW.Statisch.GetMaxStadtID();
	}

	/// <summary>
	/// Ob ein Amtsträger der einen Stufe in einem Gebiet der anderen mitwählt – die Regel aus
	/// <c>AemterManager.ErmittleWaehlerSpielerIds</c>: Ein Reichsamt wählt überall, ein Landesamt in
	/// seinem Land (und in dessen Städten), ein Stadtamt nur in seiner Stadt.
	/// </summary>
	private static bool WaehltImGebiet(int waehlerStufe, int waehlerGebiet, int amtStufe, int amtGebiet)
	{
		if (waehlerStufe == 2)
			return true;

		if (waehlerStufe == 1)
			return amtStufe == 0
				? SW.Dynamisch.GetLandIDzuStadtX(amtGebiet) == waehlerGebiet
				: amtStufe == 1 && amtGebiet == waehlerGebiet;

		return amtStufe == 0 && amtGebiet == waehlerGebiet;
	}

	/// <summary>
	/// Schritt 3: einen Stützpunkt erwerben und verwalten. Der Treiber öffnet den Kaufdialog ständig
	/// (116-mal in 15 Jahren gemessen), kam aber nie zu einem eigenen Stützpunkt – und ohne Besitz
	/// führt der Klick auf die Militärkarte immer nur wieder ins Kaufangebot.
	///
	/// Gekauft wird über den Manager statt über den Dialog: Der Preis ist dort ein Zahlenknopf, und
	/// die zufällige Dialogbedienung träfe nie einen Betrag, der überzeugt. Geöffnet wird die
	/// Verwaltung anschließend über <c>SoeldnerRaeuberKarte.WaehleStuetzpunkt</c> – denselben Einstieg,
	/// den der echte Klick nimmt und den der Treiber schon für die Karte braucht, weil synthetische
	/// Mausereignisse sie headless nicht erreichen.
	///
	/// Die Taler für das Angebot sind Gerüst, kein Verdienst: Sie werden vorher gesetzt und hinterher
	/// wieder auf den alten Stand zurückgenommen, damit die restlichen Jahre nicht mit einem Vermögen
	/// laufen, das das Spiel nie erwirtschaftet hat. Der Stützpunkt kostet den Spieler dadurch nichts –
	/// geprüft werden soll seine Verwaltung, nicht sein Preis.
	/// </summary>
	private async Task<bool> BereiteStuetzpunktVor()
	{
		var manager = new SoeldnerRaeuberManager();
		int eigener = FindeStuetzpunkt(manager, eigen: true);

		if (eigener == 0)
			eigener = await KaufeStuetzpunkt(manager);

		if (eigener == 0)
			return true;   // Angebot abgelehnt – im nächsten Zug erneut (pro Jahr ist nur eines erlaubt)

		return await OeffneStuetzpunktVerwaltung(eigener);
	}

	/// <summary>Der erste Stützpunkt in eigener bzw. (<paramref name="eigen"/> = false) in KI-Hand.</summary>
	private static int FindeStuetzpunkt(SoeldnerRaeuberManager manager, bool eigen)
	{
		for (int id = 1; id <= manager.Anzahl; id++)
		{
			if (eigen && manager.GehoertAktivemSpieler(id))
				return id;

			// Ein Stützpunkt in der Hand eines Mitspielers taugt nicht: Dessen Angebot wird ihm erst zu
			// seinem eigenen Zugbeginn vorgelegt, der Kauf wäre also nicht in diesem Zug entschieden.
			if (!eigen && manager.GetBesitzer(id) >= SW.Statisch.GetMinKIID())
				return id;
		}

		return 0;
	}

	/// <summary>
	/// Unterbreitet dem KI-Besitzer ein Angebot und bedient dabei die Rückfrage der Lib. Liefert die
	/// ID des erworbenen Stützpunkts oder 0.
	/// </summary>
	private async Task<int> KaufeStuetzpunkt(SoeldnerRaeuberManager manager)
	{
		int ziel = FindeStuetzpunkt(manager, eigen: false);

		if (ziel == 0)
		{
			MeldeZustandsfehler("KeinStuetzpunkt", "Kein Stützpunkt in KI-Besitz – es gab keinen zu kaufen.");
			return 0;
		}

		var spieler = SW.Dynamisch.GetAktHum();
		int talerVorher = spieler.GetTaler();
		int preis = manager.GetKaufInfo(ziel).Wert + AngebotsAufschlag;

		// Das Zurücknehmen gehört in ein finally: Dies ist die einzige Stelle im Treiber, die dem Spieler
		// eine siebenstellige Summe in die Hand drückt. Fördert die Dialogbedienung dazwischen eine
		// Ausnahme zutage, liefe der Rest des Spiels sonst mit einem Vermögen weiter, das niemand
		// erwirtschaftet hat – und der Bericht am Ende wiese es aus, als wäre es erspielt.
		try
		{
			spieler.SetTaler(preis + RuecklageMindestens);

			var angebot = manager.KaufangebotAbgeben(ziel, preis);

			// Die Lib fragt über SW.UI zurück („Wollt Ihr wirklich …“) und meldet danach das Ergebnis;
			// beides läuft über Dialoge, die jemand bedienen muss, während hier gewartet wird.
			for (int schritt = 0; !angebot.IsCompleted && schritt < MaxSchritte; schritt++)
			{
				var dialog = FindeOffenenDialog();

				if (dialog != null)
					await VersucheZuBedienen(dialog);

				await NaechsterFrame();
			}

			if (!angebot.IsCompleted)
			{
				MeldeZustandsfehler("Kaufangebot", "Das Kaufangebot für Stützpunkt " + ziel
									  + " kam nie zu einem Ergebnis. " + Zustandsbericht());
				return 0;
			}

			bool gekauft = await angebot;

			if (_ausfuehrlich)
				GD.Print("  Zustand: Angebot über " + preis + " Taler für Stützpunkt " + ziel + " – "
				         + (gekauft ? "angenommen" : "abgelehnt, nächstes Jahr erneut"));

			return gekauft ? ziel : 0;
		}
		finally
		{
			spieler.SetTaler(talerVorher);
		}
	}

	/// <summary>
	/// Öffnet die Militärkarte und darauf den eigenen Stützpunkt, kehrt danach zum Kontor zurück.
	/// Liefert, ob der Kontor wieder erreicht wurde – ist er es nicht, steht der Treiber in einer
	/// fremden Ansicht und der Rundgang liefe ins Leere; der Zug wird dann abgebrochen, genau wie es
	/// <see cref="BesucheBereiche"/> beim selben Befund tut.
	/// </summary>
	private async Task<bool> OeffneStuetzpunktVerwaltung(int stuetzpunktId)
	{
		var knopf = _main.Kontor.GetNodeOrNull<BaseButton>("AreaKampf");

		if (knopf == null || knopf.Disabled)
		{
			// Der Kontor steht noch – vermerkt, aber der Zug kann weiterlaufen.
			MeldeZustandsfehler("AreaKampf",
							"Der Bereich AreaKampf war nicht bedienbar, die Stützpunktverwaltung blieb zu.");
			return true;
		}

		knopf.EmitSignal(BaseButton.SignalName.Pressed);

		if (!await WarteAufSichtbar(nameof(SoeldnerRaeuberKarte)))
		{
			MeldeZustandsfehler("Militaerkarte", "Die Militärkarte ging nicht auf. " + Zustandsbericht());
		}
		else
		{
			_main.SoeldnerRaeuberKarte.WaehleStuetzpunkt(stuetzpunktId);

			if (!await WarteAufSichtbar(nameof(StuetzpunktVerwalten)))
				MeldeZustandsfehler("Verwaltung", "Die Verwaltung des eigenen Stützpunkts " + stuetzpunktId
									   + " ging nicht auf. " + Zustandsbericht());
			else if (_ausfuehrlich)
				GD.Print("  Zustand: Stützpunkt " + stuetzpunktId + " wird verwaltet");
		}

		// Auch auf den Fehlerpfaden: Der Rechtsklick-Rückweg ist zugleich die Erholung, wenn die Karte
		// gar nicht erst aufging.
		return await KehreZumKontorZurueck("AreaKampf");
	}

	/// <summary>
	/// Schritt 4 und letzter: den Auftrag erfüllen. Muss zuletzt kommen, denn ein erfüllter Auftrag
	/// beendet das Spiel (<c>Kontor.PruefeAuftragErfuellt</c>); danach lässt sich nichts mehr herstellen.
	/// Gewählt wird „Kleiner Wohlstand“, weil sein Ziel ein reiner Talerstand ist – ein Zustand, der
	/// sich setzen lässt, ohne einen zweiten Mechanismus zu bemühen.
	///
	/// <b>Der Auftrag wird hier auch erst gesetzt.</b> Ein Spiel des Treibers hat nie einen: Die
	/// Spielanlage über die Menüs lässt die Schwierigkeit auf „Keine (freies Spiel)“, der direkte Weg
	/// über <c>NewGameManager</c> kennt das Feld gar nicht. Der Schritt tut damit beides, was ein
	/// Spieler in zwei getrennten Momenten täte – den Auftrag bei der Spielanlage wählen und ihn
	/// später erfüllen. Geprüft ist dadurch die Auswertung, nicht der Weg dorthin: dass
	/// <c>AktualisiereFortschrittUndPruefe</c> anschlägt, der Siegesbildschirm aufgeht, sich in die
	/// Bestenliste einträgt und das Spiel sauber beendet. Ob ein Spieler die 100 000 Taler je
	/// zusammenbekäme, sagt der Lauf nicht – das ist eine Frage der Balance, keine der Verdrahtung.
	///
	/// Dass der Durchlauf dann vor dem geplanten Jahr endet, ist kein Fehler: <c>ErkenneSpielende</c>
	/// erkennt das reguläre Ende, und <c>Spiele</c> verlässt die Jahresschleife, ohne zu wenige Jahre
	/// zu bemängeln.
	/// </summary>
	private void BereiteAuftragssiegVor()
	{
		var info = AuftragManager.GetInfo(EnumAuftrag.KleinerWohlstand);

		if (info == null)
		{
			MeldeZustandsfehler("Auftragsdaten", "Zum Auftrag „Kleiner Wohlstand“ gab es keine Daten.");
			return;
		}

		SW.Dynamisch.Spielstand.Einstellungen.Auftrag = EnumAuftrag.KleinerWohlstand;

		var spieler = SW.Dynamisch.GetAktHum();

		// Mit Polster statt auf den Zielwert genau: Zwischen diesem Zug und der Prüfung am Zugende liegt
		// noch die Jahresabrechnung (Löhne, Betriebskosten, Steuern, Zehnt, Zoll, Zinsen, Hofhaltung).
		// Träfe sie einen Stand von genau 100 000, bliebe der Auftrag unerfüllt und der Bildschirm aus.
		// Genommen wird die Rücklage – die Handelsrunde führt sie ohnehin als Schätzung genau dieser
		// Kostenblöcke (siehe BerechneRuecklage). Reicht sie einmal nicht, wiederholt sich der Schritt
		// im nächsten Zug, weil die Ansicht dann nicht vermerkt ist.
		int ziel = info.Zielwert + _ruecklage;

		if (spieler.GetTaler() < ziel)
			spieler.SetTaler(ziel);

		if (_ausfuehrlich)
			GD.Print("  Zustand: Auftrag „" + info.Name + "“ gesetzt und erfüllt – das Spiel endet zum Zugende");
	}

	/// <summary>
	/// Hält fest, welche der <see cref="Zustandsansichten"/> sichtbar waren. Läuft in jedem Frame mit,
	/// in dem der Treiber wartet – das genügt, weil jede dieser Ansichten auf eine Bedienung wartet und
	/// deshalb über viele Frames steht. Nur so lässt sich am Ende <b>zusichern</b>, dass die Ansicht
	/// wirklich zu sehen war; „der Cheat lief durch“ wäre nur eine Hoffnung.
	/// </summary>
	private void NotiereZustandsansichten()
	{
		if (!_mitZustaenden || _main == null)
			return;

		foreach (string name in Zustandsansichten)
		{
			if (_erreichteAnsichten.Contains(name))
				continue;

			var knoten = _main.GetNodeOrNull<Control>(name);

			if (knoten == null || !knoten.IsVisibleInTree())
				continue;

			_erreichteAnsichten.Add(name);
			GD.Print("  Zustand erreicht: " + name + " (Jahr " + SW.Dynamisch.GetAktuellesJahr() + ")");
		}
	}

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
	/// Ist das Spiel regulär zu Ende gegangen? Für den Treiber sieht das aus wie ein Hänger: Es kommt
	/// kein bedienbares Kontor mehr. Unterscheiden lässt es sich am Bildschirm – der Client blendet im
	/// Spielende-Zweig das Kontor aus (<c>Kontor.BeendeZug</c>), wodurch das Hauptmenü wieder zum
	/// Vorschein kommt. Gründe: erloschene Dynastie, erfüllter Auftrag, ausgeschiedener letzter Spieler.
	///
	/// Die Abfrage gilt erst, wenn ein Spiel steht: Während der Spielanlage über die Menüs ist das
	/// Hauptmenü sichtbar und das Kontor verborgen – genau dasselbe Bild.
	/// </summary>
	private bool ErkenneSpielende()
	{
		if (_spielBeendet)
			return true;

		if (!_spielLaeuft)
			return false;

		var menue = _main.GetNodeOrNull<Control>(nameof(Mainmenu));

		if (menue == null || !menue.IsVisibleInTree() || _main.Kontor.IsVisibleInTree())
			return false;

		_spielBeendet = true;
		_endeJahr = SW.Dynamisch.GetAktuellesJahr();
		return true;
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

				// Nach derselben Ruhephase ohne Kontor: Statt die vollen MaxSchritte abzuwarten und
				// einen Hänger zu melden, wird geprüft, ob das Spiel schlicht vorbei ist.
				if (ruhig >= RuheFrames && ErkenneSpielende())
					return false;
			}

			await NaechsterFrame();
		}

		return false;
	}

	/// <summary>
	/// Talerstand aller menschlichen Spieler und die in diesem Jahr vor Ort abgesetzte Menge – die zwei
	/// Zahlen, an denen eine Abwärtsspirale erkennbar wird. Der Lokalverkauf steht dabei, weil genau er
	/// in den negativen Läufen einbricht, während der Export weiterläuft.
	/// </summary>
	private string Talerstand()
	{
		var teile = new List<string>();

		for (int i = 1; i <= SW.Dynamisch.GetAktivSpielerAnzahl(); i++)
		{
			var sp = SW.Dynamisch.GetHumWithID(i);

			if (sp != null)
				teile.Add("S" + i + " " + sp.GetTaler() + " T");
		}

		teile.Add("Verkauf " + (_verkaufteWaren - _verkauftVorjahr));
		_verkauftVorjahr = _verkaufteWaren;

		return string.Join(", ", teile);
	}

	/// <summary>
	/// Beschreibt beim Steckenbleiben, was gerade auf dem Schirm ist. Ohne das steht im Fehlerfall nur
	/// „Dialog blieb offen?" – die Vermutung, aber nicht die Beobachtung.
	/// </summary>
	private string Zustandsbericht()
	{
		var sichtbar = new List<string>();

		foreach (Node knoten in GetTree().GetNodesInGroup("Dialogs"))
		{
			if (knoten is Control dialog && dialog.IsVisibleInTree())
				sichtbar.Add(dialog.Name + (dialog.IsProcessingInput() ? " (nimmt Eingaben)" : " (ohne Eingabe)"));
		}

		var bildschirme = new List<string>();

		foreach (Node knoten in _main.GetChildren())
		{
			if (knoten is Control c && knoten != _main.Kontor && c.IsVisibleInTree() && !c.IsInGroup("Dialogs"))
				bildschirme.Add(c.Name + (c.IsProcessingInput() ? " (nimmt Eingaben)" : " (ohne Eingabe)"));
		}

		var spieler = new List<string>();

		for (int i = 1; i <= SW.Dynamisch.GetAktivSpielerAnzahl(); i++)
		{
			var sp = SW.Dynamisch.GetHumWithID(i);
			spieler.Add(sp == null
				? i + ": fehlt"
				: i + ": " + sp.GetName() + ", " + sp.GetTaler() + " Taler, Alter " + sp.GetAlter()
				  + (sp.GetSitztImKerker() ? ", im Schuldturm" : ""));
		}

		var zugEnde = _main.Kontor.GetNodeOrNull<BaseButton>("ButtonEndTurn");

		return "Zug-beenden-Knopf: " + (zugEnde == null ? "fehlt" : zugEnde.Disabled ? "gesperrt" : "frei")
		       + " | Spieler: " + string.Join(" / ", spieler)
		       + " | Sichtbare Dialoge: " + (sichtbar.Count == 0 ? "keine" : string.Join(", ", sichtbar))
		       + " | Sichtbare Bildschirme: " + (bildschirme.Count == 0 ? "keine" : string.Join(", ", bildschirme))
		       + " | Kontor: " + (_main.Kontor.IsVisibleInTree() ? "sichtbar" : "verborgen")
		       + (_main.Kontor.IsProcessingInput() ? ", nimmt Eingaben" : ", ohne Eingabe")
		       + " | aktiver Spieler: " + SW.Dynamisch.GetAktiverSpieler();
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
			_klicksAmSelbenDialog = 0;
			_eingabeGeschickt = false;
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
		// Im Standardpfad wird immer der erste sichtbare Knopf gedrückt – schlicht, aber über viele Läufe
		// als stabil belegt. Nur mit --aktionen wird gewürfelt und nach ein paar Klicks per Rechtsklick
		// ausgestiegen: Dialoge mit Reitern (die Gesetzestafel) haben keinen Knopf, der hinausführt.
		BaseButton knopf;

		_klicksSeitAktion++;

		// Der Wechsel „ein paar Knopfdruecke, dann ein Rechtsklick" gilt immer: Aus manchen Dialogen
		// fuehrt ueberhaupt kein Knopf heraus, ihre Knoepfe bleiben aber dauerhaft verfuegbar. Der
		// Tipps-Dialog etwa hat nur „Zurueck" und „Weiter" und schliesst allein per Rechtsklick – wer
		// stets den ersten sichtbaren Knopf drueckt, blaettert dort bis zum Zeitablauf.
		// Ausgehungert wird dadurch nichts: Nach dem Rechtsklick setzt Bediene den Zaehler zurueck, es
		// gibt also gleich wieder Knopfdruecke (wichtig fuers Duell, das genau die braucht).
		bool nurNochSchliessen = ++_klicksAmSelbenDialog > MaxKlicksProDialog && !_imZugende;

		if (Ausstiegsknopf.TryGetValue(dialog.Name.ToString(), out string ausstieg))
			knopf = dialog.GetNodeOrNull<BaseButton>(ausstieg) ?? FindeSichtbarenKnopf(dialog);
		else if (nurNochSchliessen)
			knopf = null;
		else if (_mitAktionen && _inAktion)
			knopf = _klicksSeitAktion <= MaxKlicksProAktion ? WaehleKnopf(dialog) : null;
		else
			knopf = FindeSichtbarenKnopf(dialog);

		if (knopf != null)
		{
			if (_ausfuehrlich)
				GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → Knopf \"" + BeschriftungVon(knopf) + "\"");

			_knopfKlicks++;
			knopf.EmitSignal(BaseButton.SignalName.Pressed);
			return;
		}

		// Manche Dialoge haben gar keinen Knopf, sondern nur ein Eingabefeld, das mit der Eingabetaste
		// bestätigt wird – der Geburtsdialog etwa verlangt einen Namen und ignoriert den Rechtsklick
		// bewusst. Für den Treiber war ein solcher Dialog eine Sackgasse: nichts zu drücken, und der
		// Rechtsklick läuft ins Leere. Deshalb wird hier ein Name eingetragen und abgeschickt.
		//
		// Mit --zustaende höchstens einmal je Anlauf, sonst nie wieder ein Rechtsklick: Der
		// Siegesbildschirm des Auftrags trägt ein Namensfeld für die Bestenliste und einen Knopf, der
		// sich nach dem Eintragen selbst sperrt. Danach ist kein Knopf mehr da, und der Treiber schickte
		// den Namen gemessen 4 000-mal ab, während der einzige Ausgang – der Rechtsklick – nie an die
		// Reihe kam. Nach dem Rechtsklick wird die Merkung zusammen mit dem Klickzähler zurückgesetzt,
		// sodass beides sich abwechselt und der Geburtsdialog, der nur über sein Feld weitergeht,
		// weiterhin bedient wird.
		//
		// Warum nur mit dem Schalter, obwohl es ein echter Fehler ist: Erreichbar ist dieser Zustand
		// allein über --zustaende. Ein Auftrag ist im Durchlauf sonst nie aktiv – die Spielanlage lässt
		// die Schwierigkeit auf „Keine (freies Spiel)“ (NewLocalGameMenu.GetSelectedAuftrag), und
		// NewGameManager setzt gar keinen –, also zeigt Kontor.PruefeAuftragErfuellt den Bildschirm nie.
		// Kein anderer Dialog des Standardpfads hat ein Eingabefeld und zugleich keinen Ausweg per Knopf.
		// Die Änderung würde dort also nichts heilen, aber die Klickfolge verschieben – und damit die
		// startwertgebundenen Messungen in docs/e2e-messwerte.md entwerten, wie es die Datei selbst als
		// Regel führt. Zeigt sich einmal ein Dialog des Standardpfads mit demselben Muster, gehört das
		// Gatter weg und das Band neu gemessen.
		if ((!_mitZustaenden || !_eingabeGeschickt)
		    && dialog.FindChild("*LineEdit*", true, false) is LineEdit feld && feld.IsVisibleInTree())
		{
			_eingabeGeschickt = true;

			if (_ausfuehrlich)
				GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → Eingabe \"Testkind\"");

			feld.Text = "Testkind";
			feld.EmitSignal(LineEdit.SignalName.TextSubmitted, feld.Text);
			return;
		}

		if (_ausfuehrlich)
			GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → ui_next_or_close");

		SchickeAbbruch();

		// Nach dem Ausstiegsversuch wieder Knöpfe und Eingaben zulassen: Ein Dialog, der sich nur über
		// einen Knopf weiterschalten lässt (die Abrechnung etwa), käme sonst nach drei Klicks nie mehr
		// voran.
		_klicksAmSelbenDialog = 0;
		_eingabeGeschickt = false;
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

	/// <summary>
	/// Waehlt einen der sichtbaren Knoepfe des Dialogs. Zufaellig statt immer den ersten: Sonst wuerde in
	/// jedem Dialog nur der erste Knopf je erreicht – bei Reitern also immer derselbe Reiter.
	/// </summary>
	private BaseButton WaehleKnopf(Control dialog)
	{
		var knoepfe = new List<BaseButton>();
		SammleKnoepfe(dialog, knoepfe);
		knoepfe.RemoveAll(k => Gesperrt.Contains(k.Name.ToString()));

		return knoepfe.Count == 0 ? null : knoepfe[_auswahl.Next(knoepfe.Count)];
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

			// Bewusst keine Prüfung auf steigendes Alter: Es gibt zu viele richtige Ausnahmen – der
			// Erbfall setzt einen jüngeren Erben ein, und im Schuldturm wird der Zug übersprungen, ohne
			// dass der Spieler altert. Die Prüfung schlug dadurch bei völlig korrekten Läufen an.
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

	/// <summary>
	/// Der Warteschritt aller Schleifen – und damit die Stelle, an der <c>--zustaende</c> nachsieht,
	/// welche seiner Ansichten gerade auf dem Schirm steht. Ohne den Schalter passiert hier nichts.
	/// </summary>
	private SignalAwaiter NaechsterFrame()
	{
		NotiereZustandsansichten();

		return ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private void Bericht(int jahre)
	{
		GD.Print("--- Ergebnis ---");

		// Scheitert der Aufbau, gibt es noch keinen Spielzustand. Ohne diese Absicherung wirft der
		// Bericht dann selbst eine NullReferenceException – und weil sie den Aufruf von Quit() verhindert,
		// läuft der Prozess endlos weiter, statt mit einer verwertbaren Meldung abzubrechen.
		if (!_spielLaeuft)
		{
			GD.PrintErr("Es kam kein Spiel zustande.");
			BerichteFehler();
			return;
		}

		GD.Print("Gespielte Jahre:   " + (SW.Dynamisch.GetAktuellesJahr() - SW.Statisch.StartJahr) + " von " + jahre + " geplant");

		// Ein reguläres Spielende ist ein gültiger Ausgang, kein Fehler – aber es muss im Bericht stehen,
		// sonst ist ein Lauf, der nach 12 von 40 Jahren endete, nicht von einem vollständigen zu
		// unterscheiden. Bewusst ohne Schwellwert, ab wann ein Ende „zu früh" wäre: Die Zeile macht es
		// sichtbar, die Bewertung gehört zum Anlass der Messung.
		if (_spielBeendet)
			GD.Print("Spiel beendet:     regulär im Jahr " + _endeJahr
			         + " (erloschene Dynastie, erfüllter Auftrag oder letzter Spieler ausgeschieden)");
		GD.Print("Züge:              " + _gespielteZuege);
		// Rechtsklicks fallen bei inszenierten Sequenzen (Duell) reichlich an, ohne etwas zu bewirken –
		// aussagekräftig ist vor allem, wie oft wirklich ein Knopf gedrückt wurde.
		GD.Print("Klicks in Dialogen:" + _dialogKlicks + " (davon Knöpfe: " + _knopfKlicks + ")");
		GD.Print("Besuchte Bereiche: " + _besuchteBereiche + ", davon Staedte: " + _besuchteStaedte);
		GD.Print("Verkaufte Waren:   " + _verkaufteWaren + " vor Ort, " + _exportierteWaren + " exportiert, " + _exportAbgelehnt + "x unrentabel");

		// Das Vermoegen am Ende zeigt, ob der Kern des Spiels ueberhaupt getragen hat: Ohne Produktion
		// und Verkauf bleibt es beim Startgeld, und alles was daran haengt (Steuern, Kredite, Auftrag)
		// laeuft mit unrealistischen Zahlen.
		for (int i = 1; i <= SW.Dynamisch.GetAktivSpielerAnzahl(); i++)
		{
			var sp = SW.Dynamisch.GetHumWithID(i);

			if (sp != null)
				GD.Print("Spieler " + i + ":         " + sp.GetTaler() + " Taler");
		}
		GD.Print("Bediente Knoepfe:  " + _bedienteKnoepfe.Count + " verschiedene");

		if (_mitBildern)
			GD.Print("Bilder:            " + _bilder + " von " + _bilderJeAnsicht.Count + " Ansichten");

		BerichteZustaende();
		BerichteFehler();
	}

	/// <summary>
	/// Weist die vier Zustands-Ansichten einzeln aus und lässt den Durchlauf scheitern, wenn eine
	/// davon ausblieb. Ohne diese Zusicherung wäre <c>--zustaende</c> wertlos: Der Cheat liefe durch,
	/// die Ansicht bliebe still aus, und niemand wüsste davon – genau so sind hier schon zweimal
	/// ganze Rundenende-Blöcke monatelang unbemerkt gefehlt.
	/// </summary>
	private void BerichteZustaende()
	{
		if (!_mitZustaenden)
			return;

		var fehlend = new List<string>();

		GD.Print("Zustands-Ansichten:");

		foreach (string name in Zustandsansichten)
		{
			bool erreicht = _erreichteAnsichten.Contains(name);
			GD.Print("  " + name.PadRight(22) + (erreicht ? "erreicht" : "NICHT erreicht"));

			if (!erreicht)
				fehlend.Add(name);
		}

		if (fehlend.Count > 0)
			_fehler.Add("Mit --zustaende blieben diese Ansichten unerreicht: " + string.Join(", ", fehlend) + ".");
	}

	private void BerichteFehler()
	{
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
