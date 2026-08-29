using System.Linq;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Justiz;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Gerichtsverhandlung (Migration von CheckGerichtsVerhandlungen/GerichtsverhandlungDurchfuehren):
/// ist der aktive Spieler an einem Prozess beteiligt (als Angeklagter, Kläger oder Richter), wird dieser
/// hier auf dem Gerichts-Bildschirm abgewickelt – Vorstellung, Vorwürfe, drei Richter stimmen ab (der
/// Spieler per Ja/Nein-Dialog) und die Auswertung samt Strafe. Der Rechtsklick blättert weiter.
/// </summary>
public partial class GerichtDialog : Control
{
	[Export]
	public NodePath LabelHauptPath { get; set; }

	[Export]
	public NodePath LabelUrteilePath { get; set; }

	[Export]
	public NodePath AussageAuswahlPath { get; set; }

	private Label _labelHaupt;
	private Label _labelUrteile;
	private VBoxContainer _aussageAuswahl;
	private PackedScene _linkButtonScene;

	private TaskCompletionSource<bool> _weiter;
	private TaskCompletionSource<int> _optionGewaehlt;

	public override void _Ready()
	{
		_labelHaupt = GetNode<Label>(LabelHauptPath);
		_labelUrteile = GetNode<Label>(LabelUrteilePath);
		_aussageAuswahl = GetNode<VBoxContainer>(AussageAuswahlPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (_weiter != null && @event.IsActionPressed("ui_next_or_close"))
		{
			SoundManager.Instance.PlayRightClick();
			var tcs = _weiter;
			_weiter = null;
			tcs.TrySetResult(true);
		}
	}

	/// <summary>
	/// Wickelt alle Verhandlungen ab, an denen der aktive Spieler beteiligt ist. Gibt es keine, kehrt
	/// die Methode sofort zurück (ohne den Bildschirm zu zeigen).
	/// </summary>
	public async Task ShowGericht()
	{
		var manager = new GerichtsverhandlungManager();
		var indizes = manager.GetVerhandlungenMitAktivemSpieler().ToList();

		if (indizes.Count == 0)
			return;

		_labelHaupt.Text = "";
		_labelUrteile.Text = "";
		DialogBase.ZeigeUeberNachrichtenschirm(this);
		SetProcessInput(true);

		foreach (int index in indizes)
			await FuehreVerhandlungDurch(manager, index);

		Hide();
		SetProcessInput(false);
	}

	private async Task FuehreVerhandlungDurch(GerichtsverhandlungManager manager, int index)
	{
		var info = manager.StarteVerhandlung(index);

		// Vorstellung des Prozesses
		SetzeHaupt(info.KlaegerName + " hat einen Prozess gegen " + info.AngeklagterName + "\ninitiiert. " + info.RollenText);
		await WarteAufWeiter();

		// Vorwürfe je Gesetzeskategorie
		await ZeigeKategorie(info.FinanzVorwuerfe);
		await ZeigeKategorie(info.StrafVorwuerfe);
		await ZeigeKategorie(info.KirchVorwuerfe);

		// Verteidigung/Aussage des Angeklagten: Ist der aktive Spieler selbst angeklagt, wählt er eine
		// Aussage (Issue #18), die Verurteilung und Strafmaß beeinflusst. Sonst die feste KI-Verteidigung.
		if (manager.IstAngeklagterAktiverSpieler())
		{
			AussageOption gewaehlt = await WaehleAussage(manager);
			manager.SetzeAussage(gewaehlt.Typ);
			SetzeHaupt(gewaehlt.Spruch);
		}
		else
		{
			SetzeHaupt(manager.GetVerteidigung());
		}

		await WarteAufWeiter();

		// Bestechung (Issue #18): Ist der aktive Spieler Partei (Angeklagter oder Kläger), kann er vor
		// dem Urteil die Richter (und später die Zeugen) bestechen.
		await WickleBestechungAb(manager);

		// Zeugenaussagen (Issue #18): Jeder Zeuge sagt aus – Verhältnis (und ggf. Bestechung) bestimmen,
		// ob für oder gegen den Angeklagten. Muss nach der Bestechung erfolgen (sie kann Zeugen umstimmen).
		foreach (var zeugenAussage in manager.ErmittleZeugenAussagen())
		{
			SetzeHaupt(zeugenAussage.Text);
			await WarteAufWeiter();
		}

		// Plädoyers (Issue #18): Anklage und Verteidigung tragen ihre Schlussworte vor (Ton nach
		// Beweislast bzw. Ansehen des Angeklagten).
		SetzeHaupt(manager.GetAnklageplaedoyer());
		await WarteAufWeiter();

		SetzeHaupt(manager.GetVerteidigungsplaedoyer());
		await WarteAufWeiter();

		// Zeugen vernommen, Entscheidung
		SetzeHaupt("Das hohe Gericht hat alle Zeugen vernommen.\n Es kommt nun zu einer Entscheidung durch das Gericht.");
		await WarteAufWeiter();

		// Offenlegung, ob in diesem Verfahren bestochen wurde
		if (manager.WurdeBestochen())
		{
			SetzeHaupt(manager.GetBestechungsOffenlegung());
			await WarteAufWeiter();
		}

		// Die drei Richter stimmen ab
		_labelUrteile.Text = "";

		for (int i = 0; i < GerichtsverhandlungManager.RichterAnzahl; i++)
		{
			bool schuldig;

			if (manager.IstRichterMensch(i))
			{
				SetProcessInput(false);
				schuldig = await SW.UI.YesNoQuestion.ShowDialogText(
					"Für welches Urteil wollt Ihr stimmen,\n " + manager.GetRichterName(i) + "?", "Schuldig", "Nicht schuldig") == DialogResultGame.Yes;

				if (Visible)
					SetProcessInput(true);
			}
			else
			{
				schuldig = manager.BerechneKiUrteil(i);
			}

			manager.SetzeUrteil(i, schuldig);

			SetzeHaupt(manager.GetRichterName(i) + " stimmt für...");
			await WarteAufWeiter();

			_labelUrteile.Text += manager.GetUrteilText(i) + "\n\n";
			await WarteAufWeiter();
		}

		// Ergebnis und ggf. Strafe
		var ergebnis = manager.WerteAus();

		SetzeHaupt(ergebnis.ErgebnisText);
		await WarteAufWeiter();

		if (!ergebnis.Freigesprochen)
		{
			SetzeHaupt(ergebnis.UrteilText);
			await WarteAufWeiter();

			SetzeHaupt(ergebnis.StrafeText);
			await WarteAufWeiter();
		}

		_labelUrteile.Text = "";
		manager.SchliesseVerhandlung();
	}

	/// <summary>
	/// Lässt den angeklagten Spieler eine Aussage wählen: zeigt die Optionen als Knöpfe und wartet auf die
	/// Auswahl (kein Rechtsklick-Weiter in diesem Schritt – es muss ein Knopf gedrückt werden).
	/// </summary>
	private async Task<AussageOption> WaehleAussage(GerichtsverhandlungManager manager)
	{
		var optionen = manager.GetAussageOptionen();
		int index = await WaehleOption("Wie wollt Ihr Euch zu den Vorwürfen äußern?", optionen.Select(o => o.ButtonText).ToList());
		return optionen[index];
	}

	/// <summary>
	/// Bietet dem Spieler, wenn er Partei ist, vor dem Urteil die Bestechung an: erst die Richter, dann –
	/// sofern Zeugen aussagen – die Zeugen. Nur bezahlbare Stufen werden angeboten; die Wahl zieht den
	/// Betrag sofort ab.
	/// </summary>
	private async Task WickleBestechungAb(GerichtsverhandlungManager manager)
	{
		if (!manager.KannBestechen())
			return;

		var richterOptionen = manager.GetRichterBestechungsOptionen();

		if (richterOptionen.Count > 1)
		{
			string frage = manager.IstAngeklagterAktiverSpieler()
				? "Wollt Ihr die Richter bestechen, um einen Freispruch zu erwirken?"
				: "Wollt Ihr die Richter bestechen, um eine Verurteilung zu erwirken?";

			int index = await WaehleOption(frage, richterOptionen.Select(o => o.ButtonText).ToList());
			manager.SetzeRichterBestechung(richterOptionen[index].Betrag);
		}

		// Zeugen-Bestechung: nur wenn Zeugen aussagen.
		if (manager.GetZeugenAnzahl() > 0)
		{
			var zeugenOptionen = manager.GetZeugenBestechungsOptionen();

			if (zeugenOptionen.Count > 1)
			{
				int index = await WaehleOption("Wollt Ihr die Zeugen bestechen?", zeugenOptionen.Select(o => o.ButtonText).ToList());
				manager.SetzeZeugenBestechung(zeugenOptionen[index].Betrag);
			}
		}
	}

	/// <summary>
	/// Zeigt eine Frage samt Auswahl-Knöpfen und wartet auf den Klick; liefert den Index der gewählten Option.
	/// Kein Rechtsklick-Weiter in diesem Schritt – es muss ein Knopf gedrückt werden.
	/// </summary>
	private async Task<int> WaehleOption(string frage, System.Collections.Generic.IReadOnlyList<string> buttonTexte)
	{
		SetzeHaupt(frage);
		LeereAussageAuswahl();

		_optionGewaehlt = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

		for (int i = 0; i < buttonTexte.Count; i++)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = buttonTexte[i];
			StyleAuswahlKnopf(button);

			int index = i;
			button.Pressed += () => _optionGewaehlt?.TrySetResult(index);

			_aussageAuswahl.AddChild(button);
		}

		_aussageAuswahl.Visible = true;

		int gewaehlt = await _optionGewaehlt.Task;

		_optionGewaehlt = null;
		_aussageAuswahl.Visible = false;
		LeereAussageAuswahl();

		return gewaehlt;
	}

	/// <summary>
	/// Setzt die im Code erzeugten Auswahlknöpfe auf die Schriftrolle: Ohne diese Überschreibungen erben
	/// sie das dunkelbraune Pergament-Theme und sind vor dem dunklen Gerichtsbild kaum zu lesen. Zugleich
	/// zentriert <see cref="Control.SizeFlags.ShrinkCenter"/> sie, statt sie über die volle Breite des
	/// VBox zu strecken und den Text links kleben zu lassen. Gleiche Gestaltung wie im
	/// <see cref="DuellDialog"/>.
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

	private void LeereAussageAuswahl()
	{
		foreach (Node child in _aussageAuswahl.GetChildren())
		{
			_aussageAuswahl.RemoveChild(child);
			child.QueueFree();
		}
	}

	private async Task ZeigeKategorie(VorwurfKategorie kategorie)
	{
		if (kategorie.Ueberschrift == null)
			return;

		SetzeHaupt(kategorie.Ueberschrift);
		await WarteAufWeiter();

		foreach (string vorwurf in kategorie.Vorwuerfe)
		{
			SetzeHaupt(vorwurf);
			await WarteAufWeiter();
		}
	}

	private void SetzeHaupt(string text)
	{
		_labelHaupt.Text = text;
	}

	private Task WarteAufWeiter()
	{
		_weiter = new TaskCompletionSource<bool>();
		return _weiter.Task;
	}
}
