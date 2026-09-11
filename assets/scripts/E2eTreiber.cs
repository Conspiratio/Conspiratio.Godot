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
