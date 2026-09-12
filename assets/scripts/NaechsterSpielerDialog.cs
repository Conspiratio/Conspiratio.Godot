using System.Threading.Tasks;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Ankündigung des nächsten Spielers zu Zugbeginn (Migration von HintNaechsterSpieler):
/// zeigt vor dem Tor-Hintergrund in Gold "Nächster Spieler" sowie Name und Amt des Spielers.
/// Ein Rechtsklick (oder Esc) schließt die Ankündigung. Ersetzt die bisherige Textmeldung.
///
/// <b>Warum der Auftragsfortschritt ein eigenes Label hat.</b> Er stand vorher als dritte Zeile im
/// Spieler-Label, das nur 600 px breit war und kein <c>autowrap_mode</c> hatte. Eine Zeile wie
/// „Auftrag ‚Kleiner Wohlstand': 270 / 100.000 Taler" braucht bei Schriftgröße 36 gut das Dreifache
/// und zeichnete deshalb über beide Ränder hinaus – die Ankündigung sah aus, als sei sie nicht
/// zentriert. Beide Labels spannen jetzt fast die volle Breite und brechen um; die Auftragszeile
/// steht darunter in kleinerer Schrift, damit sie die Namenszeile nicht überwiegt.
/// </summary>
public partial class NaechsterSpielerDialog : DialogBase
{
	[Export]
	public NodePath LabelSpielerPath { get; set; }

	[Export]
	public NodePath LabelAuftragPath { get; set; }

	private Label _labelSpieler;
	private Label _labelAuftrag;

	protected override void OnReady()
	{
		_labelSpieler = GetNode<Label>(LabelSpielerPath);
		_labelAuftrag = GetNode<Label>(LabelAuftragPath);
	}

	/// <summary>
	/// Kündigt den Spieler an; <paramref name="spielerText"/> enthält Name und Amt (mehrzeilig),
	/// <paramref name="auftragText"/> den Fortschritt eines laufenden Auftrags (leer, wenn keiner läuft).
	/// </summary>
	public Task ShowDialog(string spielerText, string auftragText = "")
	{
		_labelSpieler.Text = spielerText;
		_labelAuftrag.Text = auftragText ?? "";

		return ShowAndAwait();
	}
}
