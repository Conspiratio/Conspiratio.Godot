using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class AbrechnungDialog : DialogBase
{
	[Export]
	public NodePath GridContainerPositionenPath { get; set; }

	[Export]
	public NodePath LinkButtonClosePath { get; set; }

	private GridContainer _gridContainerPositionen;
	private controls.LinkButtonWithSounds _linkButtonClose;


	// Called when the node enters the scene tree for the first time.
	protected override void OnReady()
	{
		_gridContainerPositionen = GetNode<GridContainer>(GridContainerPositionenPath);
		_linkButtonClose = GetNode<controls.LinkButtonWithSounds>(LinkButtonClosePath);
	}

	public async Task ShowDialog(AbrechnungsErgebnis ergebnis)
	{
		FillPositionen(ergebnis);
		SoundManager.Instance.PlayCoins();

		await ShowAndAwait();
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
		AddPosition("Unterhalt", ergebnis.Unterhalt);
		AddPosition("Hofhaltung", ergebnis.Hofhaltung);
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

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
