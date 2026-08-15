using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Lagerraum-Kauf-Dialog (Migration von LagerraumKaufen): für eine Werkstätte des Spielers werden drei
/// Angebote für zusätzlichen Lagerraum (Fläche und Preis) angeboten; ein Klick kauft das Angebot, sofern
/// genügend Taler vorhanden sind. Der Rechtsklick schließt den Dialog. Die Logik liegt im LagerraumManager.
/// </summary>
public partial class LagerraumKaufenDialog : DialogBase
{
	[Export]
	public NodePath LabelAktuellPath { get; set; }

	[Export]
	public NodePath VBoxAngebotePath { get; set; }

	private Label _labelAktuell;
	private VBoxContainer _vBoxAngebote;
	private PackedScene _buttonScene;

	private LagerraumManager _manager;

	/// <summary>
	/// Die Zeile je Angebot. Gemerkt statt über den Kindindex angesprochen: Ein gekauftes Angebot wird
	/// entfernt, danach passt der Index eines späteren Angebots nicht mehr zu seiner Position in der Box.
	/// </summary>
	private readonly List<HBoxContainer> _reihen = new();

	protected override void OnReady()
	{
		_labelAktuell = GetNode<Label>(LabelAktuellPath);
		_vBoxAngebote = GetNode<VBoxContainer>(VBoxAngebotePath);
		_buttonScene = GD.Load<PackedScene>("res://scenes/controls/ButtonWithSounds.tscn");
	}

	/// <summary>Öffnet den Lagerraum-Kauf für die angegebene Werkstätte in der Stadt.</summary>
	public Task ShowDialog(int stadtId, int werkstattNr)
	{
		_manager = new LagerraumManager(stadtId, werkstattNr);

		AktualisiereAktuell();
		BaueAngebote();

		return ShowAndAwait();
	}

	private void AktualisiereAktuell()
	{
		_labelAktuell.Text = "Aktueller Lagerraum: " + _manager.AktuellerLagerraum + " m²";
	}

	private void BaueAngebote()
	{
		foreach (Node child in _vBoxAngebote.GetChildren())
		{
			_vBoxAngebote.RemoveChild(child);
			child.QueueFree();
		}

		_reihen.Clear();

		for (int i = 0; i < LagerraumManager.AnzahlAngebote; i++)
		{
			int angebot = i;

			var reihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			reihe.AddThemeConstantOverride("separation", 20);

			var button = _buttonScene.Instantiate<controls.ButtonWithSounds>();
			button.CustomMinimumSize = new Vector2(160, 44);
			button.Text = _manager.GetGroesse(i) + " m²";
			button.Pressed += () => OnAngebotGewaehlt(angebot);
			reihe.AddChild(button);

			var labelPreis = new Label { Text = "für " + _manager.GetPreis(i).ToStringGeld(), VerticalAlignment = VerticalAlignment.Center };
			labelPreis.AddThemeColorOverride("font_color", new Color(0.16f, 0.11f, 0.05f));
			reihe.AddChild(labelPreis);

			_reihen.Add(reihe);
			_vBoxAngebote.AddChild(reihe);
		}
	}

	private async void OnAngebotGewaehlt(int angebot)
	{
		SetProcessInput(false);

		if (_manager.Kaufe(angebot))
		{
			SoundManager.Instance.PlayCoins();
			AktualisiereAktuell();

			// Das gekaufte Angebot ausblenden (wie im Original) – über die gemerkte Zeile, nicht über den
			// Kindindex: Nach dem ersten Kauf ist die Box kürzer, und ein späterer Index zeigt dann
			// entweder auf das falsche Angebot oder ins Leere (Absturz beim zweiten Kauf).
			if (angebot < _reihen.Count && IsInstanceValid(_reihen[angebot]))
				_reihen[angebot].QueueFree();
		}
		else
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt nicht genügend Taler für diesen Lagerraum.");
		}

		if (Visible)
			SetProcessInput(true);
	}
}
