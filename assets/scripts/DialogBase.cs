using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Gemeinsame Basis für alle Overlay-Dialoge. Kapselt das wiederkehrende Muster aus Ein-/Ausblenden,
/// Eingabe-Aktivierung, dem <c>ui_next_or_close</c>-Handling (Rechtsklick/Esc inkl. Sound) und dem
/// <see cref="TaskCompletionSource{TResult}"/>, über das der Aufrufer das Schließen abwartet.
///
/// Ablauf für Unterklassen:
/// <list type="bullet">
/// <item>Die Kindknoten in <see cref="OnReady"/> verdrahten (statt <c>_Ready</c> zu überschreiben).</item>
/// <item>Im öffentlichen <c>Show…</c>-Aufruf den Inhalt setzen und <see cref="ShowAndAwait"/> zurückgeben.</item>
/// <item>Mit <see cref="Close"/> (z. B. aus Button-Handlern) das Ergebnis liefern und schließen.</item>
/// </list>
/// Rechtsklick/Esc schließt über <see cref="OnNextOrClose"/> (Standard: <see cref="DialogResultGame.Cancel"/>);
/// reine Bestätigungsdialoge ignorieren das Ergebnis einfach.
/// </summary>
public abstract partial class DialogBase : Control
{
	private TaskCompletionSource<DialogResultGame> _dialogClosed;

	/// <summary>
	/// Der aktuell sichtbare "durchgehende" Nachrichtenschirm (siehe <see cref="BleibtSichtbarBeimSchliessen"/>).
	/// Er bleibt zwischen aufeinanderfolgenden Meldungen sichtbar; erst ein anderer Dialog oder
	/// <see cref="VerbergeNachrichtenschirm"/> blendet ihn aus.
	/// </summary>
	protected static DialogBase OffenerNachrichtenschirm;

	/// <summary>
	/// Bleibt dieser Dialog beim Schließen (Weiterblättern) sichtbar, statt sich auszublenden? Nur der
	/// Rundennachrichten-Vollbildschirm nutzt das, damit beim Durchklicken der Meldungen nicht kurz der
	/// Bildschirm dahinter durchblitzt. Alle übrigen Dialoge blenden sich beim Schließen wie gewohnt aus.
	/// </summary>
	protected virtual bool BleibtSichtbarBeimSchliessen => false;

	public override void _Ready()
	{
		HideAndDisableInput();
		OnReady();
	}

	/// <summary>Verdrahtung der Kindknoten – Ersatz für <c>_Ready</c> in den Unterklassen.</summary>
	protected virtual void OnReady() { }

	public override void _Input(InputEvent @event)
	{
		// Nur auf das diskrete Drücken reagieren (@event statt globalem Input.IsActionPressed): Die
		// Zustandsabfrage liefert true, solange die rechte Maustaste gehalten wird, und zwar bei jedem
		// eintreffenden Ereignis – schon eine Mausbewegung genügt. Ein gehaltener Rechtsklick schloss so
		// mehrere Dialoge nacheinander ("Prellen"), statt nur den obersten.
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		OnNextOrClose();
	}

	/// <summary>
	/// Reaktion auf Rechtsklick/Esc. Standard: Eingabe als behandelt markieren, Klick-Sound spielen und
	/// mit <see cref="DialogResultGame.Cancel"/> schließen. Dialoge, die eine Knopfauswahl erzwingen, können
	/// dies mit einem leeren Override abschalten.
	/// </summary>
	protected virtual void OnNextOrClose()
	{
		GetViewport().SetInputAsHandled();
		SoundManager.Instance.PlayRightClick();
		Close(DialogResultGame.Cancel);
	}

	/// <summary>Blendet den Dialog ein, aktiviert die Eingabe und liefert die abzuwartende Aufgabe.</summary>
	protected Task<DialogResultGame> ShowAndAwait()
	{
		if (BleibtSichtbarBeimSchliessen)
		{
			// Nachrichtenschirm: als aktuell offen merken, damit er beim Weiterblättern sichtbar bleibt.
			OffenerNachrichtenschirm = this;
		}
		else if (OffenerNachrichtenschirm != null && OffenerNachrichtenschirm != this)
		{
			// Ein normaler Dialog erscheint über dem Nachrichtenschirm: diesen ausblenden, damit er beim
			// Schließen des Dialogs nicht mit veraltetem Inhalt dahinter kurz aufblitzt.
			OffenerNachrichtenschirm.HideAndDisableInput();
			OffenerNachrichtenschirm = null;
		}

		Show();
		// Nach vorne holen, damit der Dialog über gleichrangigen Geschwistern (z. B. einem noch offenen
		// Menü wie dem Lade-Dialog) liegt und seine Fehlermeldung nicht dahinter verschwindet.
		MoveToFront();
		SetProcessInput(true);
		_dialogClosed = new TaskCompletionSource<DialogResultGame>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _dialogClosed.Task;
	}

	/// <summary>Schließt den Dialog und schließt die Aufgabe mit dem angegebenen Ergebnis ab.</summary>
	protected void Close(DialogResultGame result)
	{
		// Der Nachrichtenschirm bleibt zwischen den Meldungen sichtbar (kein Durchblitzen) – nur die Eingabe
		// wird deaktiviert; ausgeblendet wird er erst durch den nächsten Dialog oder VerbergeNachrichtenschirm.
		if (BleibtSichtbarBeimSchliessen)
			SetProcessInput(false);
		else
			HideAndDisableInput();

		_dialogClosed?.TrySetResult(result);
	}

	/// <summary>
	/// Blendet einen noch sichtbaren "durchgehenden" Nachrichtenschirm aus. Vor der Rückkehr zur normalen
	/// Bedienung aufzurufen, damit keine Meldung sichtbar stehen bleibt.
	/// </summary>
	public static void VerbergeNachrichtenschirm()
	{
		if (OffenerNachrichtenschirm != null)
		{
			OffenerNachrichtenschirm.HideAndDisableInput();
			OffenerNachrichtenschirm = null;
		}
	}

	/// <summary>
	/// Blendet einen Vollbild-Overlay ein, der <b>nicht</b> von <see cref="DialogBase"/> erbt (WahlDialog,
	/// GerichtDialog, KartenspielDialog). Solche Bildschirme mussten sich sonst selbst einblenden und
	/// standen damit außerhalb des <see cref="OffenerNachrichtenschirm"/>-Protokolls: Der
	/// Rundennachrichten-Schirm bleibt nach dem Weiterklicken absichtlich sichtbar und wird
	/// <see cref="ShowAndAwait"/> nur von einem anderen <see cref="DialogBase"/> ausgeblendet. Ein
	/// bloßes <c>Show()</c> erschien deshalb <i>hinter</i> der letzten Meldung – die Wahl am Jahresende
	/// lief unsichtbar ab, während die Rechtsklicks des Spielers sie Schritt für Schritt durchklickten
	/// und auf dem Bildschirm weiterhin „Resümee“ stand.
	/// </summary>
	public static void ZeigeUeberNachrichtenschirm(Control dialog)
	{
		VerbergeNachrichtenschirm();
		dialog.Show();
		// Wie in ShowAndAwait: über die gleichrangigen Geschwister holen, damit kein früher per
		// MoveToFront nach vorn geholter Dialog darüberliegt.
		dialog.MoveToFront();
	}

	/// <summary>Blendet den Dialog aus und deaktiviert die Eingabeverarbeitung.</summary>
	protected void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}
}
