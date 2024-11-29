using System.Diagnostics;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Mainmenu : Control
{
	[Export]
	public NodePath LinkButtonVersionPath { get; set; }

	private Main _main;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var _linkButtonVersion = GetNode<LinkButtonWithSounds>(LinkButtonVersionPath);
		_linkButtonVersion.Text = "Klicken für Changelog - Version " + ProjectSettings.GetSetting("application/config/version");

		_main = GetParent<Main>();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_button_1_pressed()
	{
		_main.AudioStreamPlayerLeftClick.Play();
		SW.Statisch.Initialisieren();
		GD.Print("Das Spiel startet im Jahr: " + SW.Statisch.StartJahr);
	}

	private async void _on_button_help_pressed()
	{
		_main.AudioStreamPlayerLeftClick.Play();
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr unsere Hilfeseite in Eurem Standard-Browser öffnen?",
			    "Auf jeden Fall", "Lieber nicht") != DialogResultGame.Yes)
			return;

		Process.Start( new ProcessStartInfo { FileName = "https://github.com/Conspiratio/Conspiratio.Wiki/wiki", UseShellExecute = true } );
	}

	private async void _on_link_button_version_pressed()
	{
		_main.AudioStreamPlayerLeftClick.Play();
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr den Changelog in Eurem Standard-Browser öffnen?", 
			    "Auf jeden Fall", "Lieber nicht") != DialogResultGame.Yes)
			return;

		Process.Start( new ProcessStartInfo { FileName = "https://github.com/Conspiratio/Conspiratio.Godot/blob/main/CHANGELOG.md", UseShellExecute = true } );
	}
	
	private async void _on_button_5_pressed()
	{
		_main.AudioStreamPlayerLeftClick.Play();
		//var dialogScene = GD.Load<PackedScene>("res://dialogs/YesNoDialog.tscn");
		//var dialog = (YesNoDialog)dialogScene.Instantiate();
		//AddChild(dialog);

		// if (await SW.UI.JaNeinFrage.ShowDialogTextAsync("Wollt Ihr Conspiratio wirklich beenden?",
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Conspiratio wirklich beenden?",
			    "Auf jeden Fall", "Lieber nicht") == DialogResultGame.Yes)
		{
			GetTree().Quit();
		}
	}
}