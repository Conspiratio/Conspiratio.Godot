using System.Diagnostics;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;
using JetBrains.Annotations;

namespace Conspiratio.Godot.assets.scripts;

public partial class Mainmenu : Control
{
	[Export]
	public NodePath LinkButtonVersionPath { get; set; }

	private Main _main;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var linkButtonVersion = GetNode<LinkButtonWithSounds>(LinkButtonVersionPath);
		linkButtonVersion.Text = "Klicken für Changelog - Version " + ProjectSettings.GetSetting("application/config/version");

		_main = GetParent<Main>();

		// Im Hauptmenü lief bisher überhaupt keine Musik: Der Titelbildschirm startet die Introfanfare,
		// und weil Intro und Outro einmalige Stücke sind, verstummt die Musik an deren Ende – die erste
		// Hintergrundmusik kam erst im Kontor. Das Menü übernimmt das jetzt selbst.
		VisibilityChanged += StarteMenuemusik;
		SoundManager.Instance.EinmaligesStueckBeendet += StarteMenuemusik;

		StarteMenuemusik();

		BieteCrashMeldungAn();
	}

	public override void _ExitTree()
	{
		// Der SoundManager ist ein Autoload und überlebt diese Szene; ohne das Abmelden hielte sein
		// Ereignis das entladene Menü am Leben und riefe später auf ein freigegebenes Objekt.
		if (SoundManager.Instance != null)
			SoundManager.Instance.EinmaligesStueckBeendet -= StarteMenuemusik;
	}

	/// <summary>
	/// Schaltet auf die Hintergrundmusik um, sobald das Menü sichtbar ist – aber erst, wenn die
	/// Introfanfare ausgespielt hat: Sie sofort zu überblenden wäre schlechter als die Stille, die es
	/// zu beheben gilt. Nach dem Ende der Fanfare ruft der SoundManager hier noch einmal an.
	/// Die Sichtbarkeitsprüfung ist nötig, weil das Ereignis auch eintrifft, während längst ein Spiel
	/// läuft – dort bestimmt der jeweilige Bildschirm die Musik (Kirche, Hinterzimmer, Kampf).
	/// </summary>
	private void StarteMenuemusik()
	{
		if (!Visible || SoundManager.Instance.SpieltEinmaligesStueck())
			return;

		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
	}

	/// <summary>
	/// Wurde beim letzten Lauf ein Absturz erfasst, bietet das Hauptmenü einmalig an, ihn zu melden.
	/// Bei Zustimmung öffnet sich der Melde-Dialog im Fehlermodus; sonst wird der Absturz quittiert.
	/// </summary>
	private async void BieteCrashMeldungAn()
	{
		if (DiagnoseManager.Instance == null || !DiagnoseManager.Instance.LiegtCrashVor())
			return;

		if (await SW.UI.YesNoQuestion.ShowDialogText(
			    "Beim letzten Start ist ein Fehler aufgetreten. Möchtet Ihr ihn an die Entwickler melden?",
			    "Ja, melden", "Nein, verwerfen") == DialogResultGame.Yes)
			_main.DiagnoseDialog.ShowDialog(true);
		else
			DiagnoseManager.Instance.CrashQuittieren();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_button_local_game_pressed()
	{
		_main.LocalGameDialog.ShowAndEnableInput();
		
		SW.Statisch.Initialisieren();
		GD.Print("Das Spiel startet im Jahr: " + SW.Statisch.StartJahr);
	}

	private async void _on_button_help_pressed()
	{
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr unsere Hilfeseite in Eurem Standard-Browser öffnen?",
			    "Auf jeden Fall", "Lieber nicht") != DialogResultGame.Yes)
			return;

		Process.Start( new ProcessStartInfo { FileName = "https://github.com/Conspiratio/Conspiratio.Wiki/wiki", UseShellExecute = true } );
	}
	
	private void _on_button_profile_pressed()
	{
		_main.ProfilDialog.ShowDialog();
	}

	private void _on_button_options_pressed()
	{
		_main.OptionenDialog.ShowDialog();
	}

	private void _on_button_credits_pressed()
	{
		_main.CreditsDialog.ShowDialog();
	}

	private void _on_button_bestenliste_pressed()
	{
		_main.BestenlisteDialog.ShowDialog();
	}

	private void _on_button_feedback_pressed()
	{
		_main.DiagnoseDialog.ShowDialog();
	}

	private async void _on_link_button_version_pressed()
	{
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr den Changelog in Eurem Standard-Browser öffnen?", 
			    "Auf jeden Fall", "Lieber nicht") != DialogResultGame.Yes)
			return;

		Process.Start( new ProcessStartInfo { FileName = "https://github.com/Conspiratio/Conspiratio.Godot/blob/main/CHANGELOG.md", UseShellExecute = true } );
	}

	[PublicAPI]
	public async void _on_button_exit_pressed()
	{
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Conspiratio wirklich beenden?",
			    "Auf jeden Fall", "Lieber nicht") == DialogResultGame.Yes)
		{
			GetTree().Quit();
		}
	}
}