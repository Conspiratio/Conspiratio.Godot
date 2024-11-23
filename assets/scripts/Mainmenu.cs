using System.Diagnostics;
using Conspiratio.Lib.Allgemein;
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

	private async void _on_button_help_pressed()
	{
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr unsere Hilfeseite in Eurem Standard-Browser öffnen?",
			    "Auf jeden Fall", "Lieber nicht") != DialogResultGame.Yes)
			return;

		Process.Start("https://github.com/Conspiratio/Conspiratio.Wiki/wiki");
	}

	/*
	// Changelog aufrufen
	if (SW.UI.JaNeinFrage.ShowDialogText("Wollt Ihr den Changelog in Eurem Standard-Browser öffnen?", "Auf jeden Fall", "Lieber nicht") != DialogResultGame.Yes)
	return;

	Process.Start("https://github.com/Conspiratio/Conspiratio.WinForms/blob/main/CHANGELOG.md");
	*/
	
	private async void _on_button_5_pressed()
	{
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