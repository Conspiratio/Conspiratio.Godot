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
/// Die Zustände, die `--zustaende` eigens herstellt.
///
/// Vier Ansichten brauchen einen Spielzustand statt mehr Jahre - Gericht, Wahl,
/// Stützpunktverwaltung und Auftragssieg. Der Schalter baut sie und prüft, dass sie
/// erreicht wurden; er gehört ausdrücklich <b>nicht</b> in einen Messlauf.
/// </summary>
public partial class E2eTreiber
{
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
}
