using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Einstellungsfenster (Migration von frmEinstellungen): Häkchen für Musik/Tipps/Statistik und die
/// Anzeige von KI-Stützpunkt- und -Militärereignissen, drei Lautstärke-Regler (Musik/Effekt/Stimmen) und
/// die KI-Aggressivität als Vorgabe für neue Spiele. Die Werte werden in den ClientSettings gespeichert;
/// die Lautstärke wird sofort auf die Audio-Busse angewendet. Der Rechtsklick schließt das Fenster.
/// </summary>
public partial class OptionenDialog : DialogBase
{
	private controls.CheckBoxWithSounds _checkMusikAus;
	private controls.CheckBoxWithSounds _checkTipps;
	private controls.CheckBoxWithSounds _checkStatistik;
	private controls.CheckBoxWithSounds _checkStuetzpunkt;
	private controls.CheckBoxWithSounds _checkMilitaer;
	private controls.CheckBoxWithSounds _checkDuelle;
	private controls.CheckBoxWithSounds _checkVollbild;

	private HSlider _sliderMusik;
	private HSlider _sliderEffekt;
	private HSlider _sliderStimmen;
	private Label _labelMusik;
	private Label _labelEffekt;
	private Label _labelStimmen;

	private HSlider _sliderKiAggressivitaet;
	private Label _labelAgg;

	private bool _laedt;

	protected override void OnReady()
	{
		_checkMusikAus = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckMusikAus");
		_checkTipps = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckTipps");
		_checkStatistik = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckStatistik");
		_checkStuetzpunkt = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckStuetzpunkt");
		_checkMilitaer = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckMilitaer");
		_checkDuelle = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckDuelle");
		_checkVollbild = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckVollbild");

		_sliderMusik = GetNode<HSlider>("Rahmen/VBoxSlider/SliderMusik");
		_sliderEffekt = GetNode<HSlider>("Rahmen/VBoxSlider/SliderEffekt");
		_sliderStimmen = GetNode<HSlider>("Rahmen/VBoxSlider/SliderStimmen");
		_labelMusik = GetNode<Label>("Rahmen/VBoxSlider/LabelMusik");
		_labelEffekt = GetNode<Label>("Rahmen/VBoxSlider/LabelEffekt");
		_labelStimmen = GetNode<Label>("Rahmen/VBoxSlider/LabelStimmen");

		_sliderKiAggressivitaet = GetNode<HSlider>("Rahmen/SliderKiAggressivitaet");
		_labelAgg = GetNode<Label>("Rahmen/LabelAgg");

		_checkMusikAus.Toggled += OnMusikAusgeschaltet;
		_checkTipps.Toggled += an => ClientSettings.TippsAnzeigen = an;
		_checkStatistik.Toggled += an => ClientSettings.StatistikAnzeigen = an;
		_checkStuetzpunkt.Toggled += an => ClientSettings.StuetzpunktereignisseKiAnzeigen = an;
		_checkMilitaer.Toggled += an => ClientSettings.MilitaerereignisseKiAnzeigen = an;
		_checkDuelle.Toggled += an => ClientSettings.DuelleInteraktiv = an;
		_checkVollbild.Toggled += OnVollbildGeaendert;

		_sliderMusik.ValueChanged += wert => OnLautstaerke(AudioEinstellungen.BusMusik, (int)wert);
		_sliderEffekt.ValueChanged += wert => OnLautstaerke(AudioEinstellungen.BusEffekt, (int)wert);
		_sliderStimmen.ValueChanged += wert => OnLautstaerke(AudioEinstellungen.BusStimmen, (int)wert);

		_sliderKiAggressivitaet.ValueChanged += OnKiAggressivitaetGeaendert;
	}

	/// <summary>Öffnet das Einstellungsfenster und lädt die aktuellen Werte in die Steuerelemente.</summary>
	public Task ShowDialog()
	{
		_laedt = true;

		_checkMusikAus.ButtonPressed = ClientSettings.MusikAusschalten;
		_checkTipps.ButtonPressed = ClientSettings.TippsAnzeigen;
		_checkStatistik.ButtonPressed = ClientSettings.StatistikAnzeigen;
		_checkStuetzpunkt.ButtonPressed = ClientSettings.StuetzpunktereignisseKiAnzeigen;
		_checkMilitaer.ButtonPressed = ClientSettings.MilitaerereignisseKiAnzeigen;
		_checkDuelle.ButtonPressed = ClientSettings.DuelleInteraktiv;
		_checkVollbild.ButtonPressed = ClientSettings.Vollbild;

		_sliderMusik.Value = ClientSettings.MusikLautstaerke;
		_sliderEffekt.Value = ClientSettings.EffektLautstaerke;
		_sliderStimmen.Value = ClientSettings.StimmenLautstaerke;
		AktualisiereLautstaerkeLabel(_labelMusik, "Musik", ClientSettings.MusikLautstaerke);
		AktualisiereLautstaerkeLabel(_labelEffekt, "Effekt", ClientSettings.EffektLautstaerke);
		AktualisiereLautstaerkeLabel(_labelStimmen, "Stimmen", ClientSettings.StimmenLautstaerke);

		_sliderKiAggressivitaet.Value = ClientSettings.KiAggressivitaetProzent;
		AktualisiereKiAggressivitaetLabel(ClientSettings.KiAggressivitaetProzent);

		_laedt = false;

		return ShowAndAwait();
	}

	private void OnMusikAusgeschaltet(bool ausgeschaltet)
	{
		if (_laedt)
			return;

		ClientSettings.MusikAusschalten = ausgeschaltet;
		AudioEinstellungen.AlleAnwenden();
	}

	private void OnVollbildGeaendert(bool vollbild)
	{
		if (_laedt)
			return;

		ClientSettings.Vollbild = vollbild;
		FensterEinstellungen.AlleAnwenden();
	}

	private void OnLautstaerke(string bus, int prozent)
	{
		if (bus == AudioEinstellungen.BusMusik)
		{
			ClientSettings.MusikLautstaerke = prozent;
			AktualisiereLautstaerkeLabel(_labelMusik, "Musik", prozent);
		}
		else if (bus == AudioEinstellungen.BusEffekt)
		{
			ClientSettings.EffektLautstaerke = prozent;
			AktualisiereLautstaerkeLabel(_labelEffekt, "Effekt", prozent);
		}
		else
		{
			ClientSettings.StimmenLautstaerke = prozent;
			AktualisiereLautstaerkeLabel(_labelStimmen, "Stimmen", prozent);
		}

		if (_laedt)
			return;

		AudioEinstellungen.SetLautstaerke(bus, prozent);

		// "Musik ausschalten" hat weiterhin Vorrang.
		if (bus == AudioEinstellungen.BusMusik && ClientSettings.MusikAusschalten)
			AudioEinstellungen.AlleAnwenden();
	}

	private void OnKiAggressivitaetGeaendert(double wert)
	{
		int prozent = (int)wert;
		AktualisiereKiAggressivitaetLabel(prozent);

		if (_laedt)
			return;

		ClientSettings.KiAggressivitaetProzent = prozent;

		// Zusätzlich in den laufenden Spielstand schreiben, damit der Regler sofort wirkt und nicht
		// erst im nächsten neuen Spiel. Ohne laufendes Spiel ist der Spielstand ein Platzhalter, der
		// beim Anlegen ohnehin aus den ClientSettings überschrieben wird – eine Fallunterscheidung
		// braucht es daher nicht.
		SW.Dynamisch.Spielstand.Einstellungen.KiAggressivitaetProzent = prozent;
	}

	private void AktualisiereKiAggressivitaetLabel(int prozent)
	{
		_labelAgg.Text = "Aggressivität der KI-Spieler: " + prozent + " %";
	}

	private static void AktualisiereLautstaerkeLabel(Label label, string typ, int prozent)
	{
		label.Text = typ + " Lautstärke - " + prozent + " %";
	}
}
