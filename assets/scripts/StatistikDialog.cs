using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Personen;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Spielerstatistik (Migration von FormStatistik): zeigt zu jedem menschlichen Spieler die
/// gesammelten Statistikwerte in zwei Spalten. Über die Banner am oberen Rand wird zwischen den
/// Spielern umgeschaltet. Der Rechtsklick schließt die Anzeige. Die Werte liefert der StatistikManager.
/// </summary>
public partial class StatistikDialog : DialogBase
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

	protected override void OnReady()
	{
		_labelName = GetNode<Label>(LabelNamePath);
		_hboxBanner = GetNode<HBoxContainer>(HBoxBannerPath);
		_linksBeschriftung = GetNode<Label>(LinksBeschriftungPath);
		_linksWert = GetNode<Label>(LinksWertPath);
		_rechtsBeschriftung = GetNode<Label>(RechtsBeschriftungPath);
		_rechtsWert = GetNode<Label>(RechtsWertPath);
	}

	/// <summary>Öffnet die Statistik und zeigt zunächst den ersten menschlichen Spieler.</summary>
	public Task ShowDialog()
	{
		_manager = new StatistikManager();
		var ids = _manager.GetSpielerIds();

		BaueBannerButtons(ids);

		if (ids.Count > 0)
			ZeigeSpieler(ids[0]);

		return ShowAndAwait();
	}

	/// <summary>
	/// Öffnet die Ansicht für ein spielübergreifendes Profil (ohne Banner-Umschalter, da nur ein Profil).
	/// Nutzt dasselbe Zwei-Spalten-Pergament wie die Spielstatistik.
	/// </summary>
	public Task ShowProfil(Profil profil)
	{
		LeereBanner();

		_labelName.Text = profil.Name;
		FuelleSpalten(new ProfilStatistikManager().GetStatistik(profil));

		return ShowAndAwait();
	}

	private void LeereBanner()
	{
		foreach (Node child in _hboxBanner.GetChildren())
		{
			_hboxBanner.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void BaueBannerButtons(IReadOnlyList<int> ids)
	{
		LeereBanner();

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
		FuelleSpalten(_manager.GetStatistik(spielerId));
	}

	private void FuelleSpalten(StatistikSeite seite)
	{
		_linksBeschriftung.Text = string.Join("\n", seite.Links.Select(eintrag => eintrag.Beschriftung));
		_linksWert.Text = string.Join("\n", seite.Links.Select(eintrag => eintrag.Wert));
		_rechtsBeschriftung.Text = string.Join("\n", seite.Rechts.Select(eintrag => eintrag.Beschriftung));
		_rechtsWert.Text = string.Join("\n", seite.Rechts.Select(eintrag => eintrag.Wert));
	}
}
