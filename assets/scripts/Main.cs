using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Main : Control
{
	[Export]
	public NodePath YesNoDialogPath { get; set; }
	
	[Export]
	public NodePath AudioStreamPlayerLeftClickPath { get; set; }
	
	[Export]
	public NodePath AudioStreamPlayerRightClickPath { get; set; }
	
	public AudioStreamPlayer AudioStreamPlayerLeftClick;
	public AudioStreamPlayer AudioStreamPlayerRightClick;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var yesNoDialog = GetNode<YesNoDialog>(YesNoDialogPath);
		AudioStreamPlayerLeftClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerLeftClickPath);
		AudioStreamPlayerRightClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerRightClickPath);
		
		// TODO: add missing dialogs
		SW.UI.Initialisieren(yesNoDialog, null, null, null, null, null, null, null, null);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}