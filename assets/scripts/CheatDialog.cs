using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Cheatfenster (Migration der WinForms-Cheatbox): manipuliert den aktiven menschlichen Spieler –
/// Taler und Ansehen setzen, Handelsrechte für alle Rohstoffe freischalten sowie ein paar Aktionen
/// (Kind bekommen, sofort altern/sterben, Amt niederlegen). Nur erreichbar, wenn beim neuen Spiel der
/// Cheatmodus aktiviert wurde (Öffnen mit Taste „U" im Kontor).
/// </summary>
public partial class CheatDialog : DialogBase
{
	[Export]
	public NodePath LineEditTalerPath { get; set; }

	[Export]
	public NodePath LineEditAnsehenPath { get; set; }

	[Export]
	public NodePath GridRohstoffePath { get; set; }

	private LineEdit _lineEditTaler;
	private LineEdit _lineEditAnsehen;
	private GridContainer _gridRohstoffe;
	private PackedScene _checkBoxScene;

	protected override void OnReady()
	{
		_lineEditTaler = GetNode<LineEdit>(LineEditTalerPath);
		_lineEditAnsehen = GetNode<LineEdit>(LineEditAnsehenPath);
		_gridRohstoffe = GetNode<GridContainer>(GridRohstoffePath);
		_checkBoxScene = GD.Load<PackedScene>("res://scenes/controls/CheckBoxWithSounds.tscn");
	}

	/// <summary>Öffnet das Cheatfenster und übernimmt die aktuellen Werte des aktiven Spielers.</summary>
	public Task ShowDialog()
	{
		var spieler = SW.Dynamisch.GetAktHum();
		_lineEditTaler.Text = spieler.GetTaler().ToString();
		_lineEditAnsehen.Text = spieler.GetPermaAnsehen().ToString();

		BaueHandelsrechte();

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

	private void _on_button_kind_pressed()
	{
		SW.Dynamisch.GetAktHum().SetKindBekommen(true);
		SW.Dynamisch.BelTextAnzeigen("Ihr bekommt dieses Jahr ein Kind.");
	}

	private void _on_button_sterben_pressed()
	{
		SW.Dynamisch.GetAktHum().SetAlter(999);
		SW.Dynamisch.BelTextAnzeigen("Euer Ende naht – Ihr seid nun uralt.");
	}

	private void _on_button_amt_niederlegen_pressed()
	{
		if (SW.Dynamisch.GetAktHum().GetAmtID() != 0)
			SW.Dynamisch.SetAmtsenthebungVonID(SW.Dynamisch.GetAktiverSpieler());
		else
			SW.Dynamisch.BelTextAnzeigen("Ihr besitzt kein Amt");
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
