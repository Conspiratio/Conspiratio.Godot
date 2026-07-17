using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class NewPlayerMenu : Control
{
	private Label _labelTitle;
	private LineEdit _lineEditPlayerName;
	private CheckBoxWithSounds _checkBoxMale;
	private CheckBoxWithSounds _checkBoxFemale;
	private CheckBoxWithSounds _checkBoxReligion1;
	private CheckBoxWithSounds _checkBoxReligion2;
	private GridContainer _gridContainerBanner;
	private OptionButton _optionButtonCity;
	private OptionButton _optionButtonResource;
	private Label _labelCityCost;
	private Label _labelResourceCost;

	private PlayerSetupManager _playerSetupManager;
	private Main _main;
	private int _zufallsStadtId;
	private bool _stadtGewaehlt;

	[Export]
	public NodePath LabelTitlePath { get; set; }

	[Export]
	public NodePath LineEditPlayerNamePath { get; set; }

	[Export]
	public NodePath CheckBoxMalePath { get; set; }

	[Export]
	public NodePath CheckBoxFemalePath { get; set; }

	[Export]
	public NodePath CheckBoxReligion1Path { get; set; }

	[Export]
	public NodePath CheckBoxReligion2Path { get; set; }

	[Export]
	public NodePath GridContainerBannerPath { get; set; }

	[Export]
	public NodePath OptionButtonCityPath { get; set; }

	[Export]
	public NodePath OptionButtonResourcePath { get; set; }

	[Export]
	public NodePath LabelCityCostPath { get; set; }

	[Export]
	public NodePath LabelResourceCostPath { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelTitle = GetNode<Label>(LabelTitlePath);
		_lineEditPlayerName = GetNode<LineEdit>(LineEditPlayerNamePath);
		_checkBoxMale = GetNode<CheckBoxWithSounds>(CheckBoxMalePath);
		_checkBoxFemale = GetNode<CheckBoxWithSounds>(CheckBoxFemalePath);
		_checkBoxReligion1 = GetNode<CheckBoxWithSounds>(CheckBoxReligion1Path);
		_checkBoxReligion2 = GetNode<CheckBoxWithSounds>(CheckBoxReligion2Path);
		_gridContainerBanner = GetNode<GridContainer>(GridContainerBannerPath);
		_optionButtonCity = GetNode<OptionButton>(OptionButtonCityPath);
		_optionButtonResource = GetNode<OptionButton>(OptionButtonResourcePath);
		_labelCityCost = GetNode<Label>(LabelCityCostPath);
		_labelResourceCost = GetNode<Label>(LabelResourceCostPath);

		_main = GetParent<Main>();

		AssignButtonGroups();
		SetProcessInput(false);
	}

	/// <summary>
	/// Weist die exklusiven Gruppen (nur eine Option aktiv) im Code zu, da im Godot-Editor
	/// gespeicherte ButtonGroup-Subressourcen beim erneuten Speichern pro Node dupliziert werden.
	/// </summary>
	private void AssignButtonGroups()
	{
		var genderGroup = new ButtonGroup();
		_checkBoxMale.ButtonGroup = genderGroup;
		_checkBoxFemale.ButtonGroup = genderGroup;

		var religionGroup = new ButtonGroup();
		_checkBoxReligion1.ButtonGroup = religionGroup;
		_checkBoxReligion2.ButtonGroup = religionGroup;

		var bannerGroup = new ButtonGroup();
		for (int i = 0; i < _gridContainerBanner.GetChildCount(); i++)
			_gridContainerBanner.GetChild<CheckBoxWithSounds>(i).ButtonGroup = bannerGroup;
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

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr die Spielerstellung komplett abbrechen?") !=
		    DialogResultGame.Yes)
		{
			SetProcessInput(true);
			return;
		}

		_playerSetupManager.Beende();
		Hide();
	}

	/// <summary>
	/// Startet die Spielererstellung für alle aktiven Spieler des neuen Spiels.
	/// Darf erst aufgerufen werden, wenn die Spielwelt initialisiert wurde (NewGameManager.CreateNewGame).
	/// </summary>
	public void StartPlayerSetup()
	{
		_playerSetupManager = new PlayerSetupManager();
		_playerSetupManager.Starte();

		_checkBoxReligion1.Text = SW.Statisch.GetReligionsNamenX(SW.Statisch.GetRelKathID());
		_checkBoxReligion2.Text = SW.Statisch.GetReligionsNamenX(SW.Statisch.GetRelEvanID());
		_labelCityCost.Text = "Zufällig vorausgewählt und kostenlos, eine andere Stadt kostet " + SW.Statisch.GetNSPStadtwahlKosten() + " Taler";
		_labelResourceCost.Text = "Gezielte Wahl kostet " + SW.Statisch.GetNSPRohwahlKosten() + " Taler, zufällig ist kostenlos";
		_lineEditPlayerName.MaxLength = SW.Statisch.GetMaxNameLength();

		PopulateCityOptions();
		PrepareNextPlayer();

		Show();
		SetProcessInput(true);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void PopulateCityOptions()
	{
		_optionButtonCity.Clear();

		for (int stadtId = 1; stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
			_optionButtonCity.AddItem(SW.Dynamisch.GetStadtwithID(stadtId).GetGebietsName(), stadtId);
	}

	private void SelectCity(int stadtId)
	{
		for (int i = 0; i < _optionButtonCity.ItemCount; i++)
		{
			if (_optionButtonCity.GetItemId(i) != stadtId)
				continue;

			_optionButtonCity.Select(i);
			return;
		}
	}

	private void UpdateResourceOptions()
	{
		_optionButtonResource.Clear();
		_optionButtonResource.AddItem("Zufällig", 0);

		int stadtId = _optionButtonCity.GetSelectedId();

		for (int platz = 1; platz <= 2; platz++)
		{
			int rohstoffId = SW.Dynamisch.GetStadtwithID(stadtId).GetSingleRohstoff(platz);
			_optionButtonResource.AddItem(SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName(), platz);
		}

		_optionButtonResource.Select(0);
	}

	private void PrepareNextPlayer()
	{
		_labelTitle.Text = "Spieler " + (_playerSetupManager.AnzahlAngelegteSpieler + 1) + " von " +
		                   SW.Dynamisch.GetAktivSpielerAnzahl() + " erstellen";

		_lineEditPlayerName.Clear();
		_lineEditPlayerName.GrabFocus();

		_checkBoxMale.ButtonPressed = true;
		_checkBoxReligion1.ButtonPressed = true;

		// Heimatstadt kostenlos vorauswürfeln; erst ein manueller Wechsel auf eine andere Stadt kostet Taler
		_zufallsStadtId = _playerSetupManager.WuerfleZufaelligeStadt();
		_stadtGewaehlt = false;
		SelectCity(_zufallsStadtId);
		UpdateResourceOptions();

		// Banner-Auswahl zurücksetzen und bereits vergebene Banner ausblenden
		for (int i = 0; i < _gridContainerBanner.GetChildCount(); i++)
		{
			var checkBoxBanner = _gridContainerBanner.GetChild<CheckBoxWithSounds>(i);
			checkBoxBanner.SetPressedNoSignal(false);
			checkBoxBanner.Visible = !_playerSetupManager.IstBannerVergeben(i + 1);
		}
	}

	private int GetSelectedBanner()
	{
		for (int i = 0; i < _gridContainerBanner.GetChildCount(); i++)
		{
			if (_gridContainerBanner.GetChild<CheckBoxWithSounds>(i).ButtonPressed)
				return i + 1;
		}

		return 0;
	}

	private void _on_option_button_city_item_selected(long index)
	{
		_stadtGewaehlt = _optionButtonCity.GetSelectedId() != _zufallsStadtId;
		UpdateResourceOptions();
	}

	private void _on_line_edit_player_name_text_submitted(string newText)
	{
		_on_button_create_player_pressed();
	}

	private async void _on_button_create_player_pressed()
	{
		SetProcessInput(false);

		if (!_playerSetupManager.ValidateName(_lineEditPlayerName.Text, out string error))
		{
			await SW.UI.ShowText.ShowDialog(error);
			SetProcessInput(true);
			return;
		}

		int banner = GetSelectedBanner();

		if (banner == 0)
		{
			await SW.UI.ShowText.ShowDialog("Bitte wählt ein Banner");
			SetProcessInput(true);
			return;
		}

		int religionId = _checkBoxReligion1.ButtonPressed ? SW.Statisch.GetRelKathID() : SW.Statisch.GetRelEvanID();

		var ergebnis = _playerSetupManager.ErstelleSpieler(_lineEditPlayerName.Text, _checkBoxMale.ButtonPressed,
			banner, religionId, _optionButtonCity.GetSelectedId(), _stadtGewaehlt, _optionButtonResource.GetSelectedId());

		await SW.UI.ShowText.ShowDialog(_lineEditPlayerName.Text + " wurde erstellt.\nHeimatstadt: " +
		                                SW.Dynamisch.GetStadtwithID(ergebnis.StadtId).GetGebietsName() + "\nRohstoff: " +
		                                SW.Dynamisch.GetRohstoffwithID(ergebnis.RohstoffId).GetRohName());

		if (_playerSetupManager.AlleSpielerAngelegt)
		{
			_playerSetupManager.Beende();

			HideAndDisableInput();
			_main.Kontor.StartGame();
			return;
		}

		PrepareNextPlayer();
		SetProcessInput(true);
	}
}
