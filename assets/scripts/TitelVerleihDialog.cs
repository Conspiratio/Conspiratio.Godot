using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Titel;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Titelverleihung (Migration von TitelVerleihForm): zeigt als Urkunde den feierlichen Erlass des
/// Regenten, mit dem dem Spieler ein neuer Adelstitel verliehen wird. Der Rechtsklick (bzw. Esc) schließt
/// die Urkunde. Der neue Titel wird bereits vom TitelVerleihungManager der Lib gesetzt.
/// </summary>
public partial class TitelVerleihDialog : Control
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

	/// <summary>Zeigt die Urkunde der Titelverleihung und schließt sie beim Rechtsklick.</summary>
	public Task ShowDialog(TitelverleihungErgebnis ergebnis)
	{
		_labelText.Text = ergebnis.UrkundenText;

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
