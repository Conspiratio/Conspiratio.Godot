using System.Threading.Tasks;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Anzeige des Schuldturms (Migration von Ort_Kerker/SitztImKerker aus dem WinForms-Client):
/// zeigt vor dem Kerker-Hintergrund (HintKerker) die Meldung, dass der Spieler dieses Jahr im Schuldturm
/// verbringt. Ein Rechtsklick (oder Esc) schließt die Anzeige. Ersetzt die bisherige generische Textmeldung
/// auf dem Rundennachrichten-Bildschirm.
/// </summary>
public partial class SchuldturmDialog : DialogBase
{
	public Task ShowDialog()
	{
		return ShowAndAwait();
	}
}
