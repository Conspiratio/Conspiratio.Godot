using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Main : Control
{
	[Export]
	public NodePath YesNoDialogPath { get; set; }

	[Export]
	public NodePath TextDialogPath { get; set; }

	[Export]
	public NodePath LocalGameDialogPath { get; set; }
	
	[Export]
	public NodePath NewLocalGameMenuPath { get; set; }

	[Export]
	public NodePath NewPlayerMenuPath { get; set; }

	[Export]
	public NodePath KontorPath { get; set; }

	[Export]
	public NodePath AbrechnungDialogPath { get; set; }

	[Export]
	public NodePath StadtPath { get; set; }

	public LocalGameDialog LocalGameDialog;
	public NewLocalGameMenu NewLocalGameMenu;
	public NewPlayerMenu NewPlayerMenu;
	public Kontor Kontor;
	public AbrechnungDialog AbrechnungDialog;
	public Stadt Stadt;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var yesNoDialog = GetNode<YesNoDialog>(YesNoDialogPath);
		var textDialog = GetNode<TextDialog>(TextDialogPath);
		LocalGameDialog = GetNode<LocalGameDialog>(LocalGameDialogPath);
		NewLocalGameMenu = GetNode<NewLocalGameMenu>(NewLocalGameMenuPath);
		NewPlayerMenu = GetNode<NewPlayerMenu>(NewPlayerMenuPath);
		Kontor = GetNode<Kontor>(KontorPath);
		AbrechnungDialog = GetNode<AbrechnungDialog>(AbrechnungDialogPath);
		Stadt = GetNode<Stadt>(StadtPath);

		// TODO: add missing dialogs
		SW.UI.Initialisieren(yesNoDialog, textDialog, null, null, null, null, null, null, null);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}