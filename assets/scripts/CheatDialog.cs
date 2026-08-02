using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Cheatfenster (Migration der WinForms-Cheatbox): manipuliert den aktiven menschlichen Spieler –
/// Taler und Ansehen setzen, Handelsrechte für alle Rohstoffe freischalten sowie Aktionen (Kind bekommen,
/// sofort altern, Amt niederlegen, ein Amt übernehmen, ein Haus bauen, sich verklagen lassen). Nur
/// erreichbar, wenn beim neuen Spiel der Cheatmodus aktiviert wurde (Öffnen mit Taste „U" im Kontor).
/// </summary>
public partial class CheatDialog : DialogBase
{
	[Export] public NodePath LineEditTalerPath { get; set; }
	[Export] public NodePath LineEditAnsehenPath { get; set; }
	[Export] public NodePath GridRohstoffePath { get; set; }
	[Export] public NodePath OptionStufePath { get; set; }
	[Export] public NodePath OptionGebietPath { get; set; }
	[Export] public NodePath OptionAmtPath { get; set; }
	[Export] public NodePath OptionStadtPath { get; set; }
	[Export] public NodePath OptionHaustypPath { get; set; }

	private LineEdit _lineEditTaler;
	private LineEdit _lineEditAnsehen;
	private GridContainer _gridRohstoffe;
	private OptionButton _optionStufe;
	private OptionButton _optionGebiet;
	private OptionButton _optionAmt;
	private OptionButton _optionStadt;
	private OptionButton _optionHaustyp;
	private PackedScene _checkBoxScene;

	private CheatManager _cheatManager;

	protected override void OnReady()
	{
		_lineEditTaler = GetNode<LineEdit>(LineEditTalerPath);
		_lineEditAnsehen = GetNode<LineEdit>(LineEditAnsehenPath);
		_gridRohstoffe = GetNode<GridContainer>(GridRohstoffePath);
		_optionStufe = GetNode<OptionButton>(OptionStufePath);
		_optionGebiet = GetNode<OptionButton>(OptionGebietPath);
		_optionAmt = GetNode<OptionButton>(OptionAmtPath);
		_optionStadt = GetNode<OptionButton>(OptionStadtPath);
		_optionHaustyp = GetNode<OptionButton>(OptionHaustypPath);
		_checkBoxScene = GD.Load<PackedScene>("res://scenes/controls/CheckBoxWithSounds.tscn");
	}

	/// <summary>Öffnet das Cheatfenster und übernimmt die aktuellen Werte des aktiven Spielers.</summary>
	public Task ShowDialog()
	{
		_cheatManager = new CheatManager();

		var spieler = SW.Dynamisch.GetAktHum();
		_lineEditTaler.Text = spieler.GetTaler().ToString();
		_lineEditAnsehen.Text = spieler.GetPermaAnsehen().ToString();

		BaueHandelsrechte();
		BefuelleComboboxen();

		return ShowAndAwait();
	}

	private void BaueHandelsrechte()
	{
		foreach (Node child in _gridRohstoffe.GetChildren())
		{
			_gridRohstoffe.RemoveChild(child);
			child.QueueFree();
		}

		var spieler = SW.Dynamisch.GetAktHum();

		for (int i = 1; i < SW.Statisch.GetMaxRohID(); i++)
		{
			var checkBox = _checkBoxScene.Instantiate<CheckBoxWithSounds>();
			checkBox.Text = SW.Dynamisch.GetRohstoffwithID(i).GetRohName();

			bool hatRecht = spieler.GetRohstoffrechteX(i);
			checkBox.ButtonPressed = hatRecht;
			// Bereits vorhandene Rechte lassen sich wie im Original nicht wieder abwählen.
			checkBox.Disabled = hatRecht;

			int rohstoffId = i;
			checkBox.Toggled += pressed => SW.Dynamisch.GetAktHum().SetRohstoffrechteXZuY(rohstoffId, pressed);

			_gridRohstoffe.AddChild(checkBox);
		}
	}

	private void BefuelleComboboxen()
	{
		FuelleOption(_optionStufe, _cheatManager.GetAmtsstufen());
		FuelleOption(_optionStadt, _cheatManager.GetStaedte());
		FuelleOption(_optionHaustyp, _cheatManager.GetHaustypen());

		// Gebiete und Ämter hängen von der Amtsstufe ab (Kaskade).
		AktualisiereAmtsauswahl();
	}

	private static void FuelleOption(OptionButton option, System.Collections.Generic.IReadOnlyList<string> eintraege)
	{
		option.Clear();

		foreach (var eintrag in eintraege)
			option.AddItem(eintrag);

		if (option.ItemCount > 0)
			option.Select(0);
	}

	private void AktualisiereAmtsauswahl()
	{
		int stufe = _optionStufe.Selected < 0 ? 0 : _optionStufe.Selected;
		FuelleOption(_optionGebiet, _cheatManager.GetGebiete(stufe));
		FuelleOption(_optionAmt, _cheatManager.GetAemter(stufe));
	}

	private void _on_option_stufe_item_selected(long index) => AktualisiereAmtsauswahl();

	private void _on_line_edit_taler_text_submitted(string text)
	{
		if (int.TryParse(text, out int taler))
			SW.Dynamisch.GetAktHum().SetTaler(taler);
	}

	private void _on_line_edit_ansehen_text_submitted(string text)
	{
		if (int.TryParse(text, out int ansehen))
			SW.Dynamisch.GetAktHum().SetPermaAnsehen(ansehen);
	}

	private async void _on_button_kind_pressed()
	{
		SW.Dynamisch.GetAktHum().SetKindBekommen(true);
		await Melde("Ihr bekommt dieses Jahr ein Kind.");
	}

	private async void _on_button_sterben_pressed()
	{
		SW.Dynamisch.GetAktHum().SetAlter(999);
		await Melde("Euer Ende naht – Ihr seid nun uralt.");
	}

	private async void _on_button_amt_niederlegen_pressed()
	{
		if (SW.Dynamisch.GetAktHum().GetAmtID() != 0)
			SW.Dynamisch.SetAmtsenthebungVonID(SW.Dynamisch.GetAktiverSpieler());
		else
			await Melde("Ihr besitzt kein Amt");
	}

	private async void _on_button_amt_uebernehmen_pressed()
	{
		string meldung = _cheatManager.UebernehmeAmt(_optionStufe.Selected, _optionGebiet.Selected, _optionAmt.Selected);
		await Melde(meldung);
	}

	private async void _on_button_haus_bauen_pressed()
	{
		_cheatManager.BaueHaus(_optionStadt.Selected, _optionHaustyp.Selected);
		await Melde("Das Haus wurde errichtet.");
	}

	private async void _on_button_verklagen_pressed()
	{
		string meldung = _cheatManager.LasseVerklagen();
		await Melde(meldung);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}

	/// <summary>
	/// Zeigt eine Rückmeldung. Währenddessen wird die Eingabe des Cheatfensters deaktiviert, damit der
	/// Rechtsklick zum Schließen der Meldung nicht auch das Cheatfenster schließt.
	/// </summary>
	private async Task Melde(string text)
	{
		SetProcessInput(false);
		await SW.UI.ShowText.ShowDialog(text);

		if (Visible)
			SetProcessInput(true);
	}
}
