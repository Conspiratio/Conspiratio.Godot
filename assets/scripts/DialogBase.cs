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

	public override void _Ready()
	{
		HideAndDisableInput();
		OnReady();
	}

	/// <summary>Verdrahtung der Kindknoten – Ersatz für <c>_Ready</c> in den Unterklassen.</summary>
	protected virtual void OnReady() { }

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
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
		Show();
		SetProcessInput(true);
		_dialogClosed = new TaskCompletionSource<DialogResultGame>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _dialogClosed.Task;
	}

	/// <summary>Schließt den Dialog und schließt die Aufgabe mit dem angegebenen Ergebnis ab.</summary>
	protected void Close(DialogResultGame result)
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(result);
	}

	/// <summary>Blendet den Dialog aus und deaktiviert die Eingabeverarbeitung.</summary>
	protected void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}
}
