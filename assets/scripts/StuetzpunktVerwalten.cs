using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Kampf;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Stützpunkt-Verwaltung (Migration von frmStuetzpunktVerwalten, Kernbereich): verwaltet einen
/// eigenen Stützpunkt – die vier Einheitentypen (anheuern/entlassen über ein Zahlenfeld je Einheit),
/// die Ausbauten Sicherheit/Tarnung, Zustand (Reparatur), Kapazität (Unterkünfte) und – bei Zollburgen
/// – den Zollsatz (jeweils über den Prozentwert-festlegen-Dialog) sowie das Manöver. Die Logik liegt
/// im StuetzpunktVerwaltenManager der Lib. Die Aktionen (Überfall/Überwachen/Verstärkung) folgen später.
/// </summary>
public partial class StuetzpunktVerwalten : Control
{
	private static readonly string[] IconsZollburg =
		{ "SymbSoeldner", "SymbMusketier", "SymbKanonier", "SymbOffizier" };

	private static readonly string[] IconsRaeuberlager =
		{ "SymbRaeuber", "SymbBombenleger", "SymbKanonier", "SymbSchuetze" };

	private Label _labelName;
	private Label _labelTaler;
	private HBoxContainer _hboxEinheiten;
	private HBoxContainer _hboxUpgrades;
	private PackedScene _buttonScene;
	private PackedScene _numericScene;

	private VBoxContainer _vboxAktionen;

	private Main _main;
	private StuetzpunktVerwaltenManager _manager;
	private StuetzpunktAktionenManager _aktionenManager;
	private int[] _aktuelleAnzahl;
	private controls.NumericButtonWithSounds[] _numerics;
	private controls.NumericButtonWithSounds[][] _aktionsNumerics;

	public override void _Ready()
	{
		_labelName = GetNode<Label>("LabelName");
		_labelTaler = GetNode<Label>("LabelTaler");
		_hboxEinheiten = GetNode<HBoxContainer>("HBoxEinheiten");
		_hboxUpgrades = GetNode<HBoxContainer>("HBoxUpgrades");
		_vboxAktionen = GetNode<VBoxContainer>("VBoxAktionen");
		_buttonScene = GD.Load<PackedScene>("res://scenes/controls/ButtonWithSounds.tscn");
		_numericScene = GD.Load<PackedScene>("res://scenes/controls/NumericButtonWithSounds.tscn");

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		Schliessen();
	}

	/// <summary>Öffnet die Verwaltung des eigenen Stützpunkts.</summary>
	public void ZeigeVerwaltung(int stuetzpunktId)
	{
		_manager = new StuetzpunktVerwaltenManager(stuetzpunktId);
		_aktionenManager = new StuetzpunktAktionenManager(stuetzpunktId);
		_labelName.Text = _manager.Name;

		BaueEinheiten();
		BaueUpgrades();
		BaueAktionen();
		UpdateHud();

		Show();
		SetProcessInput(true);
	}

	private void Schliessen()
	{
		Hide();
		SetProcessInput(false);
		_main.SoeldnerRaeuberKarte.ReturnFromVerwaltung();
	}

	private void UpdateHud()
	{
		_labelTaler.Text = SW.Dynamisch.GetAktHum().GetTalerFormatiert();
	}

	private void BaueEinheiten()
	{
		foreach (Node child in _hboxEinheiten.GetChildren())
		{
			_hboxEinheiten.RemoveChild(child);
			child.QueueFree();
		}

		string[] icons = _manager.IstZollburg ? IconsZollburg : IconsRaeuberlager;
		_aktuelleAnzahl = new int[_manager.EinheitenAnzahl];
		_numerics = new controls.NumericButtonWithSounds[_manager.EinheitenAnzahl];

		for (int i = 0; i < _manager.EinheitenAnzahl; i++)
		{
			int index = i;

			var zelle = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			zelle.AddThemeConstantOverride("separation", 6);

			var iconButton = _buttonScene.Instantiate<controls.ButtonWithSounds>();
			iconButton.CustomMinimumSize = new Vector2(96, 96);
			iconButton.Icon = GD.Load<Texture2D>("res://assets/images/symbole/" + icons[i] + ".png");
			iconButton.ExpandIcon = true;
			iconButton.Flat = true;
			iconButton.TooltipText = _manager.GetEinheitName(i);
			iconButton.Pressed += () => OnEinheitPressed(index);

			var label = new Label { Text = _manager.GetEinheitName(i), HorizontalAlignment = HorizontalAlignment.Center };
			label.AddThemeColorOverride("font_color", new Color(1, 0.843137f, 0, 1));

			var numeric = _numericScene.Instantiate<controls.NumericButtonWithSounds>();
			numeric.CustomMinimumSize = new Vector2(96, 40);
			numeric.MinimalerWert = 0;
			numeric.MaximalerWert = _manager.Kapazitaet;
			numeric.Wert = _manager.GetAnzahl(i);

			_aktuelleAnzahl[i] = _manager.GetAnzahl(i);
			_numerics[i] = numeric;

			zelle.AddChild(iconButton);
			zelle.AddChild(label);
			zelle.AddChild(numeric);
			_hboxEinheiten.AddChild(zelle);
		}
	}

	private void BaueUpgrades()
	{
		foreach (Node child in _hboxUpgrades.GetChildren())
		{
			_hboxUpgrades.RemoveChild(child);
			child.QueueFree();
		}

		UpgradeButton("SymbDoor", _manager.SicherheitLabel,
			() => SW.UI.ProzentwertFestlegenDialog.ShowDialog(ProzentwertArt.SicherheitTarnungStuetzpunkt, _manager.StuetzpunktId));
		UpgradeButton("SymbAnwImBau", "Zustand reparieren",
			() => SW.UI.ProzentwertFestlegenDialog.ShowDialog(ProzentwertArt.ZustandStuetzpunkt, _manager.StuetzpunktId));
		UpgradeButton("SymbAnwHaus1", "Unterkünfte ausbauen",
			() => SW.UI.ProzentwertFestlegenDialog.ShowDialog(ProzentwertArt.KapazitaetStuetzpunkt, _manager.StuetzpunktId));

		if (_manager.IstZollburg)
			UpgradeButton("SymbJustiz", "Zollsatz festlegen",
				() => SW.UI.ProzentwertFestlegenDialog.ShowDialog(ProzentwertArt.ZollsatzZollburg, _manager.StuetzpunktId));

		var manoever = UpgradeButtonNode("Roh17", "Manöver durchführen");
		manoever.Pressed += OnManoeverPressed;
	}

	private void UpgradeButton(string icon, string tooltip, System.Action aktion)
	{
		var button = UpgradeButtonNode(icon, tooltip);
		button.Pressed += () => aktion();
	}

	private controls.ButtonWithSounds UpgradeButtonNode(string icon, string tooltip)
	{
		var button = _buttonScene.Instantiate<controls.ButtonWithSounds>();
		button.CustomMinimumSize = new Vector2(84, 84);
		button.Icon = GD.Load<Texture2D>("res://assets/images/symbole/" + icon + ".png");
		button.ExpandIcon = true;
		button.Flat = true;
		button.TooltipText = tooltip;
		_hboxUpgrades.AddChild(button);
		return button;
	}

	private async void OnEinheitPressed(int index)
	{
		SetProcessInput(false);

		int neu = _numerics[index].Wert;
		int alt = _aktuelleAnzahl[index];

		if (neu > alt)
		{
			if (await _manager.Anheuern(index, neu - alt))
				_aktuelleAnzahl[index] = neu;
			else
				_numerics[index].Wert = alt;
		}
		else if (neu < alt)
		{
			if (await _manager.Entlassen(index, alt - neu))
				_aktuelleAnzahl[index] = neu;
			else
				_numerics[index].Wert = alt;
		}

		UpdateHud();
		BaueAktionen(); // die verfügbaren Truppen für die Aufträge haben sich geändert

		if (Visible)
			SetProcessInput(true);
	}

	private void BaueAktionen()
	{
		foreach (Node child in _vboxAktionen.GetChildren())
		{
			_vboxAktionen.RemoveChild(child);
			child.QueueFree();
		}

		_aktionsNumerics = new controls.NumericButtonWithSounds[_aktionenManager.AktionenAnzahl][];

		for (int slot = 0; slot < _aktionenManager.AktionenAnzahl; slot++)
		{
			int s = slot;

			var reihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			reihe.AddThemeConstantOverride("separation", 12);

			// Aktionsart umschalten
			var artButton = _buttonScene.Instantiate<controls.ButtonWithSounds>();
			artButton.CustomMinimumSize = new Vector2(200, 40);
			artButton.Text = _aktionenManager.GetAktionsartName(slot);
			artButton.Pressed += () => { _aktionenManager.ZyklusAktionsart(s); BaueAktionen(); };
			reihe.AddChild(artButton);

			// Ziel: Grafschaft (Überwachen/Plündern) oder Stützpunkt (Truppen schicken)
			if (_aktionenManager.ZielIstGrafschaft(slot))
				reihe.AddChild(ZielButton(s, true));
			else if (_aktionenManager.ZielIstStuetzpunkt(slot))
				reihe.AddChild(ZielButton(s, false));

			// Einheiten-Zuteilung (nur wenn ein Auftrag gewählt ist)
			_aktionsNumerics[slot] = new controls.NumericButtonWithSounds[_aktionenManager.EinheitenAnzahl];

			if (_aktionenManager.GetAktionsartName(slot) != "Kein Auftrag")
			{
				for (int u = 0; u < _aktionenManager.EinheitenAnzahl; u++)
				{
					int unit = u;
					var numeric = _numericScene.Instantiate<controls.NumericButtonWithSounds>();
					numeric.CustomMinimumSize = new Vector2(84, 40);
					numeric.TooltipText = _aktionenManager.GetEinheitName(u);
					numeric.MinimalerWert = 0;
					numeric.MaximalerWert = _aktionenManager.GetMaxEinheitInAktion(slot, u);
					numeric.Wert = _aktionenManager.GetEinheitInAktion(slot, u);
					numeric.WertChanged += _ => OnAktionsEinheitChanged(s, unit);
					_aktionsNumerics[slot][u] = numeric;
					reihe.AddChild(numeric);
				}
			}

			_vboxAktionen.AddChild(reihe);
		}
	}

	private controls.ButtonWithSounds ZielButton(int slot, bool grafschaft)
	{
		var button = _buttonScene.Instantiate<controls.ButtonWithSounds>();
		button.CustomMinimumSize = new Vector2(220, 40);
		button.Text = grafschaft ? _aktionenManager.GetZielLandName(slot) : _aktionenManager.GetZielStuetzpunktName(slot);

		button.Pressed += () =>
		{
			if (grafschaft)
			{
				int naechste = _aktionenManager.GetZielLand(slot) + 1;
				if (naechste > _aktionenManager.LandMax)
					naechste = _aktionenManager.LandMin;
				_aktionenManager.SetZielLand(slot, naechste);
			}
			else
			{
				int naechste = _aktionenManager.GetZielStuetzpunkt(slot) + 1;
				if (naechste > _aktionenManager.StuetzpunktMax)
					naechste = _aktionenManager.StuetzpunktMin;
				_aktionenManager.SetZielStuetzpunkt(slot, naechste);
			}

			BaueAktionen();
		};

		return button;
	}

	private void OnAktionsEinheitChanged(int slot, int unit)
	{
		_aktionenManager.SetEinheitInAktion(slot, unit, _aktionsNumerics[slot][unit].Wert);

		// Die Obergrenzen des jeweils anderen Slots aktualisieren.
		int anderer = slot == 0 ? 1 : 0;
		if (_aktionsNumerics[anderer] != null)
		{
			for (int u = 0; u < _aktionenManager.EinheitenAnzahl; u++)
				if (_aktionsNumerics[anderer][u] != null)
					_aktionsNumerics[anderer][u].MaximalerWert = _aktionenManager.GetMaxEinheitInAktion(anderer, u);
		}
	}

	private async void OnManoeverPressed()
	{
		SetProcessInput(false);

		await _manager.ManoeverDurchfuehren();
		UpdateHud();

		if (Visible)
			SetProcessInput(true);
	}
}
