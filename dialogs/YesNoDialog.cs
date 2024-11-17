using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.dialogs;

public partial class YesNoDialog : Control
{
	//[Signal]
	//public delegate void DialogClosedEventHandler(DialogResultGame dialogResult);
	
	/// <summary>
	/// Globale Task Variable als auto implemented Property für Warten auf Buttonklick
	/// </summary>
	public TaskCompletionSource<DialogResultGame> tcsButtonklick { get; set; }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
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
		tcsButtonklick?.TrySetResult(dialogResult);
		
		// set_process_unhandled_key_input(false)
		//EmitSignal(SignalName.DialogClosed, Variant.From(dialogResult));
		//set_deferred("is_open", false)
		//Hide();
		//if _should_unpause:
		//get_tree().paused = false
	}
	
	private Task<DialogResultGame> CloseDialogTask()
	{
		tcsButtonklick = new TaskCompletionSource<DialogResultGame>();
		return tcsButtonklick.Task;
	}

	public async Task<DialogResultGame> ShowDialog()
	{
		GD.Print("Showing dialog");
		Show();
		//await ToSignal(GetTree(), SignalName.DialogClosed);
		return await CloseDialogTask();
	}
}