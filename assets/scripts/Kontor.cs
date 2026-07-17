using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
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

		// TODO: Zugnachrichten (Kinder, Hochzeit, Todesfälle, ...) und Jahresbuch migrieren
		_rundenManager.BeendeZug();

		await NaechstenSpielerAnkuendigen();
	}
}
