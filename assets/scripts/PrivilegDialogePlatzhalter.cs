using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Spielwelt;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vorläufige Platzhalter-Implementierung der noch nicht migrierten Privilegien-Dialoge
/// (Bauwerk stiften, Fest geben, Prozentwert/Steuern festlegen, Untergebene). Sie verhindert, dass
/// die zugehörigen Privilegien beim Ausführen ins Leere greifen, und weist stattdessen darauf hin,
/// dass der Dialog noch nicht verfügbar ist. Wird in späteren Stufen durch die echten Dialoge ersetzt.
/// </summary>
public class PrivilegDialogePlatzhalter : IBauwerkStiftenDialog, IFestGebenDialog, IProzentwertFestlegenDialog, IUntergebeneDialog
{
	DialogResultGame IBauwerkStiftenDialog.ShowDialog()
	{
		ZeigeHinweis("Das Stiften von Bauwerken");
		return DialogResultGame.None;
	}

	DialogResultGame IFestGebenDialog.ShowDialog()
	{
		ZeigeHinweis("Das Geben eines Festes");
		return DialogResultGame.None;
	}

	void IProzentwertFestlegenDialog.ShowDialog(ProzentwertArt prozentwertArt, int zielStuetzpunktID)
	{
		ZeigeHinweis("Das Festlegen von Steuern und Prozentsätzen");
	}

	DialogResultGame IUntergebeneDialog.ShowDialog()
	{
		ZeigeHinweis("Die Verwaltung Eurer Untergebenen");
		return DialogResultGame.None;
	}

	private static void ZeigeHinweis(string was)
	{
		// Fire-and-forget: die SW.UI-Schnittstellen sind synchron, der Godot-Dialog läuft asynchron
		_ = SW.UI.ShowText.ShowDialog(was + " ist noch nicht verfügbar.");
	}
}
