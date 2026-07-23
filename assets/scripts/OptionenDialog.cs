using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Einstellungsfenster (Migration von frmEinstellungen): Häkchen für Musik/Tipps/Statistik und die
/// Anzeige von KI-Stützpunkt- und -Militärereignissen, drei Lautstärke-Regler (Musik/Effekt/Stimmen) und
/// die KI-Aggressivität als Vorgabe für neue Spiele. Die Werte werden in den ClientSettings gespeichert;
/// die Lautstärke wird sofort auf die Audio-Busse angewendet. Der Rechtsklick schließt das Fenster.
/// </summary>
public partial class OptionenDialog : Control
{
	private controls.CheckBoxWithSounds _checkMusikAus;
	private controls.CheckBoxWithSounds _checkTipps;
	private controls.CheckBoxWithSounds _checkStatistik;
	private controls.CheckBoxWithSounds _checkStuetzpunkt;
	private controls.CheckBoxWithSounds _checkMilitaer;

	private HSlider _sliderMusik;
	private HSlider _sliderEffekt;
	private HSlider _sliderStimmen;
	private Label _labelMusik;
	private Label _labelEffekt;
	private Label _labelStimmen;

	private controls.CheckBoxWithSounds _checkNiedrig;
	private controls.CheckBoxWithSounds _checkMittel;
	private controls.CheckBoxWithSounds _checkHoch;

	private bool _laedt;

	public override void _Ready()
	{
		_checkMusikAus = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckMusikAus");
		_checkTipps = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckTipps");
		_checkStatistik = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckStatistik");
		_checkStuetzpunkt = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckStuetzpunkt");
		_checkMilitaer = GetNode<controls.CheckBoxWithSounds>("Rahmen/VBoxChecks/CheckMilitaer");

		_sliderMusik = GetNode<HSlider>("Rahmen/VBoxSlider/SliderMusik");
		_sliderEffekt = GetNode<HSlider>("Rahmen/VBoxSlider/SliderEffekt");
		_sliderStimmen = GetNode<HSlider>("Rahmen/VBoxSlider/SliderStimmen");
		_labelMusik = GetNode<Label>("Rahmen/VBoxSlider/LabelMusik");
		_labelEffekt = GetNode<Label>("Rahmen/VBoxSlider/LabelEffekt");
		_labelStimmen = GetNode<Label>("Rahmen/VBoxSlider/LabelStimmen");

		_checkNiedrig = GetNode<controls.CheckBoxWithSounds>("Rahmen/HBoxAgg/CheckNiedrig");
		_checkMittel = GetNode<controls.CheckBoxWithSounds>("Rahmen/HBoxAgg/CheckMittel");
		_checkHoch = GetNode<controls.CheckBoxWithSounds>("Rahmen/HBoxAgg/CheckHoch");

		_checkMusikAus.Toggled += OnMusikAusgeschaltet;
		_checkTipps.Toggled += an => ClientSettings.TippsAnzeigen = an;
		_checkStatistik.Toggled += an => ClientSettings.StatistikAnzeigen = an;
		_checkStuetzpunkt.Toggled += an => ClientSettings.StuetzpunktereignisseKiAnzeigen = an;
		_checkMilitaer.Toggled += an => ClientSettings.MilitaerereignisseKiAnzeigen = an;

		_sliderMusik.ValueChanged += wert => OnLautstaerke(AudioEinstellungen.BusMusik, (int)wert);
		_sliderEffekt.ValueChanged += wert => OnLautstaerke(AudioEinstellungen.BusEffekt, (int)wert);
		_sliderStimmen.ValueChanged += wert => OnLautstaerke(AudioEinstellungen.BusStimmen, (int)wert);

		_checkNiedrig.Toggled += an => OnAggressivitaet(an, 0);
		_checkMittel.Toggled += an => OnAggressivitaet(an, 1);
		_checkHoch.Toggled += an => OnAggressivitaet(an, 2);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		Hide();
		SetProcessInput(false);
	}

	/// <summary>Öffnet das Einstellungsfenster und lädt die aktuellen Werte in die Steuerelemente.</summary>
	public void ShowDialog()
	{
		_laedt = true;

		_checkMusikAus.ButtonPressed = ClientSettings.MusikAusschalten;
		_checkTipps.ButtonPressed = ClientSettings.TippsAnzeigen;
		_checkStatistik.ButtonPressed = ClientSettings.StatistikAnzeigen;
		_checkStuetzpunkt.ButtonPressed = ClientSettings.StuetzpunktereignisseKiAnzeigen;
		_checkMilitaer.ButtonPressed = ClientSettings.MilitaerereignisseKiAnzeigen;

		_sliderMusik.Value = ClientSettings.MusikLautstaerke;
		_sliderEffekt.Value = ClientSettings.EffektLautstaerke;
		_sliderStimmen.Value = ClientSettings.StimmenLautstaerke;
		AktualisiereLautstaerkeLabel(_labelMusik, "Musik", ClientSettings.MusikLautstaerke);
		AktualisiereLautstaerkeLabel(_labelEffekt, "Effekt", ClientSettings.EffektLautstaerke);
		AktualisiereLautstaerkeLabel(_labelStimmen, "Stimmen", ClientSettings.StimmenLautstaerke);

		_checkNiedrig.ButtonPressed = ClientSettings.KiAggressivitaet == 0;
		_checkMittel.ButtonPressed = ClientSettings.KiAggressivitaet == 1;
		_checkHoch.ButtonPressed = ClientSettings.KiAggressivitaet == 2;

		_laedt = false;

		Show();
		SetProcessInput(true);
	}

	private void OnMusikAusgeschaltet(bool ausgeschaltet)
	{
		if (_laedt)
			return;

		ClientSettings.MusikAusschalten = ausgeschaltet;
		AudioEinstellungen.AlleAnwenden();
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

	private void OnAggressivitaet(bool angewaehlt, int stufe)
	{
		if (_laedt || !angewaehlt)
			return;

		ClientSettings.KiAggressivitaet = stufe;
	}

	private static void AktualisiereLautstaerkeLabel(Label label, string typ, int prozent)
	{
		label.Text = typ + " Lautstärke - " + prozent + " %";
	}
}
