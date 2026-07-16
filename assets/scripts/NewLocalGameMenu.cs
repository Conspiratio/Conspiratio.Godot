using System.IO;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class NewLocalGameMenu : Control
{
	private LineEdit _lineEditGameName;
	private CheckBoxWithSounds _checkBox1Player;
	private CheckBoxWithSounds _checkBoxCheatmodus;
	private CheckBoxWithSounds _checkBoxTestmodus;
	private CheckBoxWithSounds _checkBoxShowDeaths;
	private NewGameManager _newGameManager;

	[Export]
	public NodePath LineEditGameNamePath { get; set; }

	[Export]
	public NodePath CheckBox1PlayerPath { get; set; }

	[Export]
	public NodePath CheckBoxCheatmodusPath { get; set; }

	[Export]
	public NodePath CheckBoxTestmodusPath { get; set; }

	[Export]
	public NodePath CheckBoxShowDeathsPath { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var savegamePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Conspiratio");
		_newGameManager = new NewGameManager(savegamePath);

		_lineEditGameName = GetNode<LineEdit>(LineEditGameNamePath);
		_lineEditGameName.MaxLength = _newGameManager.MaxLengthOfGameName;

		_checkBox1Player = GetNode<CheckBoxWithSounds>(CheckBox1PlayerPath);
		_checkBoxCheatmodus = GetNode<CheckBoxWithSounds>(CheckBoxCheatmodusPath);
		_checkBoxTestmodus = GetNode<CheckBoxWithSounds>(CheckBoxTestmodusPath);
		_checkBoxShowDeaths = GetNode<CheckBoxWithSounds>(CheckBoxShowDeathsPath);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr die Erstellung eines neuen Spiels wirklich abbrechen?") !=
		    DialogResultGame.Yes)
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
		Show();
		SetProcessInput(true);
	}

	private async void _on_button_create_game_pressed()
	{
		_lineEditGameName.Text = _newGameManager.SanitizeName(_lineEditGameName.Text);

		if (!_newGameManager.CreateNewGame(_lineEditGameName.Text, GetSelectedPlayerCount(),
			    _checkBoxCheatmodus.ButtonPressed, _checkBoxShowDeaths.ButtonPressed,
			    _checkBoxTestmodus.ButtonPressed, out string error))
		{
			await SW.UI.ShowText.ShowDialog(error);
			return;
		}

		// TODO: Spielererstellung starten (Migration von SpielerHinzufuegen)
		HideAndDisableInput();
	}

	private void _on_line_edit_game_name_text_submitted(string newText)
	{
		_on_button_create_game_pressed();
	}

	private int GetSelectedPlayerCount()
	{
		if (_checkBox1Player.ButtonGroup.GetPressedButton() is Button pressedButton)
			return int.Parse(pressedButton.Text);

		return 1;
	}
}
