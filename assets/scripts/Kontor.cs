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

		// TODO: Zugnachrichten (Kinder, Hochzeit, Todesfälle, ...) und Jahresbuch migrieren
		_rundenManager.BeendeZug();

		await NaechstenSpielerAnkuendigen();
	}
}
