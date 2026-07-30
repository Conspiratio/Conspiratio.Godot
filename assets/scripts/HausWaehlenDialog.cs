using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Auswahl eines Wohnsitzes zum Bauen (modus 0) oder Umbauen (modus 1), wie HausWaehlen im WinForms-Client.
/// Jeder Haustyp wird mit seinem (beim Umbauen reduzierten) Preis angeboten.
/// </summary>
public partial class HausWaehlenDialog : DialogBase
{
	[Export]
	public NodePath LabelTitlePath { get; set; }

	[Export]
	public NodePath VBoxHaeuserPath { get; set; }

	private Label _labelTitle;
	private VBoxContainer _vBoxHaeuser;
	private PackedScene _linkButtonScene;

	private AnwesenManager _anwesenManager;
	private int _stadtId;
	private int _modus;

	protected override void OnReady()
	{
		_labelTitle = GetNode<Label>(LabelTitlePath);
		_vBoxHaeuser = GetNode<VBoxContainer>(VBoxHaeuserPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	/// <summary>
	/// Öffnet die Auswahl. modus 0 = neu bauen, 1 = umbauen.
	/// </summary>
	public async Task ShowDialog(AnwesenManager anwesenManager, int stadtId, int modus)
	{
		_anwesenManager = anwesenManager;
		_stadtId = stadtId;
		_modus = modus;

		_labelTitle.Text = modus == 1 ? "Wohin wollt Ihr Euren Wohnsitz umbauen lassen?" : "Welchen Wohnsitz wollt Ihr errichten lassen?";
		FillHaeuser();

		await ShowAndAwait();
	}

	private void FillHaeuser()
	{
		foreach (Node child in _vBoxHaeuser.GetChildren())
		{
			_vBoxHaeuser.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var angebot in _anwesenManager.GetBaubareHaeuser(_stadtId, _modus))
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = angebot.Name + " für " + angebot.Preis.ToStringGeld();

			var kopie = angebot;
			button.Pressed += () => OnHausGewaehlt(kopie);

			_vBoxHaeuser.AddChild(button);
		}
	}

	private async void OnHausGewaehlt(HausAngebot angebot)
	{
		SetProcessInput(false);

		if (_anwesenManager.IstBereitsVorhanden(_stadtId, angebot.HausId))
		{
			await SW.UI.ShowText.ShowDialog("Ihr besitzt hier bereits genau diesen Wohnsitz");
		}
		else if (!_anwesenManager.KannBezahlen(angebot.Preis))
		{
			await SW.UI.ShowText.ShowDialog("Die " + angebot.Preis.ToStringGeld(false) + " Taler für dieses Vorhaben besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr wirklich für\n" + angebot.Preis.ToStringGeld() + " ein/e " + angebot.Name + "\nbauen lassen?", "Ja", "Nein") == DialogResultGame.Yes)
		{
			_anwesenManager.BaueHaus(_stadtId, angebot);
			SoundManager.Instance.PlayCoins();
			Close(DialogResultGame.OK);
			return;
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
