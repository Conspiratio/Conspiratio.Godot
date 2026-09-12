using System.Collections.Generic;
using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Amtsenthebung vor dem Ratstisch (Migration von Posi_Amtsenthebung aus dem WinForms-Client):
/// Vollbild auf demselben Hintergrund wie der Schuldenprozess (`HintAbstimmung`), unten eine Reihe von
/// Symbolen für die bis zu drei Wähler. Zu Beginn steht jedes auf „noch nicht abgestimmt" (`SymbNV`);
/// dann wird Stimme für Stimme per Rechtsklick aufgedeckt: Wer für die Absetzung stimmt, bekommt das
/// Verbrechenssymbol (`SymbCrime`), wer dagegen stimmt, verschwindet von der Bank.
///
/// Ist der Spieler selbst Wähler, wird er auf diesem Bildschirm gefragt – die Knöpfe liegen bewusst in
/// der Szene und nicht in einem überlagerten Ja/Nein-Dialog, der die Abstimmung dahinter ausblenden würde.
///
/// Die Logik liegt im <see cref="AmtsenthebungsManager"/> der Lib; dieser Bildschirm erscheint nur,
/// wenn ein Mensch beteiligt ist. Rein unter KIs läuft das Verfahren unsichtbar ab.
/// </summary>
public partial class AmtsenthebungDialog : DialogBase
{
	/// <summary>Kantenlänge eines Wähler-Symbols (wie beim Schuldenprozess).</summary>
	private const int SymbolGroesse = 80;

	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath LabelMeldungPath { get; set; }

	[Export]
	public NodePath BoxWaehlerPath { get; set; }

	[Export]
	public NodePath BoxStimmabgabePath { get; set; }

	private Label _labelTitel;
	private Label _labelMeldung;
	private HBoxContainer _boxWaehler;
	private VBoxContainer _boxStimmabgabe;

	private Texture2D _symbolOffen;
	private Texture2D _symbolDafuer;

	private PackedScene _linkButtonScene;

	private readonly List<TextureRect> _symbole = new();

	/// <summary>Ist gesetzt, solange auf den Klick zur nächsten Stimme gewartet wird.</summary>
	private TaskCompletionSource<bool> _weiterKlick;

	/// <summary>Ist gesetzt, solange auf die Stimme eines menschlichen Wählers gewartet wird.</summary>
	private TaskCompletionSource<bool> _stimmeGewaehlt;

	// Das Verfahren bleibt zwischen den einzelnen Stimmen sichtbar – sonst blitzt der Bildschirm dahinter durch.
	protected override bool BleibtSichtbarBeimSchliessen => true;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelMeldung = GetNode<Label>(LabelMeldungPath);
		_boxWaehler = GetNode<HBoxContainer>(BoxWaehlerPath);
		_boxStimmabgabe = GetNode<VBoxContainer>(BoxStimmabgabePath);

		_symbolOffen = GD.Load<Texture2D>("res://assets/images/symbole/SymbNV.png");
		_symbolDafuer = GD.Load<Texture2D>("res://assets/images/symbole/SymbCrime.png");
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

		_boxStimmabgabe.Visible = false;
	}

	/// <summary>
	/// Während des Verfahrens blättert ein Rechtsklick zur nächsten Stimme weiter, statt den Bildschirm
	/// zu schließen. Solange der Spieler seine eigene Stimme abgeben soll, tut ein Rechtsklick nichts –
	/// hier ist eine Entscheidung verlangt.
	/// </summary>
	protected override void OnNextOrClose()
	{
		if (_stimmeGewaehlt != null)
			return;

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
	/// Führt ein Amtsenthebungsverfahren vor und liefert dessen Ergebnis. Menschliche Wähler werden
	/// unterwegs auf diesem Bildschirm befragt, die KI-Stimmen ergänzt der Manager.
	/// </summary>
	public async Task<AmtsenthebungsErgebnis> ZeigeVerfahren(AmtsenthebungsManager manager, AmtsenthebungsVerfahren verfahren)
	{
		BaueWaehler(verfahren.Waehler.Count);

		_labelTitel.Text = "Amtsenthebung";

		// Wer den Antrag gestellt hat, stand bisher nirgends – das Opfer wusste nicht, gegen wen es sich
		// wehren müsste. Den Satz baut die Lib, weil sie den Fall „kein benennbarer Antragsteller"
		// (altes Spiel oder Gerichtsbeschluss) kennt.
		_labelMeldung.Text = verfahren.GetAntragText();

		OffenerNachrichtenschirm = this;
		Show();
		MoveToFront();
		SetProcessInput(true);

		await AufNaechstenKlickWarten();

		for (int i = 0; i < verfahren.Waehler.Count; i++)
		{
			var waehler = verfahren.Waehler[i];

			if (waehler.IstMensch)
			{
				_labelMeldung.Text = "Wollt Ihr, " + waehler.Name + ",\n" + verfahren.OpferName + " des Amtes entheben?";
				waehler.StimmtDafuer = await FrageStimme();
			}
			else
			{
				manager.ErgaenzeKiStimmen(verfahren);
			}

			// Wie im Original: Zustimmung zeigt das Verbrechenssymbol, Ablehnung nimmt den Wähler von der Bank.
			if (waehler.StimmtDafuer)
				_symbole[i].Texture = _symbolDafuer;
			else
				_symbole[i].Visible = false;

			_labelMeldung.Text = waehler.Name + " ist " + (waehler.StimmtDafuer ? "für" : "gegen") + " die Absetzung.";

			await AufNaechstenKlickWarten();
		}

		var ergebnis = manager.WerteAus(verfahren);

		foreach (var symbol in _symbole)
			symbol.Visible = false;

		_labelMeldung.Text = ergebnis.Meldung;
		await AufNaechstenKlickWarten();

		OffenerNachrichtenschirm = null;
		HideAndDisableInput();

		return ergebnis;
	}

	/// <summary>Blendet die Ja/Nein-Knöpfe ein und wartet auf die Entscheidung des Spielers.</summary>
	private async Task<bool> FrageStimme()
	{
		_stimmeGewaehlt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		LeereStimmabgabe();

		foreach ((string text, bool dafuer) in new[] { ("Ja, er soll gehen!", true), ("Nein, er bleibt.", false) })
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = text;

			// In Code erzeugte Knöpfe erben die dunkle Pergamentschrift des Themes; auf dem dunklen
			// Vollbild wäre sie unsichtbar.
			button.AddThemeColorOverride("font_color", new Color(0.93f, 0.83f, 0.55f));
			button.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.04f, 0.02f));
			button.AddThemeConstantOverride("outline_size", 6);
			button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

			bool stimme = dafuer;
			button.Pressed += () => _stimmeGewaehlt?.TrySetResult(stimme);

			_boxStimmabgabe.AddChild(button);
		}

		_boxStimmabgabe.Visible = true;

		bool gewaehlt = await _stimmeGewaehlt.Task;

		_stimmeGewaehlt = null;
		_boxStimmabgabe.Visible = false;
		LeereStimmabgabe();

		return gewaehlt;
	}

	private void LeereStimmabgabe()
	{
		foreach (Node kind in _boxStimmabgabe.GetChildren())
		{
			_boxStimmabgabe.RemoveChild(kind);
			kind.QueueFree();
		}
	}

	private void BaueWaehler(int anzahl)
	{
		foreach (Node kind in _boxWaehler.GetChildren())
		{
			_boxWaehler.RemoveChild(kind);
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
			_boxWaehler.AddChild(symbol);
		}
	}

	private Task AufNaechstenKlickWarten()
	{
		_weiterKlick = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _weiterKlick.Task;
	}
}
