using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Kampf;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Stützpunkt-Kauf-Dialog (Migration von frmStuetzpunktKaufen): zeigt Name, Art, Besitzer, Wert,
/// Zustand und Sicherheit/Tarnung eines fremden Stützpunkts und lässt den Spieler dem Besitzer ein
/// Kaufangebot unterbreiten (nur einmal pro Jahr). Die Logik liegt im SoeldnerRaeuberManager der Lib.
/// </summary>
public partial class StuetzpunktKaufenDialog : Control
{
	[Export]
	public NodePath LabelNamePath { get; set; }

	[Export]
	public NodePath LabelBeschreibungPath { get; set; }

	[Export]
	public NodePath LabelWertPath { get; set; }

	[Export]
	public NodePath LabelZustandPath { get; set; }

	[Export]
	public NodePath LabelSicherheitPath { get; set; }

	[Export]
	public NodePath NumericAngebotPath { get; set; }

	private Label _labelName;
	private Label _labelBeschreibung;
	private Label _labelWert;
	private Label _labelZustand;
	private Label _labelSicherheit;
	private controls.NumericButtonWithSounds _numericAngebot;

	private readonly SoeldnerRaeuberManager _manager = new SoeldnerRaeuberManager();
	private int _stuetzpunktId;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelName = GetNode<Label>(LabelNamePath);
		_labelBeschreibung = GetNode<Label>(LabelBeschreibungPath);
		_labelWert = GetNode<Label>(LabelWertPath);
		_labelZustand = GetNode<Label>(LabelZustandPath);
		_labelSicherheit = GetNode<Label>(LabelSicherheitPath);
		_numericAngebot = GetNode<controls.NumericButtonWithSounds>(NumericAngebotPath);

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Öffnet den Kauf-Dialog für den angegebenen Stützpunkt.</summary>
	public Task ShowDialog(int stuetzpunktId)
	{
		_stuetzpunktId = stuetzpunktId;

		var info = _manager.GetKaufInfo(stuetzpunktId);
		_labelName.Text = info.Name;
		_labelBeschreibung.Text = info.Beschreibung;
		_labelWert.Text = "Wert: " + info.Wert.ToStringGeld();
		_labelZustand.Text = "Zustand: " + info.ZustandProzent + " %";
		_labelSicherheit.Text = info.SicherheitLabel + ": " + info.SicherheitProzent + " %";

		_numericAngebot.MinimalerWert = 0;
		_numericAngebot.MaximalerWert = _manager.GetSpielerTaler();
		_numericAngebot.Wert = info.Wert;

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	private async void _on_button_kaufangebot_pressed()
	{
		SetProcessInput(false);

		bool erfolg = await _manager.KaufangebotAbgeben(_stuetzpunktId, _numericAngebot.Wert);

		if (erfolg)
		{
			CloseDialog();
			return;
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void CloseDialog()
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(true);
	}
}
