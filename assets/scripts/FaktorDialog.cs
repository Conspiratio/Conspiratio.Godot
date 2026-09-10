using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Bericht des Faktors: alle Städte für eine Ware nebeneinander, dazu die Prognose, wie lange die
/// eigene Lieferung den jeweiligen Markt noch verträgt.
///
/// <b>Diesen Vergleich gibt es sonst nirgends.</b> Die Zielstadt eines Exports wird über einen
/// Zahlen-Knopf einzeln durchgeschaltet; wer wissen will, wo eine Ware am meisten einbringt, muss
/// vierzehn Städte nacheinander aufsuchen. Genau diese Bequemlichkeit ist es, die der Faktor verkauft
/// – nicht die Erklärung des Marktabschlags, die seit jeher gratis im Tooltip der Preiszeile steht.
///
/// Die Ware wird durchgeblättert statt aus einer Liste gewählt: Es sind zwanzig, und eine Liste
/// bräuchte den Platz, den die Tabelle hat.
/// </summary>
public partial class FaktorDialog : DialogBase
{
	/// <summary>
	/// Spaltenbreiten in Entwurfspixeln. Zusammen mit dem Spaltenabstand (4 x 24) ergeben sie 926 und
	/// bleiben damit in der hellen Pergamentfläche, die von den 1100 des Rahmens nur etwa 30..1018
	/// ausmacht – der ausgefranste Rand der Textur trägt keine Schrift. Sie sind <b>Mindestbreiten</b>:
	/// Ein längerer Text sprengt die Spalte und schiebt die Tabelle aus dem Pergament, weshalb die
	/// Überschriften und die Prognosezellen kurz gehalten sind. Der erste Entwurf lief mit
	/// „Bis zum Höchstabschlag“ und „nie (Ihr liefert nicht mehr als der Bedarf)“ um 500 px über den
	/// Rand – im Bildnachweis gesehen, nicht im Code.
	/// </summary>
	private static readonly int[] Spaltenbreiten = { 190, 170, 190, 120, 160 };

	[Export] public NodePath LabelTitlePath { get; set; }
	[Export] public NodePath LinkButtonWarePath { get; set; }
	[Export] public NodePath GridContainerMaerktePath { get; set; }
	[Export] public NodePath LabelHinweisPath { get; set; }
	[Export] public NodePath LinkButtonEntlassenPath { get; set; }
	[Export] public NodePath LinkButtonClosePath { get; set; }

	private Label _labelTitle;
	private controls.LinkButtonWithSounds _linkButtonWare;
	private GridContainer _gridContainerMaerkte;
	private Label _labelHinweis;
	private controls.LinkButtonWithSounds _linkButtonEntlassen;
	private controls.LinkButtonWithSounds _linkButtonClose;

	private readonly FaktorManager _faktor = new FaktorManager();
	private int _rohstoffId = 1;

	protected override void OnReady()
	{
		_labelTitle = GetNode<Label>(LabelTitlePath);
		_linkButtonWare = GetNode<controls.LinkButtonWithSounds>(LinkButtonWarePath);
		_gridContainerMaerkte = GetNode<GridContainer>(GridContainerMaerktePath);
		_labelHinweis = GetNode<Label>(LabelHinweisPath);
		_linkButtonEntlassen = GetNode<controls.LinkButtonWithSounds>(LinkButtonEntlassenPath);
		_linkButtonClose = GetNode<controls.LinkButtonWithSounds>(LinkButtonClosePath);
	}

	/// <summary>Zeigt den Bericht, beginnend bei der angegebenen Ware.</summary>
	public Task<DialogResultGame> ShowDialog(int rohstoffId = 1)
	{
		_rohstoffId = Klemme(rohstoffId);
		Fill();

		return ShowAndAwait();
	}

	private void Fill()
	{
		_labelTitle.Text = "Der Faktor berichtet";
		_linkButtonWare.Text = "< " + SW.Dynamisch.GetRohstoffwithID(_rohstoffId).GetRohName() + " >";
		_linkButtonEntlassen.Text = "Entlassen (spart " + _faktor.GetJahreslohn().ToStringGeld(false) + " Taler)";

		foreach (Node kind in _gridContainerMaerkte.GetChildren())
			kind.QueueFree();

		AddZeile("Stadt", "Preis", "Lager / Bedarf", "Lieferung", "Sättigung");

		foreach (var zeile in _faktor.ErstelleMarktuebersicht(_rohstoffId))
		{
			AddZeile(zeile.StadtName + (zeile.EigeneWerkstatt ? " *" : ""),
			         zeile.Marktpreis.ToStringGeld(false) + (zeile.AbschlagProzent > 0
				         ? " (−" + zeile.AbschlagProzent + " %)"
				         : ""),
			         zeile.Vorrat + " / " + zeile.Jahresbedarf,
			         zeile.EigeneJahreslieferung == 0 ? "–" : zeile.EigeneJahreslieferung.ToString(),
			         BeschreibePrognose(zeile));
		}

		// Der Hinweis ist Pflicht, nicht Zierde: Ohne ihn liest sich die Prognose als Zusage. Sie kennt
		// weder fremde Lieferungen noch die jaehrliche Preisschwankung.
		_labelHinweis.Text = "Geschätzt allein nach Euren eigenen Lieferungen – was andere Händler in " +
		                     "dieselbe Stadt bringen, weiß auch Euer Faktor nicht, und die jährliche " +
		                     "Preisschwankung kann er nicht vorwegnehmen.\n" +
		                     "„nie“ heißt: Ihr liefert nicht mehr, als die Stadt im Jahr verbraucht. " +
		                     "* = Ihr habt dort eine Werkstätte für diese Ware.";
	}

	/// <summary>Die Prognose in Worten – die Zahl allein sagt nicht, ob sie gut oder schlecht ist.</summary>
	private static string BeschreibePrognose(FaktorMarktzeile zeile)
	{
		if (zeile.AmHoechstabschlag)
			return "erreicht";

		// Kurz halten: Jedes Wort mehr verbreitert die Spalte. Was „nie“ bedeutet, steht im Hinweis.
		if (zeile.JahreBisHoechstabschlag == FaktorMarktzeile.NieErreicht)
			return zeile.EigeneJahreslieferung == 0 ? "–" : "nie";

		return zeile.JahreBisHoechstabschlag == 1
			? "nächstes Jahr"
			: "in " + zeile.JahreBisHoechstabschlag + " Jahren";
	}

	private void AddZeile(string stadt, string preis, string lager, string lieferung, string prognose)
	{
		string[] werte = { stadt, preis, lager, lieferung, prognose };

		for (int spalte = 0; spalte < werte.Length; spalte++)
		{
			_gridContainerMaerkte.AddChild(new Label
			{
				Text = werte[spalte],
				CustomMinimumSize = new Vector2(Spaltenbreiten[spalte], 0),

				// Die erste Spalte trägt Namen, die übrigen Zahlen - Zahlen lesen sich rechtsbündig
				// leichter untereinander.
				HorizontalAlignment = spalte == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right
			});
		}
	}

	/// <summary>Hält die Rohstoff-ID im gültigen Bereich; 0 ist keine Ware.</summary>
	private static int Klemme(int rohstoffId)
	{
		int max = SW.Statisch.GetMaxRohID() - 1;

		if (rohstoffId < 1)
			return max;

		return rohstoffId > max ? 1 : rohstoffId;
	}

	private void _on_link_button_ware_pressed()
	{
		_rohstoffId = Klemme(_rohstoffId + 1);
		Fill();
	}

	private async void _on_link_button_entlassen_pressed()
	{
		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText(
			    "Wollt Ihr Euren Faktor entlassen?\n\n" +
			    "Ihr spart " + _faktor.GetJahreslohn().ToStringGeld() + " im Jahr,\n" +
			    "verliert aber seine Übersicht.", "Entlassen", "Behalten") == DialogResultGame.Yes)
		{
			_faktor.Entlasse();
			SoundManager.Instance.PlayCoins();
			Close(DialogResultGame.OK);
			return;
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
