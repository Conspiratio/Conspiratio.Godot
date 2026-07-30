using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Beziehungen pflegen (Migration von BeziehungenPflegen aus dem Hinterzimmer): ein kleines Menü mit
/// zwei Möglichkeiten, die Beziehung zu einem Ziel zu verbessern – "Karten spielen" (nur gegen KI,
/// abhängig vom eigenen Reichtum) und "Bestechen" (eine frei wählbare Taler-Summe zukommen lassen).
/// Die Logik liegt in der Lib (KartenSpielen/Bestechen).
/// </summary>
public partial class BeziehungenPflegenDialog : DialogBase, IBeziehungPflegen
{
	[Export]
	public NodePath LabelErklaerungPath { get; set; }

	[Export]
	public NodePath OptionenBoxPath { get; set; }

	[Export]
	public NodePath BestechenBoxPath { get; set; }

	[Export]
	public NodePath NumericTalerPath { get; set; }

	private Label _labelErklaerung;
	private Control _optionenBox;
	private Control _bestechenBox;
	private controls.NumericButtonWithSounds _numericTaler;

	private int _spielerId;

	protected override void OnReady()
	{
		_labelErklaerung = GetNode<Label>(LabelErklaerungPath);
		_optionenBox = GetNode<Control>(OptionenBoxPath);
		_bestechenBox = GetNode<Control>(BestechenBoxPath);
		_numericTaler = GetNode<controls.NumericButtonWithSounds>(NumericTalerPath);
	}

	/// <summary>Aufruf über das Beziehungen-pflegen-Privileg (Modus 0). Fire-and-forget, da synchron.</summary>
	void IBeziehungPflegen.ShowDialog(int spielerID)
	{
		_ = ShowBeziehungen(spielerID);
	}

	private async Task ShowBeziehungen(int spielerID)
	{
		_spielerId = spielerID;
		_labelErklaerung.Text = "Wie wollt Ihr Eure Beziehung\nzu " + SW.Dynamisch.GetSpWithID(spielerID).GetName() + " verbessern?";

		ZeigeOptionen();

		await ShowAndAwait();
	}

	private void ZeigeOptionen()
	{
		_optionenBox.Visible = true;
		_bestechenBox.Visible = false;
	}

	private void _on_link_karten_pressed()
	{
		bool gespielt = SW.Dynamisch.KartenSpielen(_spielerId);

		// Wie im Original: nur bei erfolgreichem Kartenspiel schließt der Dialog.
		if (gespielt)
			Close(DialogResultGame.OK);
	}

	private void _on_link_bestechen_pressed()
	{
		// In die Bestechen-Ansicht wechseln; das Zahlenfeld auf 0..eigene Taler begrenzen.
		_numericTaler.MinimalerWert = 0;
		_numericTaler.MaximalerWert = SW.Dynamisch.GetAktHum().GetTaler();
		_numericTaler.Wert = 0;

		_optionenBox.Visible = false;
		_bestechenBox.Visible = true;
	}

	private void _on_button_bestechen_pressed()
	{
		int wert = _numericTaler.Wert;
		if (wert <= 0)
			return;

		SW.Dynamisch.Bestechen(_spielerId, wert);
		Close(DialogResultGame.OK);
	}

	private void _on_button_abbrechen_pressed()
	{
		ZeigeOptionen();
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
