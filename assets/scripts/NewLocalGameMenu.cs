using System.IO;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Einstellungen;
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
	private OptionButton _optionButtonDifficulty;
	private OptionButton _optionButtonMission;
	private NewGameManager _newGameManager;
	private Main _main;

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

	[Export]
	public NodePath OptionButtonDifficultyPath { get; set; }

	[Export]
	public NodePath OptionButtonMissionPath { get; set; }

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

		_optionButtonDifficulty = GetNode<OptionButton>(OptionButtonDifficultyPath);
		_optionButtonMission = GetNode<OptionButton>(OptionButtonMissionPath);
		_optionButtonDifficulty.ItemSelected += _ => AktualisiereAuftragsliste();
		_optionButtonMission.ItemSelected += _ => AktualisiereAuftragsTooltip();
		AktualisiereAuftragsliste();

		_main = GetParent<Main>();

		// Godot aktiviert die Input-Verarbeitung automatisch, sobald _Input überschrieben ist. Da dieses
		// Menü beim direkten Laden eines Spielstands nie angezeigt wird, muss die Verarbeitung hier
		// ausgeschaltet werden – sonst fängt das versteckte Menü Rechtsklicks im Kontor ab.
		SetProcessInput(false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr die Erstellung eines neuen Spiels wirklich abbrechen?") !=
		    DialogResultGame.Yes)
		{
			SetProcessInput(true);
			return;
		}

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

		// Die in den Optionen gewählte KI-Aggressivität (Prozent) als Vorgabe für dieses Spiel übernehmen
		SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = ClientSettings.KiAggressivitaetProzent;

		// Den gewählten Auftrag (Mission) übernehmen – „Kein Auftrag" bedeutet freies/endloses Spiel.
		SW.Dynamisch.Spielstand.Einstellungen.Auftrag = GetSelectedAuftrag();

		HideAndDisableInput();
		_main.NewPlayerMenu.StartPlayerSetup();
	}

	/// <summary>
	/// Füllt die Auftragsliste passend zur gewählten Schwierigkeit: bei „Keine (freies Spiel)" nur
	/// „Kein Auftrag" (deaktiviert), sonst die Aufträge der jeweiligen Stufe. Die Auftrags-ID jedes
	/// Eintrags ist der <see cref="EnumAuftrag"/>-Wert.
	/// </summary>
	private void AktualisiereAuftragsliste()
	{
		_optionButtonMission.Clear();

		int schwierigkeitId = _optionButtonDifficulty.GetSelectedId(); // 0 = keine, 1/2/3 = leicht/mittel/schwer

		if (schwierigkeitId == 0)
		{
			_optionButtonMission.AddItem("Kein Auftrag", (int)EnumAuftrag.KeinAuftrag);
			_optionButtonMission.Disabled = true;
		}
		else
		{
			_optionButtonMission.Disabled = false;
			var schwierigkeit = (EnumAuftragSchwierigkeit)(schwierigkeitId - 1);
			foreach (var info in AuftragManager.GetAuftraege(schwierigkeit))
				_optionButtonMission.AddItem(info.Name, (int)info.Auftrag);
		}

		_optionButtonMission.Selected = 0;
		AktualisiereAuftragsTooltip();
	}

	/// <summary>Zeigt das Auftragsziel als Tooltip des Auftrags-Auswahlfelds.</summary>
	private void AktualisiereAuftragsTooltip()
	{
		var info = AuftragManager.GetInfo(GetSelectedAuftrag());
		_optionButtonMission.TooltipText = info?.Ziel ?? "Freies, endloses Spiel ohne Siegbedingung.";
	}

	/// <summary>Der aktuell gewählte Auftrag (oder <see cref="EnumAuftrag.KeinAuftrag"/> bei freiem Spiel).</summary>
	private EnumAuftrag GetSelectedAuftrag()
	{
		if (_optionButtonDifficulty.GetSelectedId() == 0)
			return EnumAuftrag.KeinAuftrag;

		return (EnumAuftrag)_optionButtonMission.GetSelectedId();
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
