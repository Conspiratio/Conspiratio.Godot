using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class YesNoDialog : DialogBase, IYesNoQuestion
{
	[Export]
	public NodePath LabelQuestionPath { get; set; }

	[Export]
	public NodePath LinkButtonYesPath { get; set; }

	[Export]
	public NodePath LinkButtonNoPath { get; set; }

	// Maße des Pergaments aus der Szene. Die Frage stand dort in einem Feld fester Höhe, die Knöpfe
	// darunter an fester Position – ein längerer Text (etwa die fünfzeilige Karawanen-Frage) lief
	// deshalb ungebremst über die Knöpfe hinweg. Der Rahmen wächst jetzt stattdessen mit.
	private const float TextBreite = 629f;         // LabelQuestion: 671 − 42
	private const float TextObenImRahmen = 30f;
	private const float TextHoeheStandard = 220f;  // Mindesthöhe, damit kurze Fragen aussehen wie bisher
	private const float AbstandTextZuKnoepfen = 20f;
	private const float KnopfHoehe = 40f;
	private const float RandUnten = 53f;
	/// <summary>Der Rahmen sitzt nicht exakt mittig, sondern etwas höher – dieser Versatz bleibt erhalten.</summary>
	private const float RahmenVersatzY = -16.5f;

	private Label _labelQuestion;
	private controls.LinkButtonWithSounds _linkButtonYes;
	private controls.LinkButtonWithSounds _linkButtonNo;
	private NinePatchRect _rahmen;

	protected override void OnReady()
	{
		_labelQuestion = GetNode<Label>(LabelQuestionPath);
		_linkButtonYes = GetNode<controls.LinkButtonWithSounds>(LinkButtonYesPath);
		_linkButtonNo = GetNode<controls.LinkButtonWithSounds>(LinkButtonNoPath);
		_rahmen = _labelQuestion.GetParent<NinePatchRect>();
	}

	public Task<DialogResultGame> ShowDialogText(string textQuestion, string textYes = "Ja", string textNo = "Nein")
	{
		_labelQuestion.Text = textQuestion;
		_linkButtonYes.Text = textYes;
		_linkButtonNo.Text = textNo;

		PasseGroesseAnText(textQuestion);

		return ShowAndAwait();
	}

	/// <summary>
	/// Streckt Pergament und Knopfreihe so weit, dass die umbrochene Frage vollständig darüber Platz hat.
	/// Die Höhe muss feststehen, bevor der Dialog das erste Mal gezeichnet wird – ein Nachmessen am
	/// fertigen Label (<c>GetLineCount</c>) käme einen Frame zu spät und würde sichtbar springen.
	/// </summary>
	private void PasseGroesseAnText(string text)
	{
		var schrift = _labelQuestion.GetThemeFont("font");
		int schriftgroesse = _labelQuestion.GetThemeFontSize("font_size");

		float zeilenHoehe = schrift.GetHeight(schriftgroesse) + _labelQuestion.GetThemeConstant("line_spacing");
		float textHoehe = Mathf.Max(TextHoeheStandard, ZaehleZeilen(schrift, schriftgroesse, text) * zeilenHoehe);

		float rahmenHoehe = TextObenImRahmen + textHoehe + AbstandTextZuKnoepfen + KnopfHoehe + RandUnten;
		float knopfOben = TextObenImRahmen + textHoehe + AbstandTextZuKnoepfen;

		_rahmen.OffsetTop = -rahmenHoehe / 2f + RahmenVersatzY;
		_rahmen.OffsetBottom = rahmenHoehe / 2f + RahmenVersatzY;

		_labelQuestion.OffsetBottom = TextObenImRahmen + textHoehe;

		foreach (var knopf in new[] { _linkButtonYes, _linkButtonNo })
		{
			knopf.OffsetTop = knopfOben;
			knopf.OffsetBottom = knopfOben + KnopfHoehe;
		}
	}

	/// <summary>
	/// Zählt, über wie viele Zeilen der Text im Label landet – einschließlich des Umbruchs an der Breite.
	///
	/// Nötig, weil <c>Font.GetMultilineStringSize</c> trotz Breitenangabe <b>nur die ausdrücklichen
	/// Zeilenumbrüche zählt</b> und den Wortumbruch ignoriert: Für die achtzeilige Karawanen-Frage lieferte
	/// es 231 px statt der tatsächlichen rund 300 – der Rahmen wuchs dadurch nicht weit genug und die
	/// Knöpfe standen weiterhin im Text. Hier wird deshalb Wort für Wort umbrochen wie das Label selbst.
	/// </summary>
	private static int ZaehleZeilen(Font schrift, int schriftgroesse, string text)
	{
		int zeilen = 0;

		foreach (string absatz in text.Split('\n'))
		{
			zeilen++;

			if (absatz.Length == 0)
				continue;

			string aktuell = "";

			foreach (string wort in absatz.Split(' '))
			{
				string probe = aktuell.Length == 0 ? wort : aktuell + " " + wort;

				// Passt das Wort nicht mehr, beginnt eine neue Zeile – ein einzelnes zu langes Wort
				// bleibt stehen und wird vom Label selbst hart umbrochen.
				if (aktuell.Length > 0 && schrift.GetStringSize(probe, HorizontalAlignment.Left, -1, schriftgroesse).X > TextBreite)
				{
					zeilen++;
					aktuell = wort;
				}
				else
				{
					aktuell = probe;
				}
			}
		}

		return zeilen;
	}

	private void _on_link_button_yes_pressed()
	{
		Close(DialogResultGame.Yes);
	}

	private void _on_link_button_no_pressed()
	{
		Close(DialogResultGame.No);
	}
}
