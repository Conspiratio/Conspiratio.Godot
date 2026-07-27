using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Privilegien;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Handelszertifikat-Verleihung (Migration von Handelszertifikat/HandelszertifikatAnzeigen): zeigt als
/// Urkunde den Erlass, mit dem dem Spieler der Handel mit einem neuen Rohstoff gestattet wird. Dazu erklingen
/// eine Fanfare und die passende Sprachausgabe. Der Rechtsklick (bzw. Esc) schließt die Urkunde. Der Vermerk
/// wird bereits vom HandelszertifikatManager der Lib zurückgesetzt.
/// </summary>
public partial class HandelszertifikatDialog : Control
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelText;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelText = GetNode<Label>(LabelTextPath);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Zeigt die Urkunde der Handelszertifikat-Verleihung und schließt sie beim Rechtsklick.</summary>
	public Task ShowDialog(HandelszertifikatErgebnis ergebnis)
	{
		_labelText.Text = ergebnis.UrkundenText;

		SoundManager.Instance.PlayFanfare();
		SoundManager.Instance.SpieleStimmen(
			"res://assets/voice/31_auf_grund_eurer_besonderen_erfolge.wav",
			"res://assets/voice/31_wird_euch_ab_heute_gestattet_" + ergebnis.RohstoffName.ToLower() + ".wav");

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	private void CloseDialog()
	{
		Hide();
		SetProcessInput(false);
		_dialogClosed?.TrySetResult(true);
	}
}
