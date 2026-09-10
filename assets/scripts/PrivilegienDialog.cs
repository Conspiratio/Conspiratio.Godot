using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Privilegienanzeige (Migration von PrivilegienAnzeigen): listet die Privilegien, die der aktive
/// Spieler durch Amt, Titel und Familienstand besitzt. Ein Klick führt das Privileg aus – passive
/// Privilegien zeigen eine Information, aktionierbare öffnen den zugehörigen Dialog.
/// </summary>
public partial class PrivilegienDialog : DialogBase
{
	[Export]
	public NodePath VBoxPrivilegienPath { get; set; }

	[Export]
	public NodePath LabelKeinePath { get; set; }

	[Export]
	public NodePath LinkZurueckPath { get; set; }

	[Export]
	public NodePath LinkWeiterPath { get; set; }

	[Export]
	public NodePath LabelSeitePath { get; set; }

	/// <summary>
	/// Privilegien je Seite – wie im WinForms-Original (<c>PrivilegienAnzeigen._maxPrivProSeite</c>).
	/// Geblättert statt gescrollt: Ein Rollbalken auf dem Pergament fällt optisch aus dem Rahmen.
	/// </summary>
	private const int ProSeite = 5;

	private VBoxContainer _vBoxPrivilegien;
	private Label _labelKeine;
	private controls.LinkButtonWithSounds _linkZurueck;
	private controls.LinkButtonWithSounds _linkWeiter;
	private Label _labelSeite;
	private PackedScene _linkButtonScene;

	/// <summary>Aktuelle Seite (0-basiert) der Privilegienliste.</summary>
	private int _seite;

	private PrivilegienManager _privilegienManager;
	private Main _main;

	protected override void OnReady()
	{
		_vBoxPrivilegien = GetNode<VBoxContainer>(VBoxPrivilegienPath);
		_labelKeine = GetNode<Label>(LabelKeinePath);
		_linkZurueck = GetNode<controls.LinkButtonWithSounds>(LinkZurueckPath);
		_linkWeiter = GetNode<controls.LinkButtonWithSounds>(LinkWeiterPath);
		_labelSeite = GetNode<Label>(LabelSeitePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
		_main = GetParentOrNull<Main>();
	}

	/// <summary>
	/// ID des Erpressten, dessen Privilegien gerade angezeigt werden (Issue #13); 0 = die eigenen.
	/// </summary>
	private int _erpresstesOpfer;

	public async Task ShowDialog(PrivilegienManager privilegienManager)
	{
		_privilegienManager = privilegienManager;
		_privilegienManager.AktualisierePrivilegien();
		_erpresstesOpfer = 0;
		_seite = 0;
		Fill();

		await ShowAndAwait();
	}

	private void Fill()
	{
		foreach (Node child in _vBoxPrivilegien.GetChildren())
		{
			_vBoxPrivilegien.RemoveChild(child);
			child.QueueFree();
		}

		// Im Fremdmodus (Issue #13) stehen statt der eigenen die Amtsprivilegien des Erpressten in der
		// Liste – plus der Eintrag, mit dem der Spieler zu seinen eigenen zurückkehrt.
		var privilegien = _erpresstesOpfer == 0
			? _privilegienManager.GetPrivilegien()
			: _privilegienManager.GetErpresstePrivilegien(_erpresstesOpfer);

		_labelKeine.Visible = privilegien.Count == 0;
		_vBoxPrivilegien.Visible = privilegien.Count > 0;

		// Die Liste kann zwischen zwei Aufrufen kürzer werden (z. B. „Amt niederlegen"): Dann auf die
		// letzte noch vorhandene Seite zurückgehen, statt eine leere anzuzeigen.
		int seiten = System.Math.Max(1, (privilegien.Count + ProSeite - 1) / ProSeite);
		_seite = System.Math.Clamp(_seite, 0, seiten - 1);

		for (int i = _seite * ProSeite; i < System.Math.Min(privilegien.Count, (_seite + 1) * ProSeite); i++)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = privilegien[i].Name;

			int id = privilegien[i].Id;
			button.Pressed += () => OnPrivilegPressed(id);

			_vBoxPrivilegien.AddChild(button);
		}

		// Die Blätterzeile erscheint nur, wenn es tatsächlich mehr als eine Seite gibt; „Zurück" und
		// „Weiter" jeweils nur dort, wo sie auch etwas bewirken.
		bool mehrereSeiten = seiten > 1;
		_labelSeite.Visible = mehrereSeiten;
		_labelSeite.Text = (_seite + 1) + "/" + seiten;
		_linkZurueck.Visible = mehrereSeiten && _seite > 0;
		_linkWeiter.Visible = mehrereSeiten && _seite < seiten - 1;
	}

	private void _on_link_zurueck_pressed()
	{
		_seite--;
		Fill();
	}

	private void _on_link_weiter_pressed()
	{
		_seite++;
		Fill();
	}

	private async void OnPrivilegPressed(int privilegId)
	{
		// Erpressung (Issue #13): Umschalten auf die Amtsprivilegien eines Erpressten und wieder zurück.
		if (privilegId == PrivilegienManager.EigenePrivilegienId)
		{
			_erpresstesOpfer = 0;
			_seite = 0;
			Fill();
			return;
		}

		int opferId = PrivilegienManager.GetErpressungsOpferId(privilegId);
		if (opferId != 0)
		{
			_erpresstesOpfer = opferId;
			_seite = 0;
			Fill();
			return;
		}

		// Die immer verfügbare Ahnentafel ist kein echtes Lib-Privileg, sondern öffnet den Stammbaum-Dialog.
		if (privilegId == PrivilegienManager.AhnentafelPrivilegId)
		{
			SetProcessInput(false);
			await _main.AhnentafelDialog.ShowDialog();

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// „Mätresse nehmen" (Issue #8): Rückfrage mit Kosten, dann Wirkung über den MaetresseManager.
		if (privilegId == PrivilegienManager.MaetressePrivilegId)
		{
			SetProcessInput(false);

			var maetresse = new MaetresseManager();

			if (!maetresse.KannMaetresseNehmen(out string grund))
			{
				await SW.UI.ShowText.ShowDialog(grund);
			}
			else if (await SW.UI.YesNoQuestion.ShowDialogText(maetresse.GetAngebotstext(), "Nehmen", "Ablehnen") == DialogResultGame.Yes)
			{
				maetresse.NimmMaetresse();
				SoundManager.Instance.PlayCoins();
				await SW.UI.ShowText.ShowDialog("Ihr habt Euch eine Mätresse genommen. Euer Ansehen und Eure Lebensfreude wachsen – möge die Diskretion Euch treu bleiben.");
			}

			Fill();

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// „Fechtunterricht nehmen" (Issue #17): einfacher Ja/Nein-Dialog, wiederholbar (Muster wie Mätresse).
		if (privilegId == PrivilegienManager.FechtunterrichtPrivilegId)
		{
			SetProcessInput(false);

			var fecht = new FechtDuellManager();

			while (fecht.KannFechtstundeBezahlen())
			{
				if (await SW.UI.YesNoQuestion.ShowDialogText(fecht.GetAngebotstext(), "Stunde nehmen", "Genug") != DialogResultGame.Yes)
					break;

				fecht.NimmFechtstunde(out _);
				SoundManager.Instance.PlayCoins();
			}

			if (!fecht.KannFechtstundeBezahlen())
				await SW.UI.ShowText.ShowDialog("Für eine (weitere) Fechtstunde fehlen Euch die Taler.");

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// „Faktor anstellen" bzw. „Faktor befragen": ein Eintrag für beides, weil immer nur
		// einer der Fälle zutrifft. Wer keinen hat, bekommt die Frage; wer einen hat, den Bericht.
		if (privilegId == PrivilegienManager.FaktorPrivilegId)
		{
			SetProcessInput(false);
			await ZeigeFaktor();
			Fill();

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// „Handwerkslehre nehmen": Unterricht in einer Ware, die der Spieler auch betreibt.
		if (privilegId == PrivilegienManager.HandwerkslehrePrivilegId)
		{
			SetProcessInput(false);
			await ZeigeHandwerkslehre();

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// „Zum Duell fordern" (Issue #17): öffnet die Personen-Karte (Modus 14) zur Auswahl eines
		// Amtsträgers. Das Privilegienfenster wird geschlossen; die Duell-Abwicklung erledigt die Lib
		// (KontrahentenManager Modus 14).
		if (privilegId == PrivilegienManager.DuellPrivilegId)
		{
			Close(DialogResultGame.OK);
			SW.UI.PolitischeWeltkarteDialog.ShowDialogModus(14);
			return;
		}

		// Führt das Privileg aus (Infotext oder öffnet den zugehörigen Dialog). Anschließend die Liste
		// neu aufbauen, da sich die Privilegien ändern können (z. B. Amt niederlegen).
		_privilegienManager.FuehreAus(privilegId);
		Fill();
	}

	/// <summary>
	/// Der Handwerksunterricht: Die Ware wird durchgeblättert wie die Karawane beim Transport –
	/// „Stunde nehmen" unterrichtet, „Nächste Ware" blättert weiter, ein Rechtsklick beendet. Ohne
	/// dieses dritte Ergebnis (<c>Cancel</c>) gäbe es aus einer Blätterschleife keinen Ausweg; bei nur
	/// einer Ware heißt der zweite Knopf deshalb schlicht „Genug", wie beim Fechtunterricht.
	/// </summary>
	private static async Task ZeigeHandwerkslehre()
	{
		var fertigkeit = new ProduktionsfertigkeitManager();
		var waren = ProduktionsfertigkeitManager.GetBetriebeneWaren();

		if (waren.Count == 0)
		{
			await SW.UI.ShowText.ShowDialog("Ihr betreibt keine Produktion, in der Euch ein Meister\n" +
			                                "etwas zeigen könnte.");
			return;
		}

		var spieler = SW.Dynamisch.GetAktHum();
		int i = 0;

		while (true)
		{
			int ware = waren[i];
			int preis = fertigkeit.GetLehrstundenPreis(ware);

			string frage = "Ein Meister der " + SW.Dynamisch.GetRohstoffwithID(ware).GetRohName() +
			               "-Herstellung\nbietet Euch Unterricht an.\n\n" +
			               "Euer Können: " +
			               ProduktionsfertigkeitManager.FertigkeitAlsText(spieler.GetProduktionsfertigkeit(ware)) +
			               "\nDie Stunde kostet " + preis.ToStringGeld() + ".";

			var antwort = await SW.UI.YesNoQuestion.ShowDialogText(frage, "Stunde nehmen",
				waren.Count > 1 ? "Nächste Ware" : "Genug");

			if (antwort == DialogResultGame.Yes)
			{
				if (fertigkeit.NimmLehrstunde(ware, out string meldung))
					SoundManager.Instance.PlayCoins();
				else
				{
					await SW.UI.ShowText.ShowDialog(meldung);
					break;
				}

				continue;
			}

			// Bei einer einzigen Ware ist der zweite Knopf das Ende, sonst blättert er weiter.
			if (antwort != DialogResultGame.No || waren.Count == 1)
				break;

			i = (i + 1) % waren.Count;
		}
	}

	/// <summary>
	/// Der Faktor: ohne einen im Dienst die Frage nach der Anstellung, sonst sein Bericht. Der Lohn
	/// wird in der Frage genannt, denn er ist der ganze Preis – fällig wird er erst mit der
	/// Jahresabrechnung, und dort steht er als eigener Posten.
	/// </summary>
	private async Task ZeigeFaktor()
	{
		var faktor = new FaktorManager();

		if (faktor.IstAngestellt())
		{
			await _main.FaktorDialog.ShowDialog();
			return;
		}

		if (await SW.UI.YesNoQuestion.ShowDialogText(
			    "Ein Faktor bietet Euch seine Dienste an.\n\n" +
			    "Er vergleicht für Euch die Märkte aller Städte\nund warnt, wo Ihr sie überfüllt.\n\n" +
			    "Sein Lohn beträgt " + faktor.GetJahreslohn().ToStringGeld() + " im Jahr\n" +
			    "und wächst mit der Zahl Eurer Standorte.",
			    "In Dienst nehmen", "Nicht nötig") != DialogResultGame.Yes)
			return;

		faktor.StelleAn(out string meldung);
		await SW.UI.ShowText.ShowDialog(meldung);
		await _main.FaktorDialog.ShowDialog();
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
