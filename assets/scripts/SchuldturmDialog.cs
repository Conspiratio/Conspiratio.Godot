using System.Threading.Tasks;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Anzeige des Schuldturms (Migration von Ort_Kerker/SitztImKerker aus dem WinForms-Client):
/// zeigt vor dem Kerker-Hintergrund (HintKerker) oben im Pergament-Banner dieselbe Kopfzeile wie das
/// Kontor – Taler links, Ort ("Schuldturm") und Jahr rechts, Spielername samt Amt zentriert darunter –
/// und im unteren Bereich die Meldung, dass der Spieler dieses Jahr im Schuldturm verbringt. Ein
/// Rechtsklick (oder Esc) schließt die Anzeige.
/// </summary>
public partial class SchuldturmDialog : DialogBase
{
	[Export]
	public NodePath LabelTalerPath { get; set; }

	[Export]
	public NodePath LabelPlaceDatePath { get; set; }

	[Export]
	public NodePath LabelPlayerNameAndOfficePath { get; set; }

	private Label _labelTaler;
	private Label _labelPlaceDate;
	private Label _labelPlayerNameAndOffice;

	protected override void OnReady()
	{
		_labelTaler = GetNode<Label>(LabelTalerPath);
		_labelPlaceDate = GetNode<Label>(LabelPlaceDatePath);
		_labelPlayerNameAndOffice = GetNode<Label>(LabelPlayerNameAndOfficePath);
	}

	public Task ShowDialog()
	{
		var spieler = SW.Dynamisch.GetAktHum();
		_labelTaler.Text = spieler.GetTalerFormatiert();
		_labelPlaceDate.Text = "Schuldturm A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();

		return ShowAndAwait();
	}
}
