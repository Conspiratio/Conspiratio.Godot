using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Title : Node
{
	[Export]
	public string MainScenePath { get; set; }
	
	[Export]
	public string CursorWaitPath { get; set; }
	
	[Export]
	public string CursorDefaultPath { get; set; }

	private Resource _cursorWait;
	private Resource _cursorDefault;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_cursorWait = ResourceLoader.Load(CursorWaitPath);
		_cursorDefault = ResourceLoader.Load(CursorDefaultPath);
		
		Input.SetCustomMouseCursor(_cursorWait);
		SoundManager.Instance.PlayIntro();
	}
	
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close")) 
			return;
		
		SoundManager.Instance.PlayRightClick();
		Input.SetCustomMouseCursor(_cursorDefault);
		GetTree().ChangeSceneToFile(MainScenePath);
	}
}