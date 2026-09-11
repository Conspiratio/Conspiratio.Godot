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
/// Der Rundgang durch das Kontor und die Bedienung dessen, was dabei aufgeht.
///
/// Hier steht alles, was mit Warten, Finden und Drücken zu tun hat: Ein Dialog gilt erst
/// als bereit, wenn er sichtbar ist <b>und</b> Eingaben verarbeitet, und aus jedem Bereich
/// muss der Treiber wieder ins Kontor zurückfinden.
/// </summary>
public partial class E2eTreiber
{
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
}
