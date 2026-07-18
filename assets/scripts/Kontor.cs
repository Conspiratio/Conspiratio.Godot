using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Kontor : Control
{
	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;

	private RundenManager _rundenManager;
	private SpeicherManager _speicherManager;
	private Main _main;
	private bool _geradeGeladen;

	[Export]
	public NodePath LabelPlayerNameAndOfficePath { get; set; }

	[Export]
	public NodePath LabelPlaceDatePath { get; set; }

	[Export]
	public NodePath LabelTalerPath { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelPlayerNameAndOffice = GetNode<Label>(LabelPlayerNameAndOfficePath);
		_labelPlaceDate = GetNode<Label>(LabelPlaceDatePath);
		_labelTaler = GetNode<Label>(LabelTalerPath);

		_main = GetParent<Main>();

		// Die Klickbereiche des Kontors sind unsichtbar; ihre goldene Beschriftung erscheint nur bei MouseOver
		foreach (string areaName in new[] { "AreaHandel", "AreaHinterzimmer", "AreaSchreibstube", "AreaKirche", "AreaKampf", "AreaFenster" })
		{
			var area = GetNode<Button>(areaName);
			area.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			var label = GetNode<Label>("Label" + areaName.Substring("Area".Length));
			area.MouseEntered += () => label.Visible = true;
			area.MouseExited += () => label.Visible = false;
		}

		SetProcessInput(false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr das Spiel verlassen und zum Hauptmenü zurückkehren?") !=
		    DialogResultGame.Yes)
		{
			SetProcessInput(true);
			return;
		}

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euer Spiel vorher speichern?") == DialogResultGame.Yes)
		{
			if (_speicherManager.Speichern(SW.Dynamisch.SpielName, out string fehler))
			{
				ClientSettings.LetzterSpielstand = SW.Dynamisch.SpielName;
				await SW.UI.ShowText.ShowDialog("Speichervorgang beendet");
			}
			else
			{
				await SW.UI.ShowText.ShowDialog(fehler);
			}
		}

		Hide();
	}

	/// <summary>
	/// Startet das eigentliche Spiel, nachdem alle Spieler erstellt wurden.
	/// </summary>
	public async void StartGame()
	{
		_rundenManager = new RundenManager();
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		_geradeGeladen = false;

		Show();
		await NaechstenSpielerAnkuendigen();
	}

	/// <summary>
	/// Setzt ein geladenes Spiel fort (das Jahresbuch des Vorjahres wird dabei nicht erneut abgewickelt).
	/// </summary>
	public async void ContinueLoadedGame()
	{
		_rundenManager = new RundenManager();
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		_geradeGeladen = true;

		Show();
		await NaechstenSpielerAnkuendigen();
	}

	private async Task NaechstenSpielerAnkuendigen()
	{
		UpdateHud();

		var spieler = SW.Dynamisch.GetAktHum();
		await SW.UI.ShowText.ShowDialog("Nächster Spieler\n\n" + spieler.GetTitelGegendert() + " " + spieler.GetName() +
		                                ",\n" + spieler.GetAmtNameUndOrt());

		_rundenManager.BeginneZug();

		if (_rundenManager.SitztAktiverSpielerImKerker())
		{
			_rundenManager.KerkerAufenthaltAbschliessen();
			await SW.UI.ShowText.ShowDialog("Ihr verbringt dieses Jahr im Schuldturm...");

			// Im Schuldturm wird der Zug übersprungen (der Spieler altert dabei nicht)
			_rundenManager.SchalteZumNaechstenSpieler();
			await NaechstenSpielerAnkuendigen();
			return;
		}

		// Ab dem zweiten Jahr: Die Aufträge des Vorjahres im Buch abwickeln (Exporte und Produktion).
		// Nach dem Laden eines Spielstands entfällt das, der Zug wird an Ort und Stelle fortgesetzt.
		if (_geradeGeladen == false && SW.Dynamisch.GetAktuellesJahr() != SW.Statisch.StartJahr)
		{
			var buch = new BuchManager().ErstelleJahresbuchFuerAktivenSpieler();
			UpdateHud();
			await ZeigeBuch(buch);
		}

		_geradeGeladen = false;

		// Automatisch speichern (wie im Original zu Beginn jedes Zugs)
		if (_speicherManager.Autosave(out string autosaveFehler))
			ClientSettings.LetzterSpielstand = _speicherManager.GetAutosaveName();
		else
			await SW.UI.ShowText.ShowDialog(autosaveFehler);

		SetProcessInput(true);
	}

	private static async Task ZeigeBuch(BuchErgebnis buch)
	{
		string produktion;

		if (buch.EtwasProduziert == false)
		{
			produktion = "Im letzten Jahr habt Ihr keine Waren produziert.";
		}
		else
		{
			produktion = "Produktion\n\n";

			for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
			{
				if (buch.ProduzierteWaren[rohstoffId] == 0)
					continue;

				produktion += string.Format(SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetTextQualitaetProduktion(),
					BuchManager.QualitaetAlsText(buch.ProduktionsQualitaetProzent[rohstoffId])) + ": " +
					buch.ProduzierteWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "\n";
			}

			if (buch.EtwasVerloren)
			{
				produktion += "\nAus Mangel an Lagerraum opfert Ihr folgende Waren an Bedürftige. Welch' edle Tat!\n";

				for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
				{
					if (buch.VerloreneWaren[rohstoffId] != 0)
						produktion += buch.VerloreneWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "\n";
				}
			}
		}

		await SW.UI.ShowText.ShowDialog(produktion);

		string exporte;

		if (buch.EtwasExportiert == false)
		{
			exporte = "Im letzten Jahr habt Ihr keine Waren exportiert.";
		}
		else
		{
			exporte = "Exporte\n\n";

			for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
			{
				if (buch.ExportierteWaren[rohstoffId] != 0)
					exporte += buch.ExportierteWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() +
					           " für " + buch.ExportErloese[rohstoffId].ToStringGeld() + "\n";
			}

			if (buch.EtwasGestohlen)
			{
				exporte += "\nAufgrund von Überfällen habt Ihr folgende Waren verloren.\n";

				for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
				{
					if (buch.GestohleneWaren[rohstoffId] > 0)
						exporte += buch.GestohleneWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "\n";
				}
			}
		}

		if (buch.EtwasExportiert)
			SoundManager.Instance.PlayCoins();

		await SW.UI.ShowText.ShowDialog(exporte);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	/// <summary>
	/// Wird von untergeordneten Ansichten (Stadt, Schreibstube, ...) beim Schließen aufgerufen.
	/// </summary>
	public void ReturnFromStadt()
	{
		UpdateHud();
		Show();
		SetProcessInput(true);
	}

	private void _on_area_handel_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.Stadt.ShowStadt();
	}

	private void _on_area_schreibstube_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.Schreibstube.ShowSchreibstube();
	}

	private async void _on_area_nicht_implementiert_pressed()
	{
		SetProcessInput(false);

		// TODO: Hinterzimmer, Schreibstube, Kirche, Söldner & Räuber und Geld-aus-dem-Fenster migrieren
		await SW.UI.ShowText.ShowDialog("Wurde noch nicht implementiert");

		if (Visible)
			SetProcessInput(true);
	}

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Kontor A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert() + " Taler";
	}

	private async void _on_button_end_turn_pressed()
	{
		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euren Zug wirklich beenden?") != DialogResultGame.Yes)
		{
			SetProcessInput(true);
			return;
		}

		// Jahresabrechnung berechnen, verbuchen und anzeigen
		var abrechnung = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();
		UpdateHud();
		await _main.AbrechnungDialog.ShowDialog(abrechnung);

		// Zug abschließen (Altern und Zug-Flags), damit die Zugnachrichten mit dem neuen Alter rechnen
		_rundenManager.SchliesseZugAb();

		bool spielVorbei = await ZeigeZugnachrichten();

		if (spielVorbei)
		{
			// Zurück ins Hauptmenü
			HideAndDisableInput();
			return;
		}

		await NaechstenSpielerAnkuendigen();
	}

	/// <summary>
	/// Zeigt die Zugnachrichten-Ereignisse an und schaltet danach auf den nächsten Spieler weiter.
	/// </summary>
	/// <returns>True, wenn das Spiel vorbei ist (kein menschlicher Spieler mehr im Spiel).</returns>
	private async Task<bool> ZeigeZugnachrichten()
	{
		var zugNachrichten = new ZugNachrichtenManager();

		// Gesetzesverstöße mit Strafen
		foreach (string meldung in zugNachrichten.PruefeVerbrechen())
		{
			UpdateHud();
			await SW.UI.ShowText.ShowDialog(meldung);
		}

		zugNachrichten.ErweitereStatistik();

		// Amtseinkommen
		int einkommen = zugNachrichten.KassiereAmtseinkommen();

		if (einkommen > 0)
		{
			UpdateHud();
			SoundManager.Instance.PlayCoins();
			await SW.UI.ShowText.ShowDialog("Einkommen\n\nAls " + SW.Dynamisch.GetAmtsnameVonSPIDx(SW.Dynamisch.GetAktiverSpieler()) +
			                                " verdient Ihr dieses Jahr " + einkommen.ToStringGeld() + ".");
		}

		// Anwesen (Bauzeit, Zustand, Renovierung)
		foreach (string meldung in zugNachrichten.AktualisiereAnwesen())
			await SW.UI.ShowText.ShowDialog("Eigentümer\n\n" + meldung);

		// TODO: Weitere Zugereignisse migrieren (Familie, Hinterzimmer, Kartenspiel, Feste, Gericht, Zufallsereignisse, ...)

		// Sterbeprüfung
		if (zugNachrichten.StirbtAktiverSpieler())
		{
			await SW.UI.ShowText.ShowDialog(zugNachrichten.GetZufaelligeTodesursache());

			string name = SW.Dynamisch.GetAktHum().GetName();
			bool spielVorbei = zugNachrichten.FuehreTodDesAktivenSpielersDurch();

			if (spielVorbei)
			{
				await SW.UI.ShowText.ShowDialog("Der Spieler " + name + " ist verstorben.\nEs befinden sich keine weiteren Mitstreiter in diesem Spiel.\nDas Spiel wird daher beendet.");
				return true;
			}

			await SW.UI.ShowText.ShowDialog("Der Spieler " + name + " ist verstorben und wurde aus dem Spiel entfernt.");

			// Der nächste Spieler ist durch die Entfernung bereits aktiv, es darf nicht weitergeschaltet werden
			return false;
		}

		// Schuldenprozess
		if (zugNachrichten.MussSichVorGlaeubigernVerantworten())
		{
			await SW.UI.ShowText.ShowDialog("Wegen Euren zahlreichen Schulden müsst Ihr Euch nun vor Euren Gläubigern verantworten!");

			var prozess = zugNachrichten.FuehreSchuldenProzessDurch();

			string urteile = "Die Abstimmung Eurer Gläubiger\n\n";

			for (int i = 0; i < prozess.GeschworenenNamen.Count; i++)
				urteile += prozess.GeschworenenNamen[i] + ": " + (prozess.Urteile[i] ? "schuldig!" : "nicht schuldig!") + "\n";

			await SW.UI.ShowText.ShowDialog(urteile);

			await SW.UI.ShowText.ShowDialog(prozess.Schuldig
				? "Aufgrund Eurer zahlreichen Schulden müsst Ihr nächstes\nJahr im Schuldturm verbringen"
				: "Ihr seid noch einmal mit dem Schrecken davon gekommen...");

			UpdateHud();
		}

		await SW.UI.ShowText.ShowDialog("Resümee\n\nIn diesem Jahr gab es keine weiteren besonderen Vorkommnisse");

		_rundenManager.SchalteZumNaechstenSpieler();
		return false;
	}
}
