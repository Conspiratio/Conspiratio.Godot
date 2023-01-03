using Godot;
using System;

public partial class title : Node
{
	// Declare member variables here. Examples:
	// private int a = 2;
	// private string b = "text";
	private Resource cursorWait = ResourceLoader.Load("res://assets/cursor/CurWait-32x32x32.png");
	private Resource cursorDefault = ResourceLoader.Load("res://assets/cursor/CurSword-32x32x24.png");

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Input.SetCustomMouseCursor(cursorWait);
	}

	/*
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(float delta)
	{

	}
	*/

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionPressed("ui_next_or_close"))
		{
			GD.Print("Process ui_next_or_close Action");
			Input.SetCustomMouseCursor(cursorDefault);
			GetTree().ChangeSceneToFile("res://Mainmenu.tscn");
		}

		/*
		// Mouse in viewport coordinates.
		if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.ButtonIndex == 2)  // right click
		{
			GD.Print("Input Event Mouse Right Click");
			//GetTree().ChangeSceneToFile("res://Mainmenu.tscn");
		}
		*/
	}
}
