using System.Threading.Tasks;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Anzeige eines Kindestods (Migration von Ort_Kindertod aus dem WinForms-Client): zeigt vor dem
/// Grabstein-Hintergrund (HintKindStirbt) die Überschrift „Tragischer Verlust" und die Meldung zum
/// verstorbenen Kind. Ein Rechtsklick (oder Esc) schließt die Anzeige.
/// </summary>
public partial class KindestodDialog : DialogBase
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelText;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
	}

	/// <summary>Zeigt die Kindestod-Meldung auf dem Grabstein-Bildschirm.</summary>
	public Task ShowDialog(string meldung)
	{
		_labelText.Text = meldung;
		return ShowAndAwait();
	}
}
