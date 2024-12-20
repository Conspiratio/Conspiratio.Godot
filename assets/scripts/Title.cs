using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Title : Node
{
	private Resource _cursorWait = ResourceLoader.Load("res://assets/cursor/CurWait-32x32x32.png");
	private Resource _cursorDefault = ResourceLoader.Load("res://assets/cursor/CurSword-32x32x24.png");

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Input.SetCustomMouseCursor(_cursorWait);
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
			Input.SetCustomMouseCursor(_cursorDefault);
			GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
		}
	}
}