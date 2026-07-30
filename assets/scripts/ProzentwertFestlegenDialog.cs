using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Privilegien.ProzentwertFestlegen;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Prozentwert-festlegen-Dialog (Migration von ProzentwertFestlegenForm): abhängig von der
/// ProzentwertArt legt der Spieler die Umsatzsteuer (Bürgermeister), den Zollsatz (Zollburg) oder
/// verbessert Sicherheit/Zustand/Kapazität eines Stützpunkts fest. Steuer/Zoll werden live übernommen,
/// die Stützpunkt-Verbesserungen kosten Taler und werden per Auftrag ausgeführt. Logik: Lib-Manager.
/// </summary>
public partial class ProzentwertFestlegenDialog : DialogBase, IProzentwertFestlegenDialog
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath NumericWertPath { get; set; }

	[Export]
	public NodePath LabelSuffixPath { get; set; }

	[Export]
	public NodePath LabelKostenPath { get; set; }

	[Export]
	public NodePath ButtonAuftragPath { get; set; }

	private Label _labelText;
	private controls.NumericButtonWithSounds _numericWert;
	private Label _labelSuffix;
	private Label _labelKosten;
	private controls.ButtonWithSounds _buttonAuftrag;

	private ProzentwertFestlegenManager _manager;
	private int _aktuelleKosten;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_numericWert = GetNode<controls.NumericButtonWithSounds>(NumericWertPath);
		_labelSuffix = GetNode<Label>(LabelSuffixPath);
		_labelKosten = GetNode<Label>(LabelKostenPath);
		_buttonAuftrag = GetNode<controls.ButtonWithSounds>(ButtonAuftragPath);

		_numericWert.WertChanged += OnWertChanged;
		_buttonAuftrag.Pressed += OnAuftragPressed;
	}

	/// <summary>
	/// Aufruf über die Prozentwert-Privilegien. Öffnet den Dialog asynchron (fire-and-forget), da die
	/// Schnittstelle synchron (void) ist.
	/// </summary>
	void IProzentwertFestlegenDialog.ShowDialog(ProzentwertArt prozentwertArt, int zielStuetzpunktID)
	{
		_ = ShowProzentwert(prozentwertArt, zielStuetzpunktID);
	}

	private async Task ShowProzentwert(ProzentwertArt art, int zielStuetzpunktID)
	{
		_manager = new ProzentwertFestlegenManager(art, zielStuetzpunktID);
		_aktuelleKosten = 0;

		_labelText.Text = _manager.GetLabelText();
		_labelSuffix.Text = _manager.GetSuffixText();

		_numericWert.MaximaleStellen = _manager.GetMaximaleStellen();
		_numericWert.MinimalerWert = _manager.GetMinWert();
		_numericWert.MaximalerWert = _manager.GetMaxWert();
		_numericWert.Wert = _manager.GetStartWert();

		// Kostenanzeige und Auftrag-Button nur bei den Stützpunkt-Verbesserungen
		bool kostenModus = _manager.IstKostenModus;
		_labelKosten.Visible = kostenModus;
		_buttonAuftrag.Visible = kostenModus;
		if (kostenModus)
			_labelKosten.Text = "Kosten: " + 0.ToStringGeld();

		await ShowAndAwait();
	}

	private void OnWertChanged(int neuerWert)
	{
		if (_manager.IstKostenModus)
		{
			_aktuelleKosten = _manager.BerechneKosten(neuerWert);
			_labelKosten.Text = "Kosten: " + _aktuelleKosten.ToStringGeld();
		}
		else
		{
			// Umsatzsteuer/Zollsatz werden sofort übernommen
			_manager.SetzeWertLive(neuerWert);
		}
	}

	private async void OnAuftragPressed()
	{
		if (_aktuelleKosten <= 0)
			return;

		SetProcessInput(false);

		if (!_manager.KannBezahlen(_aktuelleKosten))
		{
			await SW.UI.ShowText.ShowDialog(_manager.GetNichtGenugGoldMeldung(_aktuelleKosten));
			if (Visible)
				SetProcessInput(true);
			return;
		}

		string meldung = _manager.FuehreAuftragAus(_numericWert.Wert, _aktuelleKosten);
		await SW.UI.ShowText.ShowDialog(meldung);
		Close(DialogResultGame.OK);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
