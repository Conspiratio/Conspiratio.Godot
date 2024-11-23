using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class YesNoDialog : Control, IYesNoQuestion
{
	[Export]
	public NodePath LabelQuestionPath { get; set; }
	
	[Export]
	public NodePath LabelYesPath { get; set; }
	
	[Export]
	public NodePath LabelNoPath { get; set; }

	private Label _labelQuestion;
	private Label _labelYes;
	private Label _labelNo;
	
	private TaskCompletionSource<DialogResultGame> _dialogClosed;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelQuestion = GetNode<Label>(LabelQuestionPath);
		_labelYes = GetNode<Label>(LabelYesPath);
		_labelNo = GetNode<Label>(LabelNoPath);
		
		Hide();
	}
	
	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionPressed("ui_next_or_close"))
		{
			GD.Print("Process ui_next_or_close Action");
			CloseDialog(DialogResultGame.Cancel);
		}
	}

	public async Task<DialogResultGame> ShowDialogText(string textQuestion, string textYes = "Ja", string textNo = "Nein")
	{
		_labelQuestion.Text = textQuestion;
		_labelYes.Text = textYes;
		_labelNo.Text = textNo;
		
		GD.Print("Showing dialog");
		Show();
		return await CloseDialogTask();
	}
	
	private void _on_label_yes_gui_input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.IsPressed() ||
		    mouseButtonEvent.ButtonIndex != MouseButton.Left) 
			return;
		
		GD.Print("Clicked Yes");
		CloseDialog(DialogResultGame.Yes);
	}
	
	private void _on_label_no_gui_input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.IsPressed() ||
		    mouseButtonEvent.ButtonIndex != MouseButton.Left) 
			return;
		
		GD.Print("Clicked No");
		CloseDialog(DialogResultGame.No);
	}

	private void CloseDialog(DialogResultGame dialogResult)
	{
		Hide();
		_dialogClosed?.TrySetResult(dialogResult);
	}
	
	private Task<DialogResultGame> CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<DialogResultGame>();
		return _dialogClosed.Task;
	}
}