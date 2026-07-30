using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Bewerbungsmappe (Migration von BewerbForm): listet die freien Ämter, auf die sich der aktive
/// Spieler bewerben kann. Ein Klick meldet ihn für die Wahl an oder wieder ab (nur eine Bewerbung
/// gleichzeitig); "Info" zeigt Wähler und Mitbewerber der jeweiligen Wahl.
/// </summary>
public partial class BewerbDialog : DialogBase
{
	[Export]
	public NodePath VBoxAemterPath { get; set; }

	[Export]
	public NodePath LabelKeineAemterPath { get; set; }

	private VBoxContainer _vBoxAemter;
	private Label _labelKeineAemter;
	private PackedScene _linkButtonScene;

	private AemterManager _aemterManager;
	private Main _main;

	protected override void OnReady()
	{
		_vBoxAemter = GetNode<VBoxContainer>(VBoxAemterPath);
		_labelKeineAemter = GetNode<Label>(LabelKeineAemterPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

		_main = GetParent<Main>();
	}

	public async Task ShowDialog(AemterManager aemterManager)
	{
		_aemterManager = aemterManager;
		Fill();

		await ShowAndAwait();
	}

	private void Fill()
	{
		foreach (Node child in _vBoxAemter.GetChildren())
		{
			_vBoxAemter.RemoveChild(child);
			child.QueueFree();
		}

		var angebote = _aemterManager.GetBewerbungsangebote();

		_labelKeineAemter.Visible = angebote.Count == 0;
		_vBoxAemter.Visible = angebote.Count > 0;

		foreach (var angebot in angebote)
		{
			var zeile = new HBoxContainer();
			zeile.AddThemeConstantOverride("separation", 20);

			zeile.AddChild(new Label
			{
				Text = (angebot.IstAngemeldet ? "✓ " : "") + angebot.AmtName + " in " + angebot.GebietName,
				CustomMinimumSize = new Vector2(420, 0),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			});

			int wahlId = angebot.WahlId;

			var buttonInfo = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			buttonInfo.Text = "Info";
			buttonInfo.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			buttonInfo.Pressed += () => OnInfoPressed(wahlId);
			zeile.AddChild(buttonInfo);

			var buttonToggle = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			buttonToggle.Text = angebot.IstAngemeldet ? "Zurückziehen" : "Bewerben";
			buttonToggle.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			buttonToggle.Pressed += () => OnTogglePressed(wahlId);
			zeile.AddChild(buttonToggle);

			_vBoxAemter.AddChild(zeile);
		}
	}

	private async void OnInfoPressed(int wahlId)
	{
		SetProcessInput(false);

		await _main.BewerbInfosDialog.ShowDialog(_aemterManager, wahlId);

		if (Visible)
			SetProcessInput(true);
	}

	private async void OnTogglePressed(int wahlId)
	{
		SetProcessInput(false);

		var ergebnis = _aemterManager.WahlAnmeldungUmschalten(wahlId);

		await SW.UI.ShowText.ShowDialog(ergebnis.Angemeldet
			? "Ihr wurdet für die Wahl des Amts " + ergebnis.AmtName + " aufgestellt."
			: "Ihr zieht Eure Bewerbung für das Amt des " + ergebnis.AmtName + " zurück.");

		Fill();

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
