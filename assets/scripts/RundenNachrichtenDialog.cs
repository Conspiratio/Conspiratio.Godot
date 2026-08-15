using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
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

	/// <summary>Ist gesetzt, solange auf den Klick zur nächsten Meldung gewartet wird.</summary>
	private TaskCompletionSource<bool> _weiterKlick;

	/// <summary>
	/// Läuft gerade das Durchblättern der Kampfereignisse? Zwischen zwei Meldungen ist
	/// <see cref="_weiterKlick"/> kurz null; ohne dieses Kennzeichen würde ein Klick in genau dieses
	/// Fenster den Dialog schließen und die Eingabe abschalten – die Schleife wartete dann ewig auf
	/// einen Klick, der nicht mehr ankommen kann (das Spiel fror bei schnellem Klicken ein).
	/// </summary>
	private bool _blaettertGerade;

	// Der Nachrichtenschirm bleibt beim Weiterblättern sichtbar (kein Durchblitzen des Bildschirms dahinter).
	protected override bool BleibtSichtbarBeimSchliessen => true;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelText = GetNode<RichTextLabel>(LabelTextPath);
	}

	/// <summary>
	/// Im Kampfereignis-Modus blättert ein Rechtsklick zur nächsten Meldung, statt den Dialog zu schließen.
	/// </summary>
	protected override void OnNextOrClose()
	{
		if (_weiterKlick != null)
		{
			GetViewport().SetInputAsHandled();
			SoundManager.Instance.PlayRightClick();

			var klick = _weiterKlick;
			_weiterKlick = null;
			klick.TrySetResult(true);
			return;
		}

		// Beim Durchblättern zwischen zwei Meldungen: Der Klick läuft ins Leere, statt den Dialog zu
		// schließen. Sonst wäre die Eingabe abgeschaltet, bevor die Schleife die nächste Meldung
		// scharfmacht – und das Blättern käme nie wieder in Gang.
		if (_blaettertGerade)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		base.OnNextOrClose();
	}

	/// <summary>
	/// Zeigt eine Meldung. Der Text folgt der Konvention "Titel\n\nText": Alles vor der ersten Leerzeile
	/// wird als Überschrift dargestellt, der Rest als Fließtext. Fehlt die Leerzeile, gibt es keine
	/// Überschrift und der gesamte Text erscheint im Textbereich.
	/// </summary>
	public Task ShowDialog(string text)
	{
		int trenner = text.IndexOf("\n\n", System.StringComparison.Ordinal);

		_labelText.ScrollActive = false;

		if (trenner >= 0)
		{
			_labelTitel.Text = text.Substring(0, trenner);
			_labelText.Text = "[center]" + MarkupInhalt(text.Substring(trenner + 2)) + "[/center]";
		}
		else
		{
			_labelTitel.Text = "";
			_labelText.Text = "[center]" + MarkupInhalt(text) + "[/center]";
		}

		_labelTitel.Visible = _labelTitel.Text.Length > 0;

		return ShowAndAwait();
	}

	/// <summary>
	/// Zeigt die militärischen Ereignisse wie im WinForms-Original auf einer einzigen Seite: Der Titel bleibt
	/// oben stehen, die Meldungen werden – durch eine Leerzeile getrennt – nacheinander per Rechtsklick
	/// angehängt. Läuft der Text über, scrollt er ohne sichtbare Scrollbar ans Ende, sodass die neueste
	/// Meldung unten vollständig sichtbar ist und oben abgeschnitten wird. Der letzte Rechtsklick schließt.
	/// </summary>
	public async Task ShowKampfereignisse(string titel, IReadOnlyList<string> meldungen)
	{
		_labelTitel.Text = titel;
		_labelTitel.Visible = true;

		// Scrollen aktivieren, aber die Scrollbar unsichtbar machen (transparent, damit sie das erneute
		// Setzen des Textes übersteht – .Visible würde vom RichTextLabel wieder zurückgesetzt).
		_labelText.ScrollActive = true;
		_labelText.GetVScrollBar().SelfModulate = new Color(1, 1, 1, 0);

		var sb = new StringBuilder();

		OffenerNachrichtenschirm = this;
		_blaettertGerade = true;
		Show();
		MoveToFront();
		SetProcessInput(true);

		for (int i = 0; i < meldungen.Count; i++)
		{
			if (i > 0)
				sb.Append("\n\n");

			sb.Append(MarkupInhalt(meldungen[i]));
			_labelText.Text = "[center]" + sb + "[/center]";

			// Erst nach dem Layout ans Ende scrollen, damit die neueste Meldung unten vollständig sichtbar ist.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			ScrolleAnsEnde();

			await AufNaechstenKlickWarten();
		}

		_blaettertGerade = false;
		OffenerNachrichtenschirm = null;
		HideAndDisableInput();
	}

	private Task AufNaechstenKlickWarten()
	{
		_weiterKlick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _weiterKlick.Task;
	}

	private void ScrolleAnsEnde()
	{
		var scrollbar = _labelText.GetVScrollBar();
		scrollbar.Value = scrollbar.MaxValue;
	}

	/// <summary>Umrahmt den Meldungstext für die zentrierte Darstellung inkl. Namens-Hervorhebung.</summary>
	private static string MarkupInhalt(string text)
	{
		var sb = new StringBuilder();

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
