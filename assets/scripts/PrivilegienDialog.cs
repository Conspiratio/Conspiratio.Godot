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
public partial class PrivilegienDialog : Control
{
	[Export]
	public NodePath VBoxPrivilegienPath { get; set; }

	[Export]
	public NodePath LabelKeinePath { get; set; }

	private VBoxContainer _vBoxPrivilegien;
	private Label _labelKeine;
	private PackedScene _linkButtonScene;

	private PrivilegienManager _privilegienManager;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_vBoxPrivilegien = GetNode<VBoxContainer>(VBoxPrivilegienPath);
		_labelKeine = GetNode<Label>(LabelKeinePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(PrivilegienManager privilegienManager)
	{
		_privilegienManager = privilegienManager;
		_privilegienManager.AktualisierePrivilegien();
		Fill();

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
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

	private void OnPrivilegPressed(int privilegId)
	{
		// Führt das Privileg aus (Infotext oder öffnet den zugehörigen Dialog). Anschließend die Liste
		// neu aufbauen, da sich die Privilegien ändern können (z. B. Amt niederlegen).
		_privilegienManager.FuehreAus(privilegId);
		Fill();
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void _on_link_button_close_pressed()
	{
		CloseDialog();
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
