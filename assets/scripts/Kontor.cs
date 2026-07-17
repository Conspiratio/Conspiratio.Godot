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
	private Main _main;

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

		Hide();
	}

	/// <summary>
	/// Startet das eigentliche Spiel, nachdem alle Spieler erstellt wurden.
	/// </summary>
	public async void StartGame()
	{
		_rundenManager = new RundenManager();

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

		// Ab dem zweiten Jahr: Die Aufträge des Vorjahres im Buch abwickeln (Exporte und Produktion)
		if (SW.Dynamisch.GetAktuellesJahr() != SW.Statisch.StartJahr)
		{
			var buch = new BuchManager().ErstelleJahresbuchFuerAktivenSpieler();
			UpdateHud();
			await ZeigeBuch(buch);
		}

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
	/// Wird von der Stadtansicht beim Schließen aufgerufen.
	/// </summary>
	public void ReturnFromStadt()
	{
		UpdateHud();
		Show();
		SetProcessInput(true);
	}

	private void _on_button_handel_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.Stadt.ShowStadt();
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
