using System.Linq;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Justiz;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Gerichtsverhandlung (Migration von CheckGerichtsVerhandlungen/GerichtsverhandlungDurchfuehren):
/// ist der aktive Spieler an einem Prozess beteiligt (als Angeklagter, Kläger oder Richter), wird dieser
/// hier auf dem Gerichts-Bildschirm abgewickelt – Vorstellung, Vorwürfe, drei Richter stimmen ab (der
/// Spieler per Ja/Nein-Dialog) und die Auswertung samt Strafe. Der Rechtsklick blättert weiter.
/// </summary>
public partial class GerichtDialog : Control
{
	[Export]
	public NodePath LabelHauptPath { get; set; }

	[Export]
	public NodePath LabelUrteilePath { get; set; }

	private Label _labelHaupt;
	private Label _labelUrteile;

	private TaskCompletionSource<bool> _weiter;

	public override void _Ready()
	{
		_labelHaupt = GetNode<Label>(LabelHauptPath);
		_labelUrteile = GetNode<Label>(LabelUrteilePath);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (_weiter != null && Input.IsActionPressed("ui_next_or_close"))
		{
			SoundManager.Instance.PlayRightClick();
			var tcs = _weiter;
			_weiter = null;
			tcs.TrySetResult(true);
		}
	}

	/// <summary>
	/// Wickelt alle Verhandlungen ab, an denen der aktive Spieler beteiligt ist. Gibt es keine, kehrt
	/// die Methode sofort zurück (ohne den Bildschirm zu zeigen).
	/// </summary>
	public async Task ShowGericht()
	{
		var manager = new GerichtsverhandlungManager();
		var indizes = manager.GetVerhandlungenMitAktivemSpieler().ToList();

		if (indizes.Count == 0)
			return;

		_labelHaupt.Text = "";
		_labelUrteile.Text = "";
		Show();
		SetProcessInput(true);

		foreach (int index in indizes)
			await FuehreVerhandlungDurch(manager, index);

		Hide();
		SetProcessInput(false);
	}

	private async Task FuehreVerhandlungDurch(GerichtsverhandlungManager manager, int index)
	{
		var info = manager.StarteVerhandlung(index);

		// Vorstellung des Prozesses
		SetzeHaupt(info.KlaegerName + " hat einen Prozess gegen " + info.AngeklagterName + "\ninitiiert. " + info.RollenText);
		await WarteAufWeiter();

		// Vorwürfe je Gesetzeskategorie
		await ZeigeKategorie(info.FinanzVorwuerfe);
		await ZeigeKategorie(info.StrafVorwuerfe);
		await ZeigeKategorie(info.KirchVorwuerfe);

		// Verteidigung des Angeklagten
		SetzeHaupt(manager.GetVerteidigung());
		await WarteAufWeiter();

		// Zeugen vernommen, Entscheidung
		SetzeHaupt("Das hohe Gericht hat alle Zeugen vernommen.\n Es kommt nun zu einer Entscheidung durch das Gericht.");
		await WarteAufWeiter();

		// Die drei Richter stimmen ab
		_labelUrteile.Text = "";

		for (int i = 0; i < GerichtsverhandlungManager.RichterAnzahl; i++)
		{
			bool schuldig;

			if (manager.IstRichterMensch(i))
			{
				SetProcessInput(false);
				schuldig = await SW.UI.YesNoQuestion.ShowDialogText(
					"Für welches Urteil wollt Ihr stimmen,\n " + manager.GetRichterName(i) + "?", "Schuldig", "Nicht schuldig") == DialogResultGame.Yes;

				if (Visible)
					SetProcessInput(true);
			}
			else
			{
				schuldig = manager.BerechneKiUrteil(i);
			}

			manager.SetzeUrteil(i, schuldig);

			SetzeHaupt(manager.GetRichterName(i) + " stimmt für...");
			await WarteAufWeiter();

			_labelUrteile.Text += manager.GetUrteilText(i) + "\n\n";
			await WarteAufWeiter();
		}

		// Ergebnis und ggf. Strafe
		var ergebnis = manager.WerteAus();

		SetzeHaupt(ergebnis.ErgebnisText);
		await WarteAufWeiter();

		if (!ergebnis.Freigesprochen)
		{
			SetzeHaupt(ergebnis.UrteilText);
			await WarteAufWeiter();

			SetzeHaupt(ergebnis.StrafeText);
			await WarteAufWeiter();
		}

		_labelUrteile.Text = "";
		manager.SchliesseVerhandlung();
	}

	private async Task ZeigeKategorie(VorwurfKategorie kategorie)
	{
		if (kategorie.Ueberschrift == null)
			return;

		SetzeHaupt(kategorie.Ueberschrift);
		await WarteAufWeiter();

		foreach (string vorwurf in kategorie.Vorwuerfe)
		{
			SetzeHaupt(vorwurf);
			await WarteAufWeiter();
		}
	}

	private void SetzeHaupt(string text)
	{
		_labelHaupt.Text = text;
	}

	private Task WarteAufWeiter()
	{
		_weiter = new TaskCompletionSource<bool>();
		return _weiter.Task;
	}
}
