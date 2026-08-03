using System.Text;
using System.Threading.Tasks;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Privilegien.Weltkarte;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Detailfenster eines Kontrahenten (Migration von KontrahentDetails): zeigt Name, Titel, Alter und Amt.
/// Vermögen, Gesundheit und die Beweislast (per Spionage aufgedeckte Delikte) samt Erhebungsstand erscheinen
/// nur, wenn der aktive Spieler eine laufende Spionage gegen den Kontrahenten unterhält. Rechtsklick schließt.
/// </summary>
public partial class KontrahentDetailsDialog : DialogBase
{
	[Export]
	public NodePath LabelNamePath { get; set; }

	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath LabelInhaltPath { get; set; }

	private Label _labelName;
	private Label _labelTitel;
	private Label _labelInhalt;

	protected override void OnReady()
	{
		_labelName = GetNode<Label>(LabelNamePath);
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelInhalt = GetNode<Label>(LabelInhaltPath);
	}

	/// <summary>Öffnet das Detailfenster für den Kontrahenten mit der angegebenen Spieler-ID.</summary>
	public Task ShowDialog(int spielerId)
	{
		var d = new KontrahentenManager().GetKontrahentDetails(spielerId);

		_labelName.Text = d.Name;
		_labelTitel.Text = d.Titel;

		var text = new StringBuilder();
		text.AppendLine("Alter:  " + d.Alter);
		text.AppendLine("Amt:  " + (string.IsNullOrEmpty(d.Amt) ? "–" : d.Amt));
		text.AppendLine();

		if (d.HatSpionage)
		{
			text.AppendLine("Vermögen:  " + d.Vermoegen.ToStringGeld());
			text.AppendLine("Gesundheit:  " + d.Gesundheit);
			text.AppendLine("Beweislast (aufgedeckte Delikte):  " + d.Delikte);
			text.AppendLine();
			text.AppendLine("Stand der Erkenntnisse: A.D. " + d.StandJahr);
		}
		else
		{
			text.AppendLine("Ohne eine laufende Spionage gegen diesen Kontrahenten");
			text.AppendLine("sind Vermögen, Gesundheit und Beweislast unbekannt.");
		}

		_labelInhalt.Text = text.ToString();

		return ShowAndAwait();
	}
}
