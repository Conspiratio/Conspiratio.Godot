using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die jährliche Brautwerbung (Migration von Brautwerbung): der Spieler beschenkt den umworbenen
/// Partner mit einem von drei zufälligen Geschenken (oder gar keinem); je nach Gefallen steigt die
/// Verliebtheit. Nach der Reaktion schließt der Dialog.
/// </summary>
public partial class BrautwerbungDialog : Control
{
	[Export]
	public NodePath LabelFragePath { get; set; }

	[Export]
	public NodePath VBoxGeschenkePath { get; set; }

	private Label _labelFrage;
	private VBoxContainer _vBoxGeschenke;
	private PackedScene _linkButtonScene;

	private FamilieManager _familieManager;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelFrage = GetNode<Label>(LabelFragePath);
		_vBoxGeschenke = GetNode<VBoxContainer>(VBoxGeschenkePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

		HideAndDisableInput();
	}

	// Bewusst kein _Input: In der Brautwerbung muss wie im Original ein Knopf gewählt werden.

	public async Task ShowDialog(FamilieManager familieManager)
	{
		_familieManager = familieManager;

		var auswahl = _familieManager.ErstelleGeschenkAuswahl();
		_labelFrage.Text = "Was wollt Ihr " + auswahl.PartnerName + " in diesem Jahr als Beweis Eurer Liebe schenken?";

		foreach (Node child in _vBoxGeschenke.GetChildren())
		{
			_vBoxGeschenke.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var geschenk in auswahl.Geschenke)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = geschenk.Text + " für " + geschenk.Preis.ToStringGeld();

			var kopie = geschenk;
			button.Pressed += () => OnGeschenkGewaehlt(kopie);

			_vBoxGeschenke.AddChild(button);
		}

		var buttonKeins = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
		buttonKeins.Text = auswahl.KeinGeschenkText;
		buttonKeins.Pressed += OnKeinGeschenk;
		_vBoxGeschenke.AddChild(buttonKeins);

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private async void OnGeschenkGewaehlt(WerbeGeschenk geschenk)
	{
		SetProcessInput(false);

		var ergebnis = _familieManager.GibGeschenk(geschenk);
		SoundManager.Instance.PlayCoins();

		await SW.UI.ShowText.ShowDialog(ergebnis.ReaktionsText);
		CloseDialog();
	}

	private async void OnKeinGeschenk()
	{
		SetProcessInput(false);

		var ergebnis = _familieManager.GibKeinGeschenk();

		await SW.UI.ShowText.ShowDialog(ergebnis.ReaktionsText);
		CloseDialog();
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

	private Task CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}
}
