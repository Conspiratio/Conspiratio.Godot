using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Hinterzimmer (Migration des Hinterzimmer-Bereichs aus dem WinForms-Client): von hier aus werden
/// die verdeckten Aktionen gegen Amtsträger eingeleitet – Beziehungen pflegen, Sabotage, Anschwärzen,
/// Spionage und Ermordung. Jeder Bereich öffnet die Weltkarte im passenden Modus; das Ziel wird über
/// die Ämter-Ebene oder die Kontrahenten-Liste gewählt. Rechtsklick führt zurück ins Kontor.
/// </summary>
public partial class Hinterzimmer : Control
{
	private static readonly string[] AreaNamen =
		{ "AreaBeziehungen", "AreaSabotage", "AreaAnschwaerzen", "AreaSpionage", "AreaErmordung" };

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;
	private Main _main;

	public override void _Ready()
	{
		_labelPlayerNameAndOffice = GetNode<Label>("LabelPlayerNameAndOffice");
		_labelPlaceDate = GetNode<Label>("LabelPlaceDate");
		_labelTaler = GetNode<Label>("LabelTaler");

		// Die Klickbereiche sind unsichtbar; ihre goldene Beschriftung erscheint nur bei MouseOver.
		foreach (string areaName in AreaNamen)
		{
			var area = GetNode<Button>(areaName);
			area.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			var label = GetNode<Label>("Label" + areaName.Substring("Area".Length));
			area.MouseEntered += () => label.Visible = true;
			area.MouseExited += () => label.Visible = false;
		}

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseHinterzimmer();
	}

	/// <summary>Öffnet das Hinterzimmer für den aktiven Spieler.</summary>
	public void ShowHinterzimmer()
	{
		UpdateHud();
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Hinterzimmer);
		Show();
		SetProcessInput(true);
	}

	/// <summary>Kehrt aus der Weltkarte ins Hinterzimmer zurück.</summary>
	public void ReturnFromKarte()
	{
		UpdateHud();
		Show();
		SetProcessInput(true);
	}

	private void CloseHinterzimmer()
	{
		Hide();
		SetProcessInput(false);
		_main.Kontor.ReturnFromStadt();
	}

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Hinterzimmer A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert();
	}

	// Jeder Bereich öffnet die Weltkarte im zugehörigen Modus; nach dem Schließen kehrt das
	// Hinterzimmer wieder zurück. Die Flaggen (eigene Häuser/Werkstätten) werden eingeblendet.
	private async void _on_area_beziehungen_pressed() => await OeffneKarte(0);
	private async void _on_area_sabotage_pressed() => await OeffneKarte(1);
	private async void _on_area_anschwaerzen_pressed() => await OeffneKarte(2);
	private async void _on_area_spionage_pressed() => await OeffneKarte(3);
	private async void _on_area_ermordung_pressed() => await OeffneKarte(4);

	private async System.Threading.Tasks.Task OeffneKarte(int modus)
	{
		SetProcessInput(false);
		Hide();

		await _main.Weltkarte.OeffnePersonenKarte(modus, true);

		ReturnFromKarte();
	}
}
