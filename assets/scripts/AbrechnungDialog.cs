using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class AbrechnungDialog : Control
{
	[Export]
	public NodePath GridContainerPositionenPath { get; set; }

	[Export]
	public NodePath LinkButtonClosePath { get; set; }

	private GridContainer _gridContainerPositionen;
	private controls.LinkButtonWithSounds _linkButtonClose;

	private TaskCompletionSource<bool> _dialogClosed;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_gridContainerPositionen = GetNode<GridContainer>(GridContainerPositionenPath);
		_linkButtonClose = GetNode<controls.LinkButtonWithSounds>(LinkButtonClosePath);

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(AbrechnungsErgebnis ergebnis)
	{
		FillPositionen(ergebnis);
		SoundManager.Instance.PlayCoins();

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private void FillPositionen(AbrechnungsErgebnis ergebnis)
	{
		foreach (Node child in _gridContainerPositionen.GetChildren())
			child.QueueFree();

		AddPosition("Arbeiter", ergebnis.Arbeiterkosten);
		AddPosition("Betriebskosten", ergebnis.Betriebskosten);
		AddPosition("Transportkosten", ergebnis.Transportkosten);
		AddPosition("Verkaufssteuern", ergebnis.Verkaufssteuern);
		AddPosition("Informanten", ergebnis.Informantenkosten);
		AddPosition("Saboteure", ergebnis.Saboteurekosten);
		AddPosition("Kreditzinsen", ergebnis.Kreditzinsen);
		AddPosition("Kirchenzehnt", ergebnis.Kirchenzehnt);
		AddPosition("Zölle", ergebnis.Zollkosten);
		AddPosition("Sold", ergebnis.Sold);
		AddPosition("Gesamtkosten", ergebnis.Gesamtkosten);
	}

	private void AddPosition(string bezeichnung, int kosten)
	{
		_gridContainerPositionen.AddChild(new Label { Text = bezeichnung });
		_gridContainerPositionen.AddChild(new Label
		{
			Text = kosten.ToStringGeld(false) + " Taler",
			HorizontalAlignment = HorizontalAlignment.Right,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});
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
