using System.Threading.Tasks;

using Conspiratio.Lib.Allgemein;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Kreditangebot des Geldleihers, mit frei wählbarem Betrag.
///
/// <b>Warum der Betrag wählbar ist.</b> Bisher war das Angebot nur anzunehmen oder abzulehnen, und
/// seine Höhe ist ein Zehntel des Vermögens eines <em>zufälligen</em> Gläubigers – zu Spielbeginn 1 200
/// bis 12 000 Taler, ohne Bezug zum Bedarf des Kreditnehmers. Gemessen kostet die einzige lohnende
/// Investition eines Anfängers rund 2 600 Taler und wirft 49 % im Jahr ab; der Kredit kostet 10 bis
/// 20 %, wäre also ein gutes Geschäft. Nur zahlte, wer 12 000 nahm, um 2 600 einzusetzen, Zinsen auf
/// die vollen 12 000 – bei 20 % sind das 2 400 im Jahr gegen einen Betrieb, der 1 292 verdient. Daran
/// scheiterten Kredite im Frühspiel, nicht am Zinssatz.
///
/// Die Kostenzeile darunter rechnet den gewählten Betrag sofort in Zinsen je Jahr, Zinsen insgesamt und
/// die Schlussrate um. Gerade die ist wichtig: Die Tilgung ist endfällig und wird erzwungen, notfalls
/// ins Minus – das soll vor der Entscheidung dastehen und nicht danach.
/// </summary>
public partial class KreditDialog : DialogBase
{
	[Export]
	public NodePath LabelAngebotPath { get; set; }

	[Export]
	public NodePath NumericBetragPath { get; set; }

	[Export]
	public NodePath LabelKostenPath { get; set; }

	[Export]
	public NodePath ButtonAnnehmenPath { get; set; }

	[Export]
	public NodePath ButtonAblehnenPath { get; set; }

	[Export]
	public NodePath LabelAbsagenPath { get; set; }

	private Label _labelAngebot;
	private controls.NumericButtonWithSounds _numericBetrag;
	private Label _labelKosten;
	private controls.ButtonWithSounds _buttonAnnehmen;
	private controls.ButtonWithSounds _buttonAblehnen;
	private Label _labelAbsagen;

	private KreditAngebot _angebot;

	/// <summary>Der gewählte Betrag, oder 0 bei Ablehnung.</summary>
	private int _gewaehlterBetrag;

	protected override void OnReady()
	{
		_labelAngebot = GetNode<Label>(LabelAngebotPath);
		_numericBetrag = GetNode<controls.NumericButtonWithSounds>(NumericBetragPath);
		_labelKosten = GetNode<Label>(LabelKostenPath);
		_buttonAnnehmen = GetNode<controls.ButtonWithSounds>(ButtonAnnehmenPath);
		_buttonAblehnen = GetNode<controls.ButtonWithSounds>(ButtonAblehnenPath);
		_labelAbsagen = GetNode<Label>(LabelAbsagenPath);

		_numericBetrag.WertChanged += OnBetragChanged;
		_buttonAnnehmen.Pressed += OnAnnehmen;
		_buttonAblehnen.Pressed += OnAblehnen;
	}

	/// <summary>
	/// Solange der Dialog offen ist, ist eine Entscheidung verlangt: Ein Rechtsklick darf ihn nicht
	/// schließen, sonst verfiele das Angebot, ohne dass die Absage gezählt würde.
	/// </summary>
	protected override void OnNextOrClose()
	{
	}

	/// <summary>
	/// Zeigt das Angebot und liefert den gewählten Betrag zurück – 0, wenn der Spieler ablehnt.
	/// </summary>
	public async Task<int> ShowDialog(KreditAngebot angebot, int verbleibendeAbsagen)
	{
		_angebot = angebot;
		_gewaehlterBetrag = 0;

		_labelAngebot.Text = angebot.GetAngebotsKopf();

		_numericBetrag.TausenderTrenner = false;
		_numericBetrag.NurEinserSchritte = false;
		_numericBetrag.MaximaleStellen = angebot.Summe.ToString().Length;
		_numericBetrag.MinimalerWert = SchreibstubeManager.MindestKreditbetrag;
		_numericBetrag.MaximalerWert = angebot.Summe;

		// Voreingestellt die volle Summe: Das ist das Angebot, das der Geldleiher macht. Wer weniger
		// will, klickt es herunter – die Ziffernstellen des NumericButtons machen das in wenigen Klicks.
		_numericBetrag.Wert = angebot.Summe;
		AktualisiereKosten(angebot.Summe);

		_labelAbsagen.Text = verbleibendeAbsagen > 1
			? "Ihr könnt noch " + verbleibendeAbsagen + " Angebote ausschlagen."
			: "Schlagt Ihr auch dieses aus, ist der Geldleiher für dieses Jahr nicht mehr zu sprechen.";

		await ShowAndAwait();

		return _gewaehlterBetrag;
	}

	private void OnBetragChanged(int neuerWert)
	{
		AktualisiereKosten(neuerWert);
	}

	private void AktualisiereKosten(int betrag)
	{
		_labelKosten.Text = _angebot.GetKostenText(betrag);
	}

	private void OnAnnehmen()
	{
		_gewaehlterBetrag = _numericBetrag.Wert;
		Close(DialogResultGame.Yes);
	}

	private void OnAblehnen()
	{
		_gewaehlterBetrag = 0;
		Close(DialogResultGame.No);
	}
}
