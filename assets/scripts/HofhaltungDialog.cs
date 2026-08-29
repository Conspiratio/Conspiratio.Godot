using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Spieler wählt, wie aufwendig er Hof hält: sparsam, standesgemäß oder aufwendig. Der Aufwand wird
/// in der Jahresabrechnung fällig und schlägt sich in permanentem Ansehen nieder – nach oben wie nach
/// unten. Gespeichert wird die Abweichung von der Mitte (-1 bis +1), damit ein alter Spielstand, aus dem
/// das Feld als 0 ankommt, „standesgemäß" bedeutet.
///
/// Ein titelloser Spieler hält keinen Hof: Sein Jahresaufwand ist 0, alle drei Stufen kosten dann
/// nichts. Der Dialog sagt das ausdrücklich, statt dreimal „0 Taler" anzubieten.
/// </summary>
public partial class HofhaltungDialog : DialogBase
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath VBoxStufenPath { get; set; }

	private Label _labelText;
	private VBoxContainer _vBoxStufen;
	private PackedScene _linkButtonScene;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_vBoxStufen = GetNode<VBoxContainer>(VBoxStufenPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	// Solange die Auswahl offen ist, darf ein Rechtsklick nichts tun – es muss ein Knopf gewählt werden.
	// Sonst bliebe offen, ob der Spieler die Stufe geändert hat oder nicht.
	protected override void OnNextOrClose() { }

	public Task ShowDialog()
	{
		var spieler = SW.Dynamisch.GetAktHum();
		int standesgemaess = SW.Statisch.GetTitelX(spieler.GetTitel()).GetJahresaufwand();
		int abweichung = spieler.GetHofhaltungAbweichung();

		// Die Wahl kostet sichtbar Taler und bringt Geltung – also muss auch die Geltung sichtbar sein,
		// sonst wägt der Spieler eine Zahl gegen ein Gefühl ab. Gezeigt wird das Gesamtansehen, weil
		// genau dieser Wert gelesen wird, wo Ansehen zählt: Wahl, Gericht, Stützpunkt, Schuldenschwelle.
		string ansehenZeile = "\nEuer Ansehen beträgt derzeit " + spieler.GetAnsehen() + ".";

		_labelText.Text = standesgemaess > 0
			? "Wie wollt Ihr Hof halten?\nStandesgemäß kostet Euch das " + standesgemaess.ToStringGeld()
			  + " im Jahr.\nWer mehr Aufwand treibt, als sein Stand verlangt, gewinnt an Geltung; wer spart, verliert."
			  + ansehenZeile
			: "Ohne Adelstitel haltet Ihr keinen Hof – der Aufwand kostet Euch derzeit nichts.\n"
			  + "Mit dem ersten Titel wird die Wahl spürbar." + ansehenZeile;

		foreach (Node kind in _vBoxStufen.GetChildren())
		{
			_vBoxStufen.RemoveChild(kind);
			kind.QueueFree();
		}

		ErzeugeStufe("Sparsam", -1, standesgemaess, abweichung);
		ErzeugeStufe("Standesgemäß", 0, standesgemaess, abweichung);
		ErzeugeStufe("Aufwendig", 1, standesgemaess, abweichung);

		return ShowAndAwait();
	}

	private void ErzeugeStufe(string bezeichnung, int abweichung, int standesgemaess, int aktuelleAbweichung)
	{
		int kosten = standesgemaess * AbrechnungsManager.HofhaltungFaktorProzent(abweichung) / 100;

		var knopf = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
		knopf.Text = bezeichnung + " (" + kosten.ToStringGeld() + " im Jahr)"
		             + (abweichung == aktuelleAbweichung ? " – derzeit" : "");
		knopf.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		knopf.Pressed += () => OnStufeGewaehlt(abweichung);

		_vBoxStufen.AddChild(knopf);
	}

	private void OnStufeGewaehlt(int abweichung)
	{
		SW.Dynamisch.GetAktHum().SetHofhaltungAbweichung(abweichung);
		Close(DialogResultGame.OK);
	}
}
