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
	
	//[Signal]
	//public delegate void DialogClosedEventHandler(DialogResultGame dialogResult);
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelQuestion = GetNode<Label>(LabelQuestionPath);
		_labelYes = GetNode<Label>(LabelYesPath);
		_labelNo = GetNode<Label>(LabelNoPath);
		
		Hide();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
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
		
		// set_process_unhandled_key_input(false)
		//EmitSignal(SignalName.DialogClosed, Variant.From(dialogResult));
		//set_deferred("is_open", false)
		//Hide();
		//if _should_unpause:
		//get_tree().paused = false
	}
	
	private Task<DialogResultGame> CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<DialogResultGame>();
		return _dialogClosed.Task;
	}
	
	public async Task<DialogResultGame> ShowDialogText(string textQuestion, string textYes = "Ja", string textNo = "Nein")
	{
		/*
		var dialogScene = GD.Load<PackedScene>("res://dialogs/YesNoDialog.tscn");
		var dialog = (YesNoDialog)dialogScene.Instantiate();
		*/
		
		// TODO: Add the dialog to the caller control/node. It's the "parent scene".
		// But how can we inject the caller here?
		//GetTree().CurrentScene.AddChild(dialog);
		_labelQuestion.Text = textQuestion;
		_labelYes.Text = textYes;
		_labelNo.Text = textNo;
		
		GD.Print("Showing dialog");
		Show();
		return await CloseDialogTask();
	}
}