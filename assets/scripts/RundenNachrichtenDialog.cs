using System.Text;
using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Nachrichtenschirm für die Zug- und Rundenende-Ereignisse (Migration des WinForms-Bildschirms
/// "Ort_Nachrichten" bzw. frmKampfereignisse): zeigt vor dem Hintergrundbild HintRundenNachrichten einen
/// gestylten Titel oben und den Meldungstext darunter auf dem Pergament. Ein Rechtsklick (oder Esc) blättert
/// weiter. Wird für die eigentlichen Spielereignisse verwendet – generische Bestätigungen/Fehler laufen
/// weiterhin über den kleinen <see cref="TextDialog"/>.
///
/// Spielernamen sind im Meldungstext mit |...|-Markern (der Markup-Konvention der Lib) versehen und werden
/// wie im WinForms-Original hervorgehoben: fett und – bei menschlichen Spielern – zusätzlich dunkelrot.
/// </summary>
public partial class RundenNachrichtenDialog : DialogBase, IShowText
{
	/// <summary>Dunkelrot (WinForms Color.DarkRed) für die Namen menschlicher Spieler.</summary>
	private const string FarbeMensch = "#8b0000";

	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelTitel;
	private RichTextLabel _labelText;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelText = GetNode<RichTextLabel>(LabelTextPath);
	}

	/// <summary>
	/// Zeigt eine Meldung. Der Text folgt der Konvention "Titel\n\nText": Alles vor der ersten Leerzeile
	/// wird als Überschrift dargestellt, der Rest als Fließtext. Fehlt die Leerzeile, gibt es keine
	/// Überschrift und der gesamte Text erscheint im Textbereich.
	/// </summary>
	public Task ShowDialog(string text)
	{
		int trenner = text.IndexOf("\n\n", System.StringComparison.Ordinal);

		if (trenner >= 0)
		{
			_labelTitel.Text = text.Substring(0, trenner);
			_labelText.Text = FormatiereMarkup(text.Substring(trenner + 2));
		}
		else
		{
			_labelTitel.Text = "";
			_labelText.Text = FormatiereMarkup(text);
		}

		_labelTitel.Visible = _labelTitel.Text.Length > 0;

		return ShowAndAwait();
	}

	/// <summary>
	/// Wandelt den Meldungstext in BBCode für das RichTextLabel um: Spielernamen zwischen |...|-Markern
	/// werden fett dargestellt, menschliche Spieler zusätzlich dunkelrot. Der gesamte Text wird zentriert.
	/// </summary>
	private static string FormatiereMarkup(string text)
	{
		var sb = new StringBuilder("[center]");

		// An den |-Markern trennen: Segmente an ungeraden Positionen sind Spielernamen.
		string[] teile = text.Split('|');

		for (int i = 0; i < teile.Length; i++)
		{
			if (i % 2 == 1)
			{
				bool mensch = IstMenschlicherSpielername(teile[i]);
				sb.Append("[b]");
				if (mensch)
					sb.Append("[color=").Append(FarbeMensch).Append(']');
				sb.Append(EscapeBbcode(teile[i]));
				if (mensch)
					sb.Append("[/color]");
				sb.Append("[/b]");
			}
			else
			{
				sb.Append(EscapeBbcode(teile[i]));
			}
		}

		sb.Append("[/center]");
		return sb.ToString();
	}

	/// <summary>Prüft, ob der Name zu einem der menschlichen Spieler gehört (wie im WinForms-Original).</summary>
	private static bool IstMenschlicherSpielername(string name)
	{
		for (int i = 1; i <= SW.Dynamisch.GetAktivSpielerAnzahl(); i++)
			if (SW.Dynamisch.GetSpWithID(i).GetKompletterName() == name)
				return true;

		return false;
	}

	/// <summary>Maskiert eckige Klammern, damit Meldungstext nicht versehentlich als BBCode interpretiert wird.</summary>
	private static string EscapeBbcode(string text) => text.Replace("[", "[lb]");
}
