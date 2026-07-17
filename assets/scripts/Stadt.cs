using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Niederlassung;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Stadt : Control
{
	private OptionButton _optionButtonCity;
	private Label _labelTaler;
	private GridContainer _gridContainerResources;
	private Label _labelKarawane;

	private readonly OptionButton[] _slotTaetigkeit = new OptionButton[2];
	private readonly OptionButton[] _slotProdukt = new OptionButton[2];
	private readonly SpinBox[] _slotMenge = new SpinBox[2];
	private readonly SpinBox[] _slotStaetten = new SpinBox[2];
	private readonly OptionButton[] _slotZielstadt = new OptionButton[2];
	private readonly Label[] _slotKosten = new Label[2];

	private Main _main;
	private HandelsManager _handelsManager;
	private PackedScene _buttonWithSoundsScene;
	private int _stadtId = 1;
	private bool _refreshing;

	[Export]
	public NodePath OptionButtonCityPath { get; set; }

	[Export]
	public NodePath LabelTalerPath { get; set; }

	[Export]
	public NodePath GridContainerResourcesPath { get; set; }

	[Export]
	public NodePath HBoxSlot0Path { get; set; }

	[Export]
	public NodePath HBoxSlot1Path { get; set; }

	[Export]
	public NodePath LabelKarawanePath { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_optionButtonCity = GetNode<OptionButton>(OptionButtonCityPath);
		_labelTaler = GetNode<Label>(LabelTalerPath);
		_gridContainerResources = GetNode<GridContainer>(GridContainerResourcesPath);
		_labelKarawane = GetNode<Label>(LabelKarawanePath);

		_buttonWithSoundsScene = GD.Load<PackedScene>("res://scenes/controls/ButtonWithSounds.tscn");
		_main = GetParent<Main>();

		InitializeSlotControls(0, GetNode<Control>(HBoxSlot0Path));
		InitializeSlotControls(1, GetNode<Control>(HBoxSlot1Path));

		SetProcessInput(false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		managers.SoundManager.Instance.PlayRightClick();
		CloseStadt();
	}

	/// <summary>
	/// Öffnet die Stadtansicht mit der Heimatstadt des aktiven Spielers.
	/// </summary>
	public void ShowStadt()
	{
		_handelsManager = new HandelsManager();
		_stadtId = ErmittleHeimatstadt();

		PopulateCityOptions();
		Refresh();

		Show();
		SetProcessInput(true);
	}

	private void CloseStadt()
	{
		Hide();
		SetProcessInput(false);
		_main.Kontor.ReturnFromStadt();
	}

	private static int ErmittleHeimatstadt()
	{
		for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
		{
			if (SW.Dynamisch.GetAktHum().GetSpielerHatHausVonStadtAnArraystelle(stadtId).GetHausID() != 0)
				return stadtId;
		}

		return SW.Statisch.GetMinStadtID();
	}

	private void InitializeSlotControls(int slot, Control container)
	{
		_slotTaetigkeit[slot] = container.GetNode<OptionButton>("OptionButtonTaetigkeit");
		_slotProdukt[slot] = container.GetNode<OptionButton>("OptionButtonProdukt");
		_slotMenge[slot] = container.GetNode<SpinBox>("SpinBoxMenge");
		_slotStaetten[slot] = container.GetNode<SpinBox>("SpinBoxStaetten");
		_slotZielstadt[slot] = container.GetNode<OptionButton>("OptionButtonZielstadt");
		_slotKosten[slot] = container.GetNode<Label>("LabelKosten");

		_slotTaetigkeit[slot].Clear();
		_slotTaetigkeit[slot].AddItem("Kein Auftrag", (int)EnumProduktionsslotAktionsart.KeinAuftrag);
		_slotTaetigkeit[slot].AddItem("Produzieren", (int)EnumProduktionsslotAktionsart.Produzieren);
		_slotTaetigkeit[slot].AddItem("Verkaufen", (int)EnumProduktionsslotAktionsart.Verkaufen);
		_slotTaetigkeit[slot].AddItem("Permanenter Verkauf", (int)EnumProduktionsslotAktionsart.PermanentVerkaufen);

		_slotTaetigkeit[slot].ItemSelected += index => OnSlotTaetigkeitSelected(slot);
		_slotProdukt[slot].ItemSelected += index => OnSlotProduktSelected(slot);
		_slotMenge[slot].ValueChanged += value => OnSlotMengeChanged(slot, (int)value);
		_slotStaetten[slot].ValueChanged += value => OnSlotStaettenChanged(slot, (int)value);
		_slotZielstadt[slot].ItemSelected += index => OnSlotZielstadtSelected(slot);
	}

	private void PopulateCityOptions()
	{
		_optionButtonCity.Clear();

		for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
			_optionButtonCity.AddItem(SW.Dynamisch.GetStadtwithID(stadtId).GetGebietsName(), stadtId);

		SelectOptionById(_optionButtonCity, _stadtId);
	}

	private static void SelectOptionById(OptionButton optionButton, int id)
	{
		for (int i = 0; i < optionButton.ItemCount; i++)
		{
			if (optionButton.GetItemId(i) != id)
				continue;

			optionButton.Select(i);
			return;
		}
	}

	#region Refresh

	private void Refresh()
	{
		_refreshing = true;

		_labelTaler.Text = SW.Dynamisch.GetAktHum().GetTalerFormatiert() + " Taler";

		RefreshResourceRows();
		RefreshSlot(0);
		RefreshSlot(1);
		RefreshKarawane();

		_refreshing = false;
	}

	private void RefreshResourceRows()
	{
		foreach (Node child in _gridContainerResources.GetChildren())
		{
			_gridContainerResources.RemoveChild(child);
			child.QueueFree();
		}

		AddHeaderLabel("Rohstoff");
		AddHeaderLabel("Preis");
		AddHeaderLabel("Lager");
		AddHeaderLabel("Menge");
		AddHeaderLabel("");
		AddHeaderLabel("");
		AddHeaderLabel("Werkstätte");

		for (int werkstattNr = 1; werkstattNr <= SW.Statisch.GetMaxWerkstaettenProStadt(); werkstattNr++)
		{
			int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, werkstattNr);

			if (rohstoffId == 0)
				continue;

			bool hatRecht = _handelsManager.HatRohstoffrecht(_stadtId, werkstattNr);
			bool hatWerkstatt = _handelsManager.HatWerkstatt(_stadtId, werkstattNr);

			_gridContainerResources.AddChild(new Label { Text = SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() });
			_gridContainerResources.AddChild(new Label { Text = SW.Dynamisch.GetStadtwithID(_stadtId).GetRohstoffPreisVonIDX(rohstoffId).ToString() });
			_gridContainerResources.AddChild(new Label
			{
				Text = hatWerkstatt ? _handelsManager.GetLagerbestand(_stadtId, rohstoffId).ToString() : "—",
				TooltipText = hatWerkstatt ? _handelsManager.GetLagerplatzInfo(_stadtId, werkstattNr) : ""
			});

			var spinBoxMenge = new SpinBox
			{
				MinValue = 0,
				MaxValue = SW.Statisch.GetMaxAnzahlVonEinemRohstoff(),
				Step = 1,
				Value = 10,
				CustomMinimumSize = new Vector2(100, 0),
				Editable = hatWerkstatt
			};
			_gridContainerResources.AddChild(spinBoxMenge);

			int rohstoffIdKopie = rohstoffId;

			var buttonKaufen = CreateButton("Kaufen");
			buttonKaufen.Disabled = !hatWerkstatt;
			buttonKaufen.Pressed += () => OnRohstoffKaufenPressed(rohstoffIdKopie, spinBoxMenge);
			_gridContainerResources.AddChild(buttonKaufen);

			var buttonVerkaufen = CreateButton("Verkaufen");
			buttonVerkaufen.Disabled = !hatWerkstatt;
			buttonVerkaufen.Pressed += () => OnRohstoffVerkaufenPressed(rohstoffIdKopie, spinBoxMenge);
			_gridContainerResources.AddChild(buttonVerkaufen);

			int werkstattNrKopie = werkstattNr;
			var buttonWerkstatt = CreateButton(hatWerkstatt
				? "Verkaufen (" + _handelsManager.GetWerkstattVerkaufspreis(_stadtId, werkstattNr).ToStringGeld() + ")"
				: "Kaufen (" + _handelsManager.GetWerkstattKaufpreis(_stadtId, werkstattNr).ToStringGeld() + ")");
			buttonWerkstatt.Disabled = !hatRecht;
			buttonWerkstatt.TooltipText = hatRecht ? "" : "Euch fehlt das Rohstoffrecht für diesen Rohstoff";
			buttonWerkstatt.Pressed += () => OnWerkstattPressed(werkstattNrKopie);
			_gridContainerResources.AddChild(buttonWerkstatt);
		}
	}

	private void AddHeaderLabel(string text)
	{
		_gridContainerResources.AddChild(new Label { Text = text });
	}

	private ButtonWithSounds CreateButton(string text)
	{
		var button = _buttonWithSoundsScene.Instantiate<ButtonWithSounds>();
		button.Text = text;
		return button;
	}

	private void RefreshSlot(int slot)
	{
		var aktionsart = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();

		SelectOptionById(_slotTaetigkeit[slot], (int)aktionsart);

		bool produzieren = aktionsart == EnumProduktionsslotAktionsart.Produzieren;
		bool verkaufen = aktionsart == EnumProduktionsslotAktionsart.Verkaufen || aktionsart == EnumProduktionsslotAktionsart.PermanentVerkaufen;

		_slotProdukt[slot].Visible = produzieren || verkaufen;
		_slotMenge[slot].Visible = produzieren || verkaufen;
		_slotStaetten[slot].Visible = produzieren;
		_slotZielstadt[slot].Visible = verkaufen;
		_slotKosten[slot].Visible = produzieren || verkaufen;

		var produktionsslot = _handelsManager.GetProduktionsslot(_stadtId, slot);

		if (produzieren)
		{
			int rohstoffId = _handelsManager.KorrigiereProduktionsRohstoff(_stadtId, slot);

			_slotProdukt[slot].Clear();

			for (int werkstattNr = 1; werkstattNr <= SW.Statisch.GetMaxWerkstaettenProStadt(); werkstattNr++)
			{
				if (_handelsManager.HatRohstoffrecht(_stadtId, werkstattNr) && _handelsManager.HatWerkstatt(_stadtId, werkstattNr))
				{
					int rohId = _handelsManager.RohstoffIdAnPlatz(_stadtId, werkstattNr);
					_slotProdukt[slot].AddItem(SW.Dynamisch.GetRohstoffwithID(rohId).GetRohName(), rohId);
				}
			}

			if (rohstoffId == 0)
				_slotProdukt[slot].AddItem("Keine Werkstätte", 0);

			SelectOptionById(_slotProdukt[slot], rohstoffId);

			_slotMenge[slot].MaxValue = SW.Statisch.GetMaxArbeiterAnzahl();
			_slotMenge[slot].SetValueNoSignal(produktionsslot.GetProduktionArbeiter());
			_slotMenge[slot].TooltipText = "Anzahl Arbeiter";
			_slotMenge[slot].Editable = rohstoffId != 0;

			_slotStaetten[slot].MaxValue = 99;
			_slotStaetten[slot].SetValueNoSignal(produktionsslot.GetProduktionStaetten());
			_slotStaetten[slot].TooltipText = "Anzahl Produktionsstätten";
			_slotStaetten[slot].Editable = rohstoffId != 0;
		}
		else if (verkaufen)
		{
			_handelsManager.KorrigiereVerkaufsEinstellungen(_stadtId, slot);

			_slotProdukt[slot].Clear();

			for (int werkstattNr = 1; werkstattNr <= SW.Statisch.GetMaxWerkstaettenProStadt(); werkstattNr++)
			{
				int rohId = _handelsManager.RohstoffIdAnPlatz(_stadtId, werkstattNr);

				if (rohId != 0)
					_slotProdukt[slot].AddItem(SW.Dynamisch.GetRohstoffwithID(rohId).GetRohName(), rohId);
			}

			SelectOptionById(_slotProdukt[slot], produktionsslot.GetVerkaufRohstoff());

			_slotMenge[slot].MaxValue = SW.Statisch.GetMaxAnzahlVonEinemRohstoff();
			_slotMenge[slot].SetValueNoSignal(produktionsslot.GetVerkaufAnzahl());
			_slotMenge[slot].TooltipText = "Verkaufsanzahl (wird aus dem Lager reserviert)";
			_slotMenge[slot].Editable = true;

			_slotZielstadt[slot].Clear();

			for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
			{
				if (stadtId != _stadtId)
					_slotZielstadt[slot].AddItem(SW.Dynamisch.GetStadtwithID(stadtId).GetGebietsName(), stadtId);
			}

			SelectOptionById(_slotZielstadt[slot], produktionsslot.GetVerkaufStadt());
		}

		_slotKosten[slot].Text = "Kosten: " + _handelsManager.BerechneKosten(_stadtId, slot).ToStringGeld();
	}

	private void RefreshKarawane()
	{
		var karawane = _handelsManager.GetKarawane(_stadtId);
		_labelKarawane.Text = "Karawane: " + karawane.Beschreibung + " (Fixpreis " + karawane.Fixpreis + ", je 100 Stück " +
		                      karawane.PreisProStueck + ", Verlässlichkeit " + karawane.Verlaesslichkeit + "%, Sicherheit " +
		                      karawane.Sicherheit + "%)";
	}

	#endregion

	#region Handler

	private void _on_option_button_city_item_selected(long index)
	{
		_stadtId = _optionButtonCity.GetSelectedId();
		Refresh();
	}

	private void _on_button_karawane_pressed()
	{
		_handelsManager.NaechsteKarawane(_stadtId);
		_refreshing = true;
		RefreshKarawane();
		RefreshSlot(0);
		RefreshSlot(1);
		_refreshing = false;
	}

	private void _on_button_back_pressed()
	{
		CloseStadt();
	}

	private async void OnRohstoffKaufenPressed(int rohstoffId, SpinBox spinBoxMenge)
	{
		SetProcessInput(false);

		int gekauft = _handelsManager.KaufeRohstoff(_stadtId, rohstoffId, (int)spinBoxMenge.Value, out string fehler);

		if (fehler != "")
			await SW.UI.ShowText.ShowDialog(fehler);
		else if (gekauft > 0)
			managers.SoundManager.Instance.PlayCoins();

		Refresh();

		if (Visible)
			SetProcessInput(true);
	}

	private void OnRohstoffVerkaufenPressed(int rohstoffId, SpinBox spinBoxMenge)
	{
		int erloes = _handelsManager.VerkaufeRohstoff(_stadtId, rohstoffId, (int)spinBoxMenge.Value);

		if (erloes > 0)
			managers.SoundManager.Instance.PlayCoins();

		Refresh();
	}

	private async void OnWerkstattPressed(int werkstattNr)
	{
		SetProcessInput(false);

		int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, werkstattNr);
		string rohstoffName = SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName();

		if (_handelsManager.HatWerkstatt(_stadtId, werkstattNr))
		{
			int verkaufspreis = _handelsManager.GetWerkstattVerkaufspreis(_stadtId, werkstattNr);

			if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Eure Werkstätte für " + verkaufspreis.ToStringGeld() + "\nverkaufen?") == DialogResultGame.Yes)
			{
				_handelsManager.VerkaufeWerkstatt(_stadtId, werkstattNr);
				managers.SoundManager.Instance.PlayCoins();
			}
		}
		else
		{
			int kaufpreis = _handelsManager.GetWerkstattKaufpreis(_stadtId, werkstattNr);

			if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr für " + kaufpreis.ToStringGeld() + " in " +
			                                             SW.Dynamisch.GetStadtwithID(_stadtId).GetGebietsName() + " eine Werkstätte für eine\n" +
			                                             rohstoffName + "-Produktion kaufen?") == DialogResultGame.Yes)
			{
				if (_handelsManager.KaufeWerkstatt(_stadtId, werkstattNr, out string fehler))
					managers.SoundManager.Instance.PlayCoins();
				else
					await SW.UI.ShowText.ShowDialog(fehler);
			}
		}

		Refresh();

		if (Visible)
			SetProcessInput(true);
	}

	private void OnSlotTaetigkeitSelected(int slot)
	{
		if (_refreshing)
			return;

		_handelsManager.SetzeTaetigkeit(_stadtId, slot, (EnumProduktionsslotAktionsart)_slotTaetigkeit[slot].GetSelectedId());
		Refresh();
	}

	private void OnSlotProduktSelected(int slot)
	{
		if (_refreshing)
			return;

		int rohstoffId = _slotProdukt[slot].GetSelectedId();
		var aktionsart = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();

		if (aktionsart == EnumProduktionsslotAktionsart.Produzieren)
			_handelsManager.SetzeProduktionsRohstoff(_stadtId, slot, rohstoffId);
		else
			_handelsManager.SetzeVerkaufsRohstoff(_stadtId, slot, rohstoffId);

		Refresh();
	}

	private void OnSlotMengeChanged(int slot, int wert)
	{
		if (_refreshing)
			return;

		var aktionsart = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();

		if (aktionsart == EnumProduktionsslotAktionsart.Produzieren)
			_handelsManager.SetzeProduktionsArbeiter(_stadtId, slot, wert);
		else
			_handelsManager.SetzeVerkaufsAnzahl(_stadtId, slot, wert);

		Refresh();
	}

	private void OnSlotStaettenChanged(int slot, int wert)
	{
		if (_refreshing)
			return;

		_handelsManager.SetzeProduktionsStaetten(_stadtId, slot, wert);
		Refresh();
	}

	private void OnSlotZielstadtSelected(int slot)
	{
		if (_refreshing)
			return;

		_handelsManager.SetzeVerkaufsStadt(_stadtId, slot, _slotZielstadt[slot].GetSelectedId());
		Refresh();
	}

	#endregion
}
