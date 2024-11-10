using Conspiratio.Lib.Gameplay.Justiz;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Mainmenu : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_button_1_pressed()
	{
		SW.Statisch.Initialisieren();
		GD.Print("Das Spiel startet im Jahr: " + SW.Statisch.StartJahr);
	}
	
	private void _on_button_5_pressed()
	{
		GetTree().Quit();
	}

	// TODO: Kann das nicht in die Lib ausgelagert werden?
	
}