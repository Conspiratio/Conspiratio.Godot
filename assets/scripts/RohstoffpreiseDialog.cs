using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien.Weltkarte;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Rohstoffpreis-Ansicht (Migration von RohstoffpreiseForm): zeigt die Rohstoffpreise einer Stadt
/// als Raster. Über die Weltkarte-Privilegien geöffnet – Level 0 (Händler) nur zur Einsicht, Level 1
/// (Kaufmann) und Level 2 (Großkaufmann) erlauben je einmal pro Jahr eine Preis-Beeinflussung.
/// Die Logik liegt im RohstoffpreiseManager der Lib.
/// </summary>
public partial class RohstoffpreiseDialog : Control
{
	[Export]
	public NodePath LabelUeberschriftPath { get; set; }

	[Export]
	public NodePath GridPath { get; set; }

	private Label _labelUeberschrift;
	private GridContainer _grid;

	private RohstoffpreiseManager _manager;
	private readonly Dictionary<int, Label> _preisLabels = new();
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelUeberschrift = GetNode<Label>(LabelUeberschriftPath);
		_grid = GetNode<GridContainer>(GridPath);

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Öffnet die Preisansicht für eine Stadt in der angegebenen Einflussstufe.</summary>
	public Task ShowDialog(int stadtId, int level)
	{
		_manager = new RohstoffpreiseManager(stadtId, level);
		_labelUeberschrift.Text = _manager.GetUeberschrift();

		BaueRaster();

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	private void BaueRaster()
	{
		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFree();
		}

		_preisLabels.Clear();

		foreach (var preis in _manager.GetPreise())
		{
			var zelle = new VBoxContainer { CustomMinimumSize = new Vector2(80, 96) };
			zelle.AddThemeConstantOverride("separation", 2);

			var button = new Button
			{
				Icon = GD.Load<Texture2D>("res://assets/images/rohstoffe/Roh" + preis.RohId + ".png"),
				CustomMinimumSize = new Vector2(72, 72),
				TooltipText = preis.Name,
				Flat = true,
				ExpandIcon = true
			};

			int rohId = preis.RohId;
			button.Pressed += () => OnRohstoffPressed(rohId);

			var label = new Label
			{
				Text = preis.Preis.ToString(),
				HorizontalAlignment = HorizontalAlignment.Center
			};

			zelle.AddChild(button);
			zelle.AddChild(label);
			_grid.AddChild(zelle);

			_preisLabels[rohId] = label;
		}
	}

	private void AktualisierePreise()
	{
		foreach (var eintrag in _preisLabels)
			eintrag.Value.Text = _manager.GetPreis(eintrag.Key).ToString();
	}

	private async void OnRohstoffPressed(int rohId)
	{
		SetProcessInput(false);

		if (_manager.IstBereitsBenutzt())
		{
			await SW.UI.ShowText.ShowDialog(_manager.GetBereitsBenutztMeldung());
		}
		else if (_manager.Level == 0)
		{
			await SW.UI.ShowText.ShowDialog(_manager.GetZuWenigEinflussMeldung());
		}
		else if (_manager.Level == 1)
		{
			if (await SW.UI.YesNoQuestion.ShowDialogText(_manager.GetKaufmannFrage(rohId), "Ja", "Nein") == DialogResultGame.Yes)
			{
				await SW.UI.ShowText.ShowDialog(_manager.FuehreKaufmannAus(rohId));
				AktualisierePreise();
			}
		}
		else if (_manager.Level == 2)
		{
			var antwort = await SW.UI.YesNoQuestion.ShowDialogText(_manager.GetGroßkaufmannFrage(rohId), "steigern", "senken");

			if (antwort == DialogResultGame.Yes)
			{
				await SW.UI.ShowText.ShowDialog(_manager.FuehreGroßkaufmannAus(rohId, true));
				AktualisierePreise();
			}
			else if (antwort == DialogResultGame.No)
			{
				await SW.UI.ShowText.ShowDialog(_manager.FuehreGroßkaufmannAus(rohId, false));
				AktualisierePreise();
			}
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void CloseDialog()
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(true);
	}
}
