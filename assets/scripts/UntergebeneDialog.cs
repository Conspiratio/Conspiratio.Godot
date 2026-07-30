using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Privilegien.Untergebene;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Untergebene-Privileg (Migration von UntergebeneForm/UntergebenenOptionen): listet die
/// Untergebenen des aktiven Spielers (durch dessen Amt bestimmt). Ein Klick öffnet die Optionen, deren
/// einzige echte Aktion die Einleitung einer Amtsenthebung ist. Die Logik liegt im UntergebeneManager.
/// </summary>
public partial class UntergebeneDialog : DialogBase, IUntergebeneDialog
{
	[Export]
	public NodePath VBoxUntergebenePath { get; set; }

	[Export]
	public NodePath LabelKeinePath { get; set; }

	private VBoxContainer _vBoxUntergebene;
	private Label _labelKeine;
	private PackedScene _linkButtonScene;

	private UntergebeneManager _manager;

	protected override void OnReady()
	{
		_vBoxUntergebene = GetNode<VBoxContainer>(VBoxUntergebenePath);
		_labelKeine = GetNode<Label>(LabelKeinePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	/// <summary>
	/// Aufruf über das Untergebene-Privileg. Öffnet den Dialog asynchron (fire-and-forget), da die
	/// Schnittstelle synchron ist.
	/// </summary>
	DialogResultGame IUntergebeneDialog.ShowDialog()
	{
		_ = ShowUntergebene();
		return DialogResultGame.None;
	}

	private async Task ShowUntergebene()
	{
		_manager = new UntergebeneManager();
		Fill();

		await ShowAndAwait();
	}

	private void Fill()
	{
		foreach (Node child in _vBoxUntergebene.GetChildren())
		{
			_vBoxUntergebene.RemoveChild(child);
			child.QueueFree();
		}

		var untergebene = _manager.GetUntergebene();

		_labelKeine.Text = _manager.KeineUntergebeneText;
		_labelKeine.Visible = untergebene.Count == 0;
		_vBoxUntergebene.Visible = untergebene.Count > 0;

		foreach (var untergebener in untergebene)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = untergebener.Name;

			int id = untergebener.Id;
			button.Pressed += () => OnUntergebenerPressed(id);

			_vBoxUntergebene.AddChild(button);
		}
	}

	private async void OnUntergebenerPressed(int id)
	{
		SetProcessInput(false);

		// Optionen: einzige echte Aktion ist die Amtsenthebung (btn_d3 im Original ist wirkungslos)
		var antwort = await SW.UI.YesNoQuestion.ShowDialogText(
			_manager.GetOptionenFrage(id), "Amtsenthebung einleiten", "Abbrechen");

		if (antwort == DialogResultGame.Yes)
		{
			string meldung = _manager.LeiteAmtsenthebungEin(id);
			await SW.UI.ShowText.ShowDialog(meldung);
		}

		// Wie im Original: nach der Optionsauswahl wird die Liste geschlossen.
		Close(DialogResultGame.OK);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
