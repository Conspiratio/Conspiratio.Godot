using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Privilegienanzeige (Migration von PrivilegienAnzeigen): listet die Privilegien, die der aktive
/// Spieler durch Amt, Titel und Familienstand besitzt. Ein Klick führt das Privileg aus – passive
/// Privilegien zeigen eine Information, aktionierbare öffnen den zugehörigen Dialog.
/// </summary>
public partial class PrivilegienDialog : DialogBase
{
	[Export]
	public NodePath VBoxPrivilegienPath { get; set; }

	[Export]
	public NodePath LabelKeinePath { get; set; }

	private VBoxContainer _vBoxPrivilegien;
	private Label _labelKeine;
	private PackedScene _linkButtonScene;

	private PrivilegienManager _privilegienManager;
	private Main _main;

	protected override void OnReady()
	{
		_vBoxPrivilegien = GetNode<VBoxContainer>(VBoxPrivilegienPath);
		_labelKeine = GetNode<Label>(LabelKeinePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
		_main = GetParentOrNull<Main>();
	}

	public async Task ShowDialog(PrivilegienManager privilegienManager)
	{
		_privilegienManager = privilegienManager;
		_privilegienManager.AktualisierePrivilegien();
		Fill();

		await ShowAndAwait();
	}

	private void Fill()
	{
		foreach (Node child in _vBoxPrivilegien.GetChildren())
		{
			_vBoxPrivilegien.RemoveChild(child);
			child.QueueFree();
		}

		var privilegien = _privilegienManager.GetPrivilegien();

		_labelKeine.Visible = privilegien.Count == 0;
		_vBoxPrivilegien.Visible = privilegien.Count > 0;

		foreach (var privileg in privilegien)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = privileg.Name;

			int id = privileg.Id;
			button.Pressed += () => OnPrivilegPressed(id);

			_vBoxPrivilegien.AddChild(button);
		}
	}

	private async void OnPrivilegPressed(int privilegId)
	{
		// Die immer verfügbare Ahnentafel ist kein echtes Lib-Privileg, sondern öffnet den Stammbaum-Dialog.
		if (privilegId == PrivilegienManager.AhnentafelPrivilegId)
		{
			SetProcessInput(false);
			await _main.AhnentafelDialog.ShowDialog();

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// Führt das Privileg aus (Infotext oder öffnet den zugehörigen Dialog). Anschließend die Liste
		// neu aufbauen, da sich die Privilegien ändern können (z. B. Amt niederlegen).
		_privilegienManager.FuehreAus(privilegId);
		Fill();
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
