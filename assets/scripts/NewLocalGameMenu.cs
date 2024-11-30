using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class NewLocalGameMenu : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// TODO: Set Maxlength of LineEdit for Game Name
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	
	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close")) 
			return;

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr die Erstellung eines neuen Spiels wirklich abbrechen?") !=
		    DialogResultGame.Yes)
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