using System.Threading.Tasks;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Ankündigung des nächsten Spielers zu Zugbeginn (Migration von HintNaechsterSpieler):
/// zeigt vor dem Tor-Hintergrund in Gold "Nächster Spieler" sowie Name und Amt des Spielers.
/// Ein Rechtsklick (oder Esc) schließt die Ankündigung. Ersetzt die bisherige Textmeldung.
/// </summary>
public partial class NaechsterSpielerDialog : DialogBase
{
	[Export]
	public NodePath LabelSpielerPath { get; set; }

	private Label _labelSpieler;

	protected override void OnReady()
	{
		_labelSpieler = GetNode<Label>(LabelSpielerPath);
	}

	/// <summary>Kündigt den Spieler an; <paramref name="spielerText"/> enthält Name und Amt (mehrzeilig).</summary>
	public Task ShowDialog(string spielerText)
	{
		_labelSpieler.Text = spielerText;
		return ShowAndAwait();
	}
}
