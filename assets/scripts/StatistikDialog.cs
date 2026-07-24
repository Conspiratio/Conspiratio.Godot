using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Spielerstatistik (Migration von FormStatistik): zeigt zu jedem menschlichen Spieler die
/// gesammelten Statistikwerte in zwei Spalten. Über die Banner am oberen Rand wird zwischen den
/// Spielern umgeschaltet. Der Rechtsklick schließt die Anzeige. Die Werte liefert der StatistikManager.
/// </summary>
public partial class StatistikDialog : Control
{
	[Export]
	public NodePath LabelNamePath { get; set; }

	[Export]
	public NodePath HBoxBannerPath { get; set; }

	[Export]
	public NodePath LinksBeschriftungPath { get; set; }

	[Export]
	public NodePath LinksWertPath { get; set; }

	[Export]
	public NodePath RechtsBeschriftungPath { get; set; }

	[Export]
	public NodePath RechtsWertPath { get; set; }

	private Label _labelName;
	private HBoxContainer _hboxBanner;
	private Label _linksBeschriftung;
	private Label _linksWert;
	private Label _rechtsBeschriftung;
	private Label _rechtsWert;

	private StatistikManager _manager;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelName = GetNode<Label>(LabelNamePath);
		_hboxBanner = GetNode<HBoxContainer>(HBoxBannerPath);
		_linksBeschriftung = GetNode<Label>(LinksBeschriftungPath);
		_linksWert = GetNode<Label>(LinksWertPath);
		_rechtsBeschriftung = GetNode<Label>(RechtsBeschriftungPath);
		_rechtsWert = GetNode<Label>(RechtsWertPath);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Öffnet die Statistik und zeigt zunächst den ersten menschlichen Spieler.</summary>
	public Task ShowDialog()
	{
		_manager = new StatistikManager();
		var ids = _manager.GetSpielerIds();

		BaueBannerButtons(ids);

		if (ids.Count > 0)
			ZeigeSpieler(ids[0]);

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	private void BaueBannerButtons(IReadOnlyList<int> ids)
	{
		foreach (Node child in _hboxBanner.GetChildren())
		{
			_hboxBanner.RemoveChild(child);
			child.QueueFree();
		}

		// Nur bei mehreren Spielern sind die Umschalt-Banner nötig.
		if (ids.Count < 2)
			return;

		foreach (int id in ids)
		{
			int spielerId = id;

			var button = new TextureButton
			{
				TextureNormal = GD.Load<Texture2D>("res://assets/images/banner/ban" + _manager.GetBanner(id) + ".png"),
				CustomMinimumSize = new Vector2(40, 54),
				IgnoreTextureSize = true,
				StretchMode = TextureButton.StretchModeEnum.KeepAspect,
				TooltipText = _manager.GetName(id)
			};

			button.Pressed += () =>
			{
				SoundManager.Instance.PlayLeftClick();
				ZeigeSpieler(spielerId);
			};

			_hboxBanner.AddChild(button);
		}
	}

	private void ZeigeSpieler(int spielerId)
	{
		_labelName.Text = _manager.GetName(spielerId);

		var seite = _manager.GetStatistik(spielerId);

		_linksBeschriftung.Text = string.Join("\n", seite.Links.Select(eintrag => eintrag.Beschriftung));
		_linksWert.Text = string.Join("\n", seite.Links.Select(eintrag => eintrag.Wert));
		_rechtsBeschriftung.Text = string.Join("\n", seite.Rechts.Select(eintrag => eintrag.Beschriftung));
		_rechtsWert.Text = string.Join("\n", seite.Rechts.Select(eintrag => eintrag.Wert));
	}

	private void CloseDialog()
	{
		Hide();
		SetProcessInput(false);
		_dialogClosed?.TrySetResult(true);
	}
}
