using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Hinterzimmer;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Kartenspiel "17 und 4" (Migration von KartenSpielen): Hat der Spieler im Hinterzimmer eine
/// KI zum Kartenspielen eingeladen, wird die Runde hier abgewickelt – Einsatz festlegen, austeilen,
/// Karten kaufen (Sicher!/Lieber nicht...) und die Auswertung. Der Rechtsklick (bzw. Esc) blättert
/// wie im Original durch die Meldungen. Die Spiellogik liegt in der Lib-Klasse Kartenspiel.
/// </summary>
public partial class KartenspielDialog : Control
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath HBoxEinsatzPath { get; set; }

	[Export]
	public NodePath NumericEinsatzPath { get; set; }

	[Export]
	public NodePath HBoxJaNeinPath { get; set; }

	private Label _labelText;
	private Control _hboxEinsatz;
	private controls.NumericButtonWithSounds _numericEinsatz;
	private Control _hboxJaNein;

	private string _text;
	private TaskCompletionSource<bool> _weiter;
	private TaskCompletionSource<bool> _okGeklickt;
	private TaskCompletionSource<bool> _jaNein;

	public override void _Ready()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_hboxEinsatz = GetNode<Control>(HBoxEinsatzPath);
		_numericEinsatz = GetNode<controls.NumericButtonWithSounds>(NumericEinsatzPath);
		_hboxJaNein = GetNode<Control>(HBoxJaNeinPath);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		// Der Rechtsklick blättert nur weiter, wenn gerade auf eine Blättern-Eingabe gewartet wird
		// (das Kartenspiel lässt sich – wie im Original – nicht mittendrin abbrechen).
		if (_weiter != null && Input.IsActionPressed("ui_next_or_close"))
		{
			SoundManager.Instance.PlayRightClick();
			var tcs = _weiter;
			_weiter = null;
			tcs.TrySetResult(true);
		}
	}

	/// <summary>
	/// Wickelt eine anstehende Kartenspiel-Runde ab. Findet keine statt, kehrt die Methode sofort zurück.
	/// </summary>
	public async Task ShowKartenspiel()
	{
		var spiel = new Kartenspiel();

		if (!spiel.FindetKartenspielStatt)
			return;

		spiel.InitiiereKartenspielUndErmittleGegner();

		_text = "";
		_hboxEinsatz.Visible = false;
		_hboxJaNein.Visible = false;
		Show();
		SetProcessInput(true);

		// Zu wenig Taler: der Gegner verlässt verärgert den Tisch.
		if (!spiel.HatSpielerGenugTaler())
		{
			SetzeText(spiel.LehneMangelsTalerAb());
			await WarteAufWeiter();
			Schliessen();
			return;
		}

		// Einsatz festlegen
		_numericEinsatz.MinimalerWert = spiel.MinEinsatz;
		_numericEinsatz.MaximalerWert = spiel.MaxEinsatz;
		_numericEinsatz.Wert = spiel.MinEinsatz;

		SetzeText("Ihr habt Euch entschieden, in diesem Jahr mit " + spiel.GegnerName + " eine Runde 17 und 4 zu spielen, wobei " +
		          spiel.GegnerErSie + " die Aufgabe des Bankhalters übernimmt.\n\nNach einem Blick in Euren Geldbeutel legt " +
		          spiel.GegnerName + " einen Mindesteinsatz von " + spiel.MinEinsatz.ToStringGeld() + " fest.\n\nWie viel wollt Ihr setzen?");

		_hboxEinsatz.Visible = true;
		await WarteAufOk();
		_hboxEinsatz.Visible = false;

		int einsatz = _numericEinsatz.Wert;
		spiel.SetzeEinsatz(einsatz);
		HaengeAn("\n\n\nIhr entschließt Euch " + einsatz.ToStringGeld() + " zu setzen.");
		await WarteAufWeiter();

		// Austeilen
		SetzeText(spiel.GegnerName + " mischt die Karten und beginnt auszuteilen.");
		var start = spiel.Austeilen();
		HaengeAn(" Euer Kontrahent erhält als erste Karte " + start.GegnerKarteName + " und besitzt somit " + start.GegnerPunkte + " Punkte.");
		await WarteAufWeiter();
		HaengeAn("\nIhr erhaltet Eure ersten beiden Karten, " + start.EigeneKarte1Name + " und " + start.EigeneKarte2Name +
		         " und besitzt damit schon " + start.EigenePunkte + " Punkte.");
		HaengeAn(" Wollt Ihr noch eine weitere Karte ziehen?");

		// Der Spieler kauft Karten
		bool ueberkauft = false;

		while (true)
		{
			if (!await WarteAufJaNein())
				break;

			var zug = spiel.SpielerZiehtKarte();
			HaengeAn("\nIhr erhaltet noch " + zug.KarteName + " und besitzt damit " + zug.EigenePunkte + " Punkte.");

			if (zug.Status == KartenZugStatus.Ueberkauft)
			{
				ueberkauft = true;
				break;
			}

			if (zug.Status == KartenZugStatus.Genau21)
			{
				await WarteAufWeiter();
				break;
			}

			HaengeAn("\nWollt Ihr noch eine weitere Karte ziehen?");
		}

		// Auswertung
		if (ueberkauft)
		{
			HaengeAn("\n" + spiel.SpielerUeberkauftAuswerten());
			await WarteAufWeiter();
		}
		else
		{
			SetzeText("");

			var auswertung = spiel.GegnerZiehtUndWertetAus();

			foreach (string zug in auswertung.GegnerZuege)
			{
				HaengeAn("\n" + zug);
				await WarteAufWeiter();
			}

			HaengeAn("\n" + auswertung.Ergebnis);
			await WarteAufWeiter();
		}

		Schliessen();
	}

	private void SetzeText(string text)
	{
		_text = text;
		_labelText.Text = _text;
	}

	private void HaengeAn(string text)
	{
		_text += text;
		_labelText.Text = _text;
	}

	private Task WarteAufWeiter()
	{
		_weiter = new TaskCompletionSource<bool>();
		return _weiter.Task;
	}

	private Task WarteAufOk()
	{
		_okGeklickt = new TaskCompletionSource<bool>();
		return _okGeklickt.Task;
	}

	private Task<bool> WarteAufJaNein()
	{
		_hboxJaNein.Visible = true;
		_jaNein = new TaskCompletionSource<bool>();
		return _jaNein.Task;
	}

	private void _on_button_ok_pressed()
	{
		_okGeklickt?.TrySetResult(true);
	}

	private void _on_button_ja_pressed()
	{
		_hboxJaNein.Visible = false;
		var tcs = _jaNein;
		_jaNein = null;
		tcs?.TrySetResult(true);
	}

	private void _on_button_nein_pressed()
	{
		_hboxJaNein.Visible = false;
		var tcs = _jaNein;
		_jaNein = null;
		tcs?.TrySetResult(false);
	}

	private void Schliessen()
	{
		Hide();
		SetProcessInput(false);
	}
}
