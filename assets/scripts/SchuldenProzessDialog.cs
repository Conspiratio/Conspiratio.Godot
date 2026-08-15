using System.Collections.Generic;
using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Schuldenprozess vor den Gläubigern (Migration von Ort_Abstimmung aus dem WinForms-Client):
/// Vollbild vor dem Ratstisch (`HintAbstimmung`), unten eine Reihe von Symbolen für die Geschworenen.
/// Zu Beginn steht jedes auf „noch nicht abgestimmt" (`SymbNV`); dann wird Stimme für Stimme per
/// Rechtsklick aufgedeckt: Wer den Spieler für schuldig hält, bekommt die Kerkertür (`SymbDoor`), wer
/// ihn freispricht, verschwindet von der Bank. Die jeweilige Aussage steht in Goldschrift darunter.
///
/// Zuvor lief der ganze Prozess als drei Textseiten über den generischen Nachrichtenschirm – die
/// Inszenierung des Originals (einzelne Stimmen, Symbole, eigener Hintergrund) fehlte dabei.
/// </summary>
public partial class SchuldenProzessDialog : DialogBase
{
	/// <summary>Kantenlänge eines Geschworenen-Symbols (im Original ein Elftel der Bildschirmbreite).</summary>
	private const int SymbolGroesse = 80;

	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath LabelMeldungPath { get; set; }

	[Export]
	public NodePath BoxGeschworenePath { get; set; }

	private Label _labelTitel;
	private Label _labelMeldung;
	private HBoxContainer _boxGeschworene;

	private Texture2D _symbolOffen;
	private Texture2D _symbolSchuldig;

	private readonly List<TextureRect> _symbole = new();

	/// <summary>Ist gesetzt, solange auf den Klick zur nächsten Stimme gewartet wird.</summary>
	private TaskCompletionSource<bool> _weiterKlick;

	// Der Prozess bleibt zwischen den einzelnen Stimmen sichtbar – sonst blitzt der Bildschirm dahinter durch.
	protected override bool BleibtSichtbarBeimSchliessen => true;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelMeldung = GetNode<Label>(LabelMeldungPath);
		_boxGeschworene = GetNode<HBoxContainer>(BoxGeschworenePath);

		_symbolOffen = GD.Load<Texture2D>("res://assets/images/symbole/SymbNV.png");
		_symbolSchuldig = GD.Load<Texture2D>("res://assets/images/symbole/SymbDoor.png");
	}

	/// <summary>
	/// Während der Prozess läuft, blättert ein Rechtsklick zur nächsten Stimme weiter, statt den
	/// Bildschirm zu schließen (dasselbe Muster wie beim Nachrichtenschirm).
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

		base.OnNextOrClose();
	}

	/// <summary>
	/// Führt den Prozess vor: Auftakt, dann Stimme für Stimme, zuletzt das Ergebnis. Der Bildschirm
	/// bleibt am Ende offen, damit der Aufrufer bei einem Schuldspruch direkt auf den Kerker
	/// weiterschalten kann; sonst schließt der letzte Rechtsklick.
	/// </summary>
	public async Task ZeigeProzess(SchuldenProzessErgebnis prozess)
	{
		BaueGeschworene(prozess.GeschworenenNamen.Count);

		_labelTitel.Text = "Die Abstimmung Eurer Gläubiger";
		_labelMeldung.Text = "Wegen Euren zahlreichen Schulden müsst Ihr Euch nun vor Euren Gläubigern verantworten!";

		OffenerNachrichtenschirm = this;
		Show();
		MoveToFront();
		SetProcessInput(true);

		await AufNaechstenKlickWarten();

		for (int i = 0; i < prozess.GeschworenenNamen.Count; i++)
		{
			bool schuldig = prozess.Urteile[i];

			// Wie im Original: schuldig zeigt die Kerkertür, ein Freispruch nimmt den Geschworenen von der Bank.
			if (schuldig)
				_symbole[i].Texture = _symbolSchuldig;
			else
				_symbole[i].Visible = false;

			_labelMeldung.Text = prozess.GeschworenenNamen[i] + ": " + (schuldig ? "schuldig!" : "nicht schuldig!");

			await AufNaechstenKlickWarten();
		}

		foreach (var symbol in _symbole)
			symbol.Visible = false;

		// Nur der Freispruch wird hier verkündet. Bei einem Schuldspruch wechselt das Original auf den
		// Kerker-Bildschirm und nennt die Folge erst dort – die Meldung hier wäre doppelt.
		if (!prozess.Schuldig)
		{
			_labelMeldung.Text = "Ihr seid noch einmal mit dem Schrecken davon gekommen...";
			await AufNaechstenKlickWarten();
		}

		OffenerNachrichtenschirm = null;
		HideAndDisableInput();
	}

	private void BaueGeschworene(int anzahl)
	{
		foreach (Node kind in _boxGeschworene.GetChildren())
		{
			_boxGeschworene.RemoveChild(kind);
			kind.QueueFree();
		}

		_symbole.Clear();

		for (int i = 0; i < anzahl; i++)
		{
			// ExpandMode vor der Größe setzen: Sonst erzwingt die Standardeinstellung KeepSize die
			// native Texturgröße als Mindestmaß und das Symbol lässt sich nicht frei skalieren.
			var symbol = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Texture = _symbolOffen,
				CustomMinimumSize = new Vector2(SymbolGroesse, SymbolGroesse)
			};

			_symbole.Add(symbol);
			_boxGeschworene.AddChild(symbol);
		}
	}

	private Task AufNaechstenKlickWarten()
	{
		_weiterKlick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _weiterKlick.Task;
	}
}
