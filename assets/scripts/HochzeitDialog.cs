using System.Threading.Tasks;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Anzeige der Hochzeit (Migration von Posi_Hochzeit/case 16 aus dem WinForms-Client): zeigt vor
/// dem Trauungs-Hintergrund (HintTrauung, zwei goldene Ringe) die Meldung, dass der umworbene Partner
/// endlich eingewilligt hat. Ein Rechtsklick (oder Esc) schließt die Anzeige.
///
/// Zuvor lief die Hochzeit im Godot-Client als schlichte Meldung über den generischen Nachrichtenschirm –
/// der eigene Bildschirm des Originals fehlte, obwohl die Hochzeitsmusik bereits gespielt wurde.
/// </summary>
public partial class HochzeitDialog : DialogBase
{
	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelTitel;
	private Label _labelText;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelText = GetNode<Label>(LabelTextPath);
	}

	/// <summary>Zeigt die Hochzeitsmeldung auf dem Trauungs-Bildschirm.</summary>
	public Task ShowDialog(string meldung)
	{
		_labelTitel.Text = "Hochzeit";
		_labelText.Text = meldung;

		return ShowAndAwait();
	}
}
