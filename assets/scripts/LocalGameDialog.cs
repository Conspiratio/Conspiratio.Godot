using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class LocalGameDialog : Control
{
	private Main _main;
	private Button _buttonContinueGame;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_main = GetParent<Main>();
		_buttonContinueGame = GetNode<Button>("Panel/NinePatchRect/ButtonContinueGame");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
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
		_buttonContinueGame.Disabled = string.IsNullOrEmpty(ClientSettings.LetzterSpielstand);

		Show();
		SetProcessInput(true);
	}

	public void _on_button_start_game_pressed()
	{
		_main.NewLocalGameMenu.ShowAndEnableInput();
		HideAndDisableInput();
	}

	private async void _on_button_load_game_pressed()
	{
		HideAndDisableInput();

		if (await _main.LoadGameDialog.ZeigeUndLade())
			_main.Kontor.ContinueLoadedGame();
		else
			ShowAndEnableInput();  // Abbruch: zurück ins lokale Spielmenü
	}

	private async void _on_button_continue_game_pressed()
	{
		SetProcessInput(false);

		var speicherManager = new SpeicherManager(ClientSettings.SavegamePath);

		if (speicherManager.Laden(ClientSettings.LetzterSpielstand, out string fehler))
		{
			// Den Dialog schließen, bevor die Ladebestätigung erscheint, sonst bleibt er darunter sichtbar
			HideAndDisableInput();

			await SW.UI.ShowText.ShowDialog("Ladevorgang beendet!");
			_main.Kontor.ContinueLoadedGame();
			return;
		}

		await SW.UI.ShowText.ShowDialog(fehler);

		if (Visible)
			SetProcessInput(true);
	}
}
