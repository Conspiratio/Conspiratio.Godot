using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class TextDialog : Control, IShowText
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath LinkButtonClosePath { get; set; }

	private Label _labelText;
	private controls.LinkButtonWithSounds _linkButtonClose;

	private TaskCompletionSource<bool> _dialogClosed;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_linkButtonClose = GetNode<controls.LinkButtonWithSounds>(LinkButtonClosePath);

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(string text)
	{
		_labelText.Text = text;

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void _on_link_button_close_pressed()
	{
		CloseDialog();
	}

	private void CloseDialog()
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(true);
	}

	private Task CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}
}
