using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Ankündigung des nächsten Spielers zu Zugbeginn (Migration von HintNaechsterSpieler):
/// zeigt vor dem Tor-Hintergrund in Gold "Nächster Spieler" sowie Name und Amt des Spielers.
/// Ein Rechtsklick (oder Esc) schließt die Ankündigung. Ersetzt die bisherige Textmeldung.
/// </summary>
public partial class NaechsterSpielerDialog : Control
{
	[Export]
	public NodePath LabelSpielerPath { get; set; }

	private Label _labelSpieler;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelSpieler = GetNode<Label>(LabelSpielerPath);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		GetViewport().SetInputAsHandled();
		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Kündigt den Spieler an; <paramref name="spielerText"/> enthält Name und Amt (mehrzeilig).</summary>
	public Task ShowDialog(string spielerText)
	{
		_labelSpieler.Text = spielerText;

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _dialogClosed.Task;
	}

	private void CloseDialog()
	{
		Hide();
		SetProcessInput(false);
		_dialogClosed?.TrySetResult(true);
	}
}
