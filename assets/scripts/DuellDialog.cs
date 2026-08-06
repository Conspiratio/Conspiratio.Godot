using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Vollbild-Inszenierung eines Duells im Morgengrauen (Issue #17). Der Kampf selbst bleibt bewusst
/// unsichtbar: Nebel zieht auf und verhüllt die Kontrahenten, aus dem Nebel dringen abwechselnd
/// Kampfrufe und spöttische Sprüche (im Geiste der Fecht-Duelle aus „Monkey Island"), untermalt von
/// Erzählertexten. Erst zum Schluss lichtet sich der Nebel und gibt den Ausgang preis.
///
/// Die Duell-Logik ist zu diesem Zeitpunkt bereits ausgewertet (<c>FechtDuellManager.FuehreDuellDurch</c>);
/// dieser Dialog stellt das Ergebnis nur dar. Ein Rechtsklick überspringt die laufende Inszenierung und
/// zeigt sofort den Ausgang; ein weiterer Rechtsklick schließt.
/// </summary>
public partial class DuellDialog : DialogBase, IDuellDialog
{
	/// <summary>Wie lange ein Spruch nach dem Einblenden stehen bleibt (plus je 0,35 s Ein-/Ausblenden).</summary>
	private const double SpruchHaltedauer = 2.4;

	private const double EinblendDauer = 0.35;

	/// <summary>Anzahl der Sprüche im Nebel – zusammen mit Intro/Outro ergibt das rund 25 Sekunden.</summary>
	private const int AnzahlSprueche = 6;

	/// <summary>Kampfrufe und Sticheleien des Spielers.</summary>
	private static readonly string[] Angriffe =
	{
		"Nimm das!",
		"Ist das alles?",
		"Ihr kämpft wie ein Bauerntölpel!",
		"Meine Großmutter ficht besser als Ihr!",
		"Habt Ihr das Fechten aus einem Kochbuch gelernt?",
		"Selbst ein nasser Waschlappen träfe besser!",
		"Ihr stolpert über Euren eigenen Schatten!",
		"Wollt Ihr fechten oder tanzen?"
	};

	/// <summary>Erwiderungen des Gegners.</summary>
	private static readonly string[] Antworten =
	{
		"Arrrrgh!",
		"Zu langsam!",
		"Ha, verfehlt!",
		"Das war nur ein Kratzer!",
		"Ihr seid ein lausiger Fechter!",
		"Nur Mut, ich beiße nicht!",
		"Eure Klinge ist so stumpf wie Euer Witz!",
		"Oho! Fast hättet Ihr mich gekitzelt!"
	};

	/// <summary>Erzähler-Einwürfe, während der Nebel den Kampf verhüllt.</summary>
	private static readonly string[] Nebelstimmung =
	{
		"Klingen klirren im Verborgenen …",
		"Der Nebel wogt, ein Schatten taumelt zurück …",
		"Stahl trifft auf Stahl, doch niemand sieht, wen es trifft …"
	};

	[Export]
	public NodePath NebelPath { get; set; }

	[Export]
	public NodePath LabelSpruchPath { get; set; }

	[Export]
	public NodePath LabelErzaehlerPath { get; set; }

	[Export]
	public NodePath LabelPunktestandPath { get; set; }

	[Export]
	public NodePath LabelAmZugPath { get; set; }

	[Export]
	public NodePath AuswahlBoxPath { get; set; }

	private Control _nebel;
	private Label _labelSpruch;
	private Label _labelErzaehler;
	private Label _labelPunktestand;
	private Label _labelAmZug;
	private VBoxContainer _auswahlBox;
	private PackedScene _linkButtonScene;

	/// <summary>Läuft die Inszenierung gerade (dann überspringt ein Rechtsklick, statt zu schließen)?</summary>
	private bool _laeuft;

	private bool _ueberspringen;

	/// <summary>Wartet gerade eine Auswahl auf einen Klick? Dann darf Rechtsklick nichts auslösen.</summary>
	private TaskCompletionSource<int> _optionGewaehlt;

	/// <summary>Wird das Duell selbst ausgetragen? Dann gibt es kein Überspringen der Wortwechsel.</summary>
	private bool _interaktiv;

	/// <summary>Läuft, bis der Spieler die Szene am Ende schließt (zwischen den beiden Aufrufen offen).</summary>
	private Task<DialogResultGame> _geschlossen;

	private readonly List<Tween> _nebelTweens = new();

	/// <summary>Nur für die Darstellung – bewusst nicht der Spiel-Zufall, damit der Spielverlauf gleich bleibt.</summary>
	private readonly Random _zufall = new();

	protected override void OnReady()
	{
		_nebel = GetNode<Control>(NebelPath);
		_labelSpruch = GetNode<Label>(LabelSpruchPath);
		_labelErzaehler = GetNode<Label>(LabelErzaehlerPath);
		_labelPunktestand = GetNode<Label>(LabelPunktestandPath);
		_labelAmZug = GetNode<Label>(LabelAmZugPath);
		_auswahlBox = GetNode<VBoxContainer>(AuswahlBoxPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	/// <summary>
	/// Während die Inszenierung läuft, überspringt ein Rechtsklick/Esc nur zum Ausgang; erst danach
	/// schließt er wie gewohnt den Dialog.
	/// </summary>
	protected override void OnNextOrClose()
	{
		// Während einer Auswahl muss ein Knopf gedrückt werden – Rechtsklick tut nichts (wie im Gericht).
		if (_optionGewaehlt != null)
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_laeuft)
		{
			GetViewport().SetInputAsHandled();

			// Ein selbst ausgetragenes Duell lässt sich nicht überspringen – sonst würden die
			// Wortwechsel durchrauschen. Nur die automatische Inszenierung darf übersprungen werden.
			if (!_interaktiv && !_ueberspringen)
			{
				SoundManager.Instance.PlayRightClick();
				_ueberspringen = true;
			}

			return;
		}

		base.OnNextOrClose();
	}

	/// <summary>
	/// Trägt das Duell aus und liefert, ob der aktive Spieler gewonnen hat. Die Szene bleibt danach
	/// offen, damit der Aufrufer die Folgen anwenden und sie mit <see cref="ZeigeAusgang"/> zeigen kann.
	/// </summary>
	public async Task<bool> SpieleWortgefecht(WortgefechtManager gefecht, string gegnerName)
	{
		_laeuft = true;
		_ueberspringen = false;
		_labelSpruch.Text = "";
		_labelSpruch.Modulate = new Color(1, 1, 1, 0);
		_labelErzaehler.Text = "";
		_labelAmZug.Text = "";
		_labelPunktestand.Text = "";
		_auswahlBox.Visible = false;

		_interaktiv = ClientSettings.DuelleInteraktiv;
		_geschlossen = ShowAndAwait();

		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Kampf);
		StarteNebelDrift();

		// Intro: erst die Gegenüberstellung, dann zieht der Nebel auf.
		await ZeigeErzaehler("Im ersten Licht des Morgengrauens tretet Ihr " + gegnerName + " gegenüber.", 2.4);
		await ZeigeErzaehler("Und dann zog Nebel auf und verhüllte die Kontrahenten …", 2.2);
		_labelErzaehler.Text = "";

		if (_interaktiv)
			await SpieleRunden(gefecht);
		else
			await SpieleAutomatisch(gefecht);

		return gefecht.SpielerHatGewonnen;
	}

	/// <summary>Der Schlagabtausch: Beleidigung wählen, passenden Konter erkennen – bis einer 3 Treffer hat.</summary>
	private async Task SpieleRunden(WortgefechtManager gefecht)
	{
		AktualisierePunktestand(gefecht);

		while (!gefecht.IstBeendet)
		{
			// 1. Der Angreifer wählt eine Beleidigung.
			var angriff = gefecht.NaechsterAngriff();

			int angriffsIndex = angriff.MenschWaehlt
				? await WaehleOption(angriff.WaehlerIstAktiverSpieler ? "" : angriff.WaehlerName, angriff.Optionen)
				: gefecht.WaehleKiAngriff();

			var konter = gefecht.WaehleAngriff(angriffsIndex);
			await ZeigeSpruch(konter.Beleidigung);

			// 2. Der Angegriffene sucht die passende Erwiderung.
			int konterIndex = konter.MenschWaehlt
				? await WaehleOption(konter.WaehlerIstAktiverSpieler ? "" : konter.WaehlerName, konter.Optionen)
				: gefecht.WaehleKiKonter();

			var ergebnis = gefecht.WerteKonterAus(konterIndex);
			await ZeigeSpruch(ergebnis.Erwiderung);

			_labelErzaehler.Text = ergebnis.Kommentar;
			AktualisierePunktestand(gefecht);
			await Warte(1.8);
			_labelErzaehler.Text = "";
		}
	}

	/// <summary>
	/// Nicht interaktiv (Option „Duelle selbst austragen" aus): der bisherige Ablauf – zufällige Rufe
	/// aus dem Nebel, während der Ausgang gewürfelt wird.
	/// </summary>
	private async Task SpieleAutomatisch(WortgefechtManager gefecht)
	{
		gefecht.WuerfleAusgang();

		var angriffe = MischeUndNimm(Angriffe, (AnzahlSprueche + 1) / 2);
		var antworten = MischeUndNimm(Antworten, AnzahlSprueche / 2);

		for (int i = 0; i < AnzahlSprueche; i++)
		{
			// Zur Halbzeit ein Erzähler-Einwurf, damit die Stimmung nicht abreißt.
			if (i == AnzahlSprueche / 2)
				_labelErzaehler.Text = Nebelstimmung[_zufall.Next(Nebelstimmung.Length)];

			await ZeigeSpruch(i % 2 == 0 ? angriffe[i / 2] : antworten[i / 2]);
		}
	}

	/// <summary>Zeigt den Ausgang und wartet, bis der Spieler die Szene schließt.</summary>
	public async Task ZeigeAusgang(bool spielerGewinnt, string gegnerName, bool amtVerloren, string amtName)
	{
		// Der Nebel lichtet sich und gibt den Ausgang preis.
		_labelErzaehler.Text = "";
		_labelAmZug.Text = "";
		await BlendeNebelAus();

		_labelSpruch.Text = spielerGewinnt ? "Sieg!" : "Niederlage";
		var einblenden = CreateTween();
		einblenden.TweenProperty(_labelSpruch, "modulate:a", 1.0, EinblendDauer);
		await WarteAufTween(einblenden);

		// Beim Überspringen laufen die Tweens nicht zu Ende – der Endzustand wird darum gesetzt.
		_labelSpruch.Modulate = new Color(1, 1, 1, 1);
		_labelErzaehler.Text = BaueAusgangstext(spielerGewinnt, gegnerName, amtVerloren, amtName);

		// Wurde übersprungen, ist die Maustaste womöglich noch gedrückt – derselbe Klick würde den
		// Dialog sofort wieder schließen. Darum erst das Loslassen abwarten.
		while (Input.IsActionPressed("ui_next_or_close"))
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// Ab hier schließt ein Rechtsklick, statt zu überspringen.
		_ueberspringen = false;
		_laeuft = false;

		await _geschlossen;

		StoppeNebelDrift();
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
	}

	/// <summary>
	/// Stellt die Auswahl als Knöpfe dar und wartet auf den Klick (Muster wie im Gerichtsdialog).
	/// Solange eine Auswahl offen ist, schließt Rechtsklick den Dialog nicht.
	/// </summary>
	private async Task<int> WaehleOption(string waehlerName, IReadOnlyList<string> texte)
	{
		// Der Erzählertext liegt im selben Bereich wie die Knöpfe – während der Auswahl bleibt er leer.
		_labelErzaehler.Text = "";
		_labelAmZug.Text = string.IsNullOrEmpty(waehlerName) ? "" : "Am Zug: " + waehlerName;

		LeereAuswahl();
		_optionGewaehlt = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

		for (int i = 0; i < texte.Count; i++)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = texte[i];
			StyleAuswahlKnopf(button);

			int index = i;
			button.Pressed += () => _optionGewaehlt?.TrySetResult(index);

			_auswahlBox.AddChild(button);
		}

		_auswahlBox.Visible = true;

		int gewaehlt = await _optionGewaehlt.Task;

		_optionGewaehlt = null;
		_auswahlBox.Visible = false;
		_labelAmZug.Text = "";
		LeereAuswahl();

		return gewaehlt;
	}

	/// <summary>
	/// Die Knöpfe erben sonst die dunkle Pergamentschrift des Standardthemas – vor dem nächtlichen
	/// Duellplatz wären sie kaum lesbar. Darum hier mittig und in derselben Goldschrift wie die Sprüche.
	/// </summary>
	private static void StyleAuswahlKnopf(controls.LinkButtonWithSounds button)
	{
		button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

		button.AddThemeColorOverride("font_color", new Color(0.93f, 0.83f, 0.55f));
		button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.7f));
		button.AddThemeColorOverride("font_pressed_color", new Color(1f, 1f, 0.85f));
		button.AddThemeColorOverride("font_focus_color", new Color(0.93f, 0.83f, 0.55f));
		button.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.04f, 0.02f));
		button.AddThemeConstantOverride("outline_size", 6);
		button.AddThemeFontSizeOverride("font_size", 28);
	}

	private void LeereAuswahl()
	{
		foreach (Node kind in _auswahlBox.GetChildren())
		{
			_auswahlBox.RemoveChild(kind);
			kind.QueueFree();
		}
	}

	private void AktualisierePunktestand(WortgefechtManager gefecht) =>
		_labelPunktestand.Text = "Ihr " + gefecht.SpielerTreffer + " : " + gefecht.GegnerTreffer + " " + gefecht.GegnerName;

	private static string BaueAusgangstext(bool spielerGewinnt, string gegnerName, bool amtVerloren, string amtName)
	{
		string text = spielerGewinnt
			? "Der Nebel lichtet sich, und Ihr steht triumphierend über Eurem Gegner. Seine Gesundheit hat gelitten!"
			: "Der Nebel lichtet sich – " + gegnerName + " steht triumphierend über Euch. Eure Gesundheit hat gelitten!";

		if (amtVerloren)
		{
			text += spielerGewinnt
				? "\n\nSo schwer verletzt muss Euer Gegner das Amt als " + amtName + " niederlegen – es wird neu besetzt."
				: "\n\nEure Verletzung zwingt Euch, Euer Amt als " + amtName + " niederzulegen.";
		}

		return text + "\n\n(Rechtsklick)";
	}

	/// <summary>Zeigt einen Erzählertext und lässt ihn die angegebene Zeit stehen.</summary>
	private async Task ZeigeErzaehler(string text, double dauer)
	{
		_labelErzaehler.Text = text;
		await Warte(dauer);
	}

	/// <summary>Blendet einen Spruch ein, hält ihn kurz und blendet ihn wieder aus.</summary>
	private async Task ZeigeSpruch(string text)
	{
		_labelSpruch.Text = text;
		_labelSpruch.Modulate = new Color(1, 1, 1, 0);

		var tween = CreateTween();
		tween.TweenProperty(_labelSpruch, "modulate:a", 1.0, EinblendDauer);
		tween.TweenInterval(SpruchHaltedauer);
		tween.TweenProperty(_labelSpruch, "modulate:a", 0.0, EinblendDauer);

		await WarteAufTween(tween);
		_labelSpruch.Modulate = new Color(1, 1, 1, 0);
	}

	/// <summary>Lässt die Nebelschwaden dauerhaft und unterschiedlich schnell über den Bildschirm ziehen.</summary>
	private void StarteNebelDrift()
	{
		StoppeNebelDrift();
		_nebel.Modulate = new Color(1, 1, 1, 1);

		double dauer = 14.0;

		foreach (var kind in _nebel.GetChildren())
		{
			if (kind is not Control schwade)
				continue;

			float start = schwade.Position.X;

			var tween = CreateTween();
			tween.SetLoops();
			tween.TweenProperty(schwade, "position:x", start + 260f, dauer).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.TweenProperty(schwade, "position:x", start, dauer).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

			_nebelTweens.Add(tween);
			dauer += 4.5;
		}
	}

	private void StoppeNebelDrift()
	{
		foreach (var tween in _nebelTweens)
		{
			if (tween.IsValid())
				tween.Kill();
		}

		_nebelTweens.Clear();
	}

	private async Task BlendeNebelAus()
	{
		var tween = CreateTween();
		tween.TweenProperty(_nebel, "modulate:a", 0.15, 1.2);
		await WarteAufTween(tween);

		// Endzustand auch dann herstellen, wenn übersprungen wurde.
		_nebel.Modulate = new Color(1, 1, 1, 0.15f);
	}

	/// <summary>Wartet die angegebene Zeit ab – bricht sofort ab, wenn übersprungen wird.</summary>
	private async Task Warte(double sekunden)
	{
		double verstrichen = 0;

		while (verstrichen < sekunden && !_ueberspringen)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			verstrichen += GetProcessDeltaTime();
		}
	}

	/// <summary>Wartet auf das Ende eines Tweens – bricht ihn ab, wenn übersprungen wird.</summary>
	private async Task WarteAufTween(Tween tween)
	{
		while (tween.IsValid() && tween.IsRunning() && !_ueberspringen)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (tween.IsValid())
			tween.Kill();
	}

	/// <summary>Zieht <paramref name="anzahl"/> verschiedene Sprüche aus dem Vorrat (Fisher-Yates).</summary>
	private string[] MischeUndNimm(string[] vorrat, int anzahl)
	{
		var kopie = (string[])vorrat.Clone();

		for (int i = kopie.Length - 1; i > 0; i--)
		{
			int j = _zufall.Next(i + 1);
			(kopie[i], kopie[j]) = (kopie[j], kopie[i]);
		}

		var ergebnis = new string[anzahl];
		Array.Copy(kopie, ergebnis, anzahl);
		return ergebnis;
	}
}
