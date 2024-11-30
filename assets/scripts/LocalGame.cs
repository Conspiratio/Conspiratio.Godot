using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class LocalGame : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	
	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close")) 
			return;
		
		HideAndDisableInput();
	}
	
	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	public void ShowAndEnableInput()
	{
		Show();
		SetProcessInput(true);
	}
}