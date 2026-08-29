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

	/// <summary>
	/// Zeigt die Jahresabrechnung. <paramref name="ansehenAenderung"/> ist die Geltung, die dieselbe
	/// Abrechnung dem Spieler gebracht oder gekostet hat (Hofhaltung und Auslastung); sie steht nicht im
	/// <see cref="AbrechnungsErgebnis"/>, weil die Lib sie direkt verbucht, und wird deshalb vom Aufrufer
	/// gemessen. Ohne sie sieht der Spieler nur die Kostenseite seiner Hofhaltungswahl.
	/// </summary>
	public async Task ShowDialog(AbrechnungsErgebnis ergebnis, int ansehenAenderung = 0)
	{
		FillPositionen(ergebnis, ansehenAenderung);
		SoundManager.Instance.PlayCoins();

		await ShowAndAwait();
	}

	private void FillPositionen(AbrechnungsErgebnis ergebnis, int ansehenAenderung)
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

		// Der Gegenwert zur Kostenseite: was die Hofhaltungsstufe und die Auslastung der Betriebe in
		// diesem Jahr an Geltung eingebracht haben. Auch die 0 wird gezeigt - "nichts gewonnen" ist die
		// Antwort auf "sparsam" und gehoert zur Entscheidung, und die Zeilenzahl bleibt so fest.
		AddZeile("Ansehen", FormatiereAnsehen(ansehenAenderung));
	}

	private static string FormatiereAnsehen(int aenderung)
	{
		if (aenderung > 0)
			return "+" + aenderung + " Punkte";

		if (aenderung < 0)
			return aenderung + (aenderung == -1 ? " Punkt" : " Punkte");

		return "±0 Punkte";
	}

	private void AddPosition(string bezeichnung, int kosten)
	{
		AddZeile(bezeichnung, kosten.ToStringGeld(false) + " Taler");
	}

	private void AddZeile(string bezeichnung, string wert)
	{
		_gridContainerPositionen.AddChild(new Label { Text = bezeichnung });
		_gridContainerPositionen.AddChild(new Label
		{
			Text = wert,
			HorizontalAlignment = HorizontalAlignment.Right,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
