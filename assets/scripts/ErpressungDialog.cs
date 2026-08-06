using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Entscheidung eines menschlichen Erpressungsopfers (Issue #13): Anders als eine KI würfelt ein
/// Mitspieler nicht – ihm werden die Vorwürfe vorgelegt, und er entscheidet selbst, ob er sich beugt.
/// Lehnt er ab, behält der Erpresser die Beweise und kann ihn damit vor Gericht bringen.
///
/// Die Auswahl läuft über den vorhandenen Ja/Nein-Dialog; diese Klasse übersetzt sie nur in die
/// Lib-Schnittstelle <see cref="IErpressungDialog"/>.
/// </summary>
public partial class ErpressungDialog : Node, IErpressungDialog
{
	public async Task<bool> FrageOpfer(string frage)
	{
		var yesNoDialog = GetParent().GetNode<YesNoDialog>(nameof(YesNoDialog));

		return await yesNoDialog.ShowDialogText(frage, "Sich beugen", "Ablehnen") == DialogResultGame.Yes;
	}
}
