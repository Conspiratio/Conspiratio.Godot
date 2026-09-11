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
/// Die Handelsrunde in der Heimatstadt - der Kern des Durchlaufs.
///
/// Ohne sie erwirtschaftet der Lauf nichts, und alles, was am Vermögen hängt (Abrechnung,
/// Steuern, Kredite, Schuldturm, Auftragsziele), rechnet mit unrealistischen Zahlen.
/// </summary>
public partial class E2eTreiber
{
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
		await ZeigeStadtinformationen(stadtId);
		await FuehreHandelsrundeDurch(stadtId);

		if (_mitAktionen)
			await BedieneBildschirm(nameof(Stadt));

		// Zurück auf die Karte, damit der Aufrufer von dort aus zum Kontor findet.
		SchickeAbbruch();
		await NaechsterFrame();
	}

	/// <summary>
	/// Öffnet die Stadtinformationen – die einzige Ansicht, die sonst in keinem Lauf vorkommt.
	///
	/// Im Spiel hängen sie an einem <b>Rechtsklick auf eine Stadt der Handelskarte</b>, und der setzt
	/// voraus, dass die Karte weiß, über welcher Stadt der Zeiger steht (<c>_hoverStadt</c>).
	/// Headless erreichen synthetische Mausereignisse die Karte überhaupt nicht – derselbe Grund, aus
	/// dem die Stadtansicht oben direkt geöffnet wird. Also derselbe Ausweg: der Aufruf, den
	/// <c>Weltkarte.ZeigeStadtinformationen</c> beim echten Rechtsklick auch macht.
	/// </summary>
	private async Task ZeigeStadtinformationen(int stadtId)
	{
		_ = _main.StadtInformationenDialog.ShowDialog(stadtId);

		if (!await WarteAufSichtbar(nameof(StadtInformationenDialog), 60))
		{
			_fehler.Add("Die Stadtinformationen zu Stadt " + stadtId + " ließen sich nicht öffnen.");
			return;
		}

		await Schiesse(nameof(StadtInformationenDialog));

		// Der Dialog kennt keinen Knopf, nur den Rechtsklick - wie das Pergament-Muster es vorsieht.
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
		int staetten = ErmittleSinnvolleStaetten(stadtId, rohstoffId, ware, out bool lagerIstDieGrenze);

		// Erst mal nehmen, dann teilen - wie die Lib. Ein Zwischenwert „Arbeiter je Stätte“ wäre bei
		// Wolle, Fell und Rum null (ein Arbeiter je zwei Werkstätten), und der Lauf endete dann je
		// nach Seed in einer DivideByZeroException: Welche Ware der Treiber betreibt, hängt an der
		// Werkstätte seiner Heimatstadt.
		int arbeiter = Math.Max(1, staetten * ware.GetArbeiter() / ware.GetWerkstaetten());

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
	///
	/// Gerechnet wird mit dem Verhältnis der Ware statt mit einem Zwischenwert „Arbeiter je Stätte“:
	/// Der wäre bei Wolle, Fell und Rum null und die Division darunter unmöglich.
	/// </summary>
	private static int ErmittleSinnvolleStaetten(int stadtId, int rohstoffId, Rohstoff ware,
	                                             out bool lagerIstDieGrenze)
	{
		double effizienz = SW.Dynamisch.GetStadtwithID(stadtId).GetEffizienzVonRohstoffMitIDX(rohstoffId);
		int ertragJeStaette = Math.Max(1, (int)(ware.GetWSProdProWS() * 0.9 * effizienz));

		int lagerplatzStueck = SW.Dynamisch.GetAktHum().ErmittleLagerplatzInStadt(stadtId, rohstoffId)
		                       * ware.GetLagermengeProQMeter();

		int nachLager = Math.Max(1, lagerplatzStueck / ertragJeStaette);
		int nachArbeitern = Math.Max(1, SW.Statisch.GetMaxArbeiterAnzahl() * ware.GetWerkstaetten() /
		                                ware.GetArbeiter());

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
}
