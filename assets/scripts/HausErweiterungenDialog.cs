using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Auswahl einer Hauserweiterung (Garten, Teich, ...) für den Wohnsitz, wie HausErweiterungen im WinForms-Client.
/// </summary>
public partial class HausErweiterungenDialog : DialogBase
{
	[Export]
	public NodePath LabelTitlePath { get; set; }

	[Export]
	public NodePath VBoxErweiterungenPath { get; set; }

	private Label _labelTitle;
	private VBoxContainer _vBoxErweiterungen;
	private PackedScene _linkButtonScene;

	private AnwesenManager _anwesenManager;
	private int _stadtId;

	protected override void OnReady()
	{
		_labelTitle = GetNode<Label>(LabelTitlePath);
		_vBoxErweiterungen = GetNode<VBoxContainer>(VBoxErweiterungenPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	public async Task ShowDialog(AnwesenManager anwesenManager, int stadtId)
	{
		_anwesenManager = anwesenManager;
		_stadtId = stadtId;

		_labelTitle.Text = "Welche Erweiterung wollt Ihr an " + _anwesenManager.GetNameInklPronomen(stadtId, false, false) + " anbauen?";
		FillErweiterungen();

		await ShowAndAwait();
	}

	private void FillErweiterungen()
	{
		foreach (Node child in _vBoxErweiterungen.GetChildren())
		{
			_vBoxErweiterungen.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var angebot in _anwesenManager.GetFehlendeErweiterungen(_stadtId))
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = angebot.Name + " für " + angebot.Preis.ToStringGeld();

			var kopie = angebot;
			button.Pressed += () => OnErweiterungGewaehlt(kopie);

			_vBoxErweiterungen.AddChild(button);
		}
	}

	private async void OnErweiterungGewaehlt(ErweiterungsAngebot angebot)
	{
		SetProcessInput(false);

		if (!_anwesenManager.KannBezahlen(angebot.Preis))
		{
			await SW.UI.ShowText.ShowDialog("Die " + angebot.Preis.ToStringGeld(false) + " Taler für dieses Vorhaben besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr wirklich\n" + angebot.Name + "\nbauen lassen?", "Ja", "Lieber nicht!") == DialogResultGame.Yes)
		{
			_anwesenManager.BaueErweiterung(_stadtId, angebot);
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
