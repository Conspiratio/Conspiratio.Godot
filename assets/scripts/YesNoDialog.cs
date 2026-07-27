using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class YesNoDialog : Control, IYesNoQuestion
{
	[Export]
	public NodePath LabelQuestionPath { get; set; }
	
	[Export]
	public NodePath LinkButtonYesPath { get; set; }
	
	[Export]
	public NodePath LinkButtonNoPath { get; set; }

	private Label _labelQuestion;
	private controls.LinkButtonWithSounds _linkButtonYes;
	private controls.LinkButtonWithSounds _linkButtonNo;
	
	private TaskCompletionSource<DialogResultGame> _dialogClosed;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelQuestion = GetNode<Label>(LabelQuestionPath);
		_linkButtonYes = GetNode<controls.LinkButtonWithSounds>(LinkButtonYesPath);
		_linkButtonNo = GetNode<controls.LinkButtonWithSounds>(LinkButtonNoPath);

		HideAndDisableInput();
	}
	
	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close")) 
			return;
		
		SoundManager.Instance.PlayRightClick();
		CloseDialog(DialogResultGame.Cancel);
	}

	public async Task<DialogResultGame> ShowDialogText(string textQuestion, string textYes = "Ja", string textNo = "Nein")
	{
		_labelQuestion.Text = textQuestion;
		_linkButtonYes.Text = textYes;
		_linkButtonNo.Text = textNo;
		
		Show();
		SetProcessInput(true);
		return await CloseDialogTask();
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void _on_link_button_yes_pressed()
	{
		CloseDialog(DialogResultGame.Yes);
	}
	
	private void _on_link_button_no_pressed()
	{
		CloseDialog(DialogResultGame.No);
	}

	private void CloseDialog(DialogResultGame dialogResult)
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(dialogResult);
	}
	
	private Task<DialogResultGame> CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<DialogResultGame>();
		return _dialogClosed.Task;
	}
}