using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die interaktive Wahl-Auszählung am Jahresende (Migration von WahlMitIDxAbhalten): zeigt die
/// Kandidaten, lässt menschliche Wähler per Klick abstimmen, KI-Wähler stimmen nach Sympathie ab,
/// zählt die Stimmen aus (Los bei Gleichstand) und verkündet den Gewinner. Die Auswertung und
/// Amtsvergabe erledigt der AemterManager.
/// </summary>
public partial class WahlDialog : Control
{
	[Export]
	public NodePath LabelTitlePath { get; set; }

	[Export]
	public NodePath LabelInfoPath { get; set; }

	[Export]
	public NodePath VBoxKandidatenPath { get; set; }

	[Export]
	public NodePath VBoxStimmenPath { get; set; }

	[Export]
	public NodePath LinkWeiterPath { get; set; }

	private Label _labelTitle;
	private Label _labelInfo;
	private Label _labelHinweis;
	private VBoxContainer _vBoxKandidaten;
	private VBoxContainer _vBoxStimmen;
	private controls.LinkButtonWithSounds _linkWeiter;
	private PackedScene _linkButtonScene;

	/// <summary>Schriftfarbe auf dem dunklen Ratstisch – das Standardthema wäre dort unlesbar.</summary>
	private static readonly Color Goldschrift = new(0.93f, 0.83f, 0.55f);
	private static readonly Color GoldschriftHell = new(1f, 0.95f, 0.7f);
	private static readonly Color Randfarbe = new(0.05f, 0.04f, 0.02f);

	/// <summary>Kantenlänge eines Stimmsymbols; passt zur 30er-Schrift der Kandidatennamen.</summary>
	private const int Muenzgroesse = 34;

	/// <summary>Abstand zwischen zwei Stimmsymbolen (im Original 35 px Rasterabstand bei 31 px Symbol).</summary>
	private const int Muenzabstand = 4;

	private AemterManager _aemterManager;
	private readonly List<Label> _kandidatenLabels = new List<Label>();

	/// <summary>Je Kandidat der Kasten rechts neben dem Namen, in dem die Stimmen als Münzen liegen.</summary>
	private readonly List<HBoxContainer> _stimmenKaesten = new List<HBoxContainer>();

	private List<WahlKandidat> _kandidaten;
	private Texture2D _muenze;

	private TaskCompletionSource<bool> _weiter;
	private TaskCompletionSource<int> _stimme;

	public override void _Ready()
	{
		_labelTitle = GetNode<Label>(LabelTitlePath);
		_labelInfo = GetNode<Label>(LabelInfoPath);
		_labelHinweis = GetNode<Label>("LabelHinweis");
		_vBoxKandidaten = GetNode<VBoxContainer>(VBoxKandidatenPath);
		_vBoxStimmen = GetNode<VBoxContainer>(VBoxStimmenPath);
		_linkWeiter = GetNode<controls.LinkButtonWithSounds>(LinkWeiterPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
		_muenze = GD.Load<Texture2D>("res://assets/images/symbole/SymbStimme.png");

		_linkWeiter.Pressed += () => _weiter?.TrySetResult(true);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		// Rechtsklick schaltet wie im Original einen Schritt weiter (nur während "Weiter"-Schritten).
		// Die kantengenaue Event-Prüfung verhindert, dass ein Klick versehentlich zwei Schritte überspringt.
		if (@event.IsActionPressed("ui_next_or_close") && _weiter != null)
		{
			SoundManager.Instance.PlayRightClick();
			_weiter.TrySetResult(true);
		}
	}

	/// <summary>
	/// Hält die Wahl mit der angegebenen ID ab: Kandidaten zeigen, abstimmen lassen, Gewinner verkünden
	/// und das Amt vergeben. Kehrt zurück, wenn die Wahl abgeschlossen ist.
	/// </summary>
	public async Task ShowWahl(AemterManager aemterManager, int wahlId)
	{
		_aemterManager = aemterManager;

		DialogBase.ZeigeUeberNachrichtenschirm(this);
		SetProcessInput(true);

		await RunWahl(wahlId);

		Hide();
		SetProcessInput(false);
	}

	private async Task RunWahl(int wahlId)
	{
		var ansicht = _aemterManager.ErstelleWahlAnsicht(wahlId);
		_kandidaten = ansicht.Kandidaten;

		_labelTitle.Text = "Wahl des " + ansicht.AmtName + " in " + ansicht.GebietName;
		BaueKandidaten(ansicht.Waehler.Count);
		LeereStimmButtons();

		var kandidatenIds = new List<int>();
		foreach (var kandidat in ansicht.Kandidaten)
			kandidatenIds.Add(kandidat.SpielerId);

		var tally = new int[ansicht.Kandidaten.Count];

		if (ansicht.IstLoswahl)
		{
			_labelInfo.Text = "Für dieses Amt haben sich folgende Bewerber aufgestellt.";
			await WarteWeiter();

			_labelInfo.Text = "Mangels Wählern wird diese Wahl durch ein Los entschieden...";
			await WarteWeiter();

			var losErgebnis = _aemterManager.WerteWahlAus(wahlId, new List<int>());
			await ZeigeErgebnis(losErgebnis);
			return;
		}

		_labelInfo.Text = "Für die Wahl des " + ansicht.AmtName + " haben sich folgende Bewerber aufgestellt.";
		await WarteWeiter();

		var stimmen = new List<int>();

		foreach (var waehler in ansicht.Waehler)
		{
			int stimme;

			if (waehler.IstMensch)
			{
				_labelInfo.Text = waehler.Name + ", bitte trefft Eure Wahl:";
				stimme = await WarteAufStimme();
			}
			else
			{
				stimme = _aemterManager.ErmittleKiStimme(waehler.SpielerId, kandidatenIds);
			}

			stimmen.Add(stimme);
			tally[stimme]++;
			AktualisiereStimmen(tally);

			_labelInfo.Text = waehler.Name + " stimmt für " + ansicht.Kandidaten[stimme].Name;
			await WarteWeiter();
		}

		var ergebnis = _aemterManager.WerteWahlAus(wahlId, stimmen);

		if (ergebnis.WarLoswahl)
		{
			_labelInfo.Text = "Aufgrund von Stimmengleichheit wird ein Los für die Entscheidung gezogen...";
			await WarteWeiter();
		}

		await ZeigeErgebnis(ergebnis);
	}

	private async Task ZeigeErgebnis(WahlErgebnis ergebnis)
	{
		SoundManager.Instance.PlayLeftClick();
		_labelInfo.Text = "Damit geht das Amt des " + ergebnis.AmtName + " in " + ergebnis.GebietName + " an\n" + ergebnis.GewinnerName;
		await WarteWeiter();
	}

	/// <summary>
	/// Baut je Kandidat eine Zeile aus Name und dem Kasten für die Stimmen. Der Kasten bekommt schon
	/// jetzt die Breite für <paramref name="waehlerAnzahl"/> Münzen reserviert: Sonst wanderte der
	/// zentrierte Name bei jeder eintreffenden Stimme nach links.
	/// </summary>
	private void BaueKandidaten(int waehlerAnzahl)
	{
		foreach (Node child in _vBoxKandidaten.GetChildren())
		{
			_vBoxKandidaten.RemoveChild(child);
			child.QueueFree();
		}

		_kandidatenLabels.Clear();
		_stimmenKaesten.Clear();

		float reserve = waehlerAnzahl <= 0
			? 0
			: waehlerAnzahl * Muenzgroesse + (waehlerAnzahl - 1) * Muenzabstand;

		foreach (var kandidat in _kandidaten)
		{
			// Die Zeile trägt Name und Münzen nebeneinander und wird als Ganzes zentriert.
			var zeile = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			zeile.AddThemeConstantOverride("separation", 12);

			var label = new Label
			{
				Text = kandidat.Name,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};

			// Die Kandidaten stehen auf dem dunklen Ratstisch – ohne Goldschrift wären sie unlesbar.
			label.AddThemeColorOverride("font_color", Goldschrift);
			label.AddThemeColorOverride("font_outline_color", Randfarbe);
			label.AddThemeConstantOverride("outline_size", 6);
			label.AddThemeFontSizeOverride("font_size", 30);

			var kasten = new HBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Begin,
				CustomMinimumSize = new Vector2(reserve, Muenzgroesse)
			};
			kasten.AddThemeConstantOverride("separation", Muenzabstand);

			zeile.AddChild(label);
			zeile.AddChild(kasten);

			_kandidatenLabels.Add(label);
			_stimmenKaesten.Add(kasten);
			_vBoxKandidaten.AddChild(zeile);
		}
	}

	/// <summary>
	/// Legt je abgegebener Stimme eine Münze neben den Kandidaten – wie im WinForms-Original, das die
	/// Stimmen als <c>SymbStimme</c>-Bilder rechts neben den Namen setzte, statt sie auszuzählen.
	/// </summary>
	private void AktualisiereStimmen(int[] tally)
	{
		for (int i = 0; i < _stimmenKaesten.Count; i++)
		{
			var kasten = _stimmenKaesten[i];

			foreach (Node child in kasten.GetChildren())
			{
				kasten.RemoveChild(child);
				child.QueueFree();
			}

			for (int stimme = 0; stimme < tally[i]; stimme++)
			{
				// ExpandMode zuerst: KeepSize erzwänge sonst die native Größe der Textur als Mindestmaß.
				var muenze = new TextureRect
				{
					Texture = _muenze,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(Muenzgroesse, Muenzgroesse)
				};

				kasten.AddChild(muenze);
			}
		}
	}

	private void ZeigeStimmButtons()
	{
		LeereStimmButtons();

		for (int i = 0; i < _kandidaten.Count; i++)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = "Für " + _kandidaten[i].Name + " stimmen";

			button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			button.AddThemeColorOverride("font_color", Goldschrift);
			button.AddThemeColorOverride("font_hover_color", GoldschriftHell);
			button.AddThemeColorOverride("font_outline_color", Randfarbe);
			button.AddThemeConstantOverride("outline_size", 6);
			button.AddThemeFontSizeOverride("font_size", 26);

			int index = i;
			button.Pressed += () => _stimme?.TrySetResult(index);

			_vBoxStimmen.AddChild(button);
		}

		_vBoxStimmen.Visible = true;
	}

	private void LeereStimmButtons()
	{
		foreach (Node child in _vBoxStimmen.GetChildren())
		{
			_vBoxStimmen.RemoveChild(child);
			child.QueueFree();
		}

		_vBoxStimmen.Visible = false;
	}

	private async Task WarteWeiter()
	{
		_linkWeiter.Visible = true;
		_labelHinweis.Visible = true;
		_weiter = new TaskCompletionSource<bool>();
		await _weiter.Task;
		_weiter = null;
	}

	private async Task<int> WarteAufStimme()
	{
		// Während der Stimmabgabe ist ein Knopf zu drücken – „Weiter" und sein Hinweis wären irreführend.
		_linkWeiter.Visible = false;
		_labelHinweis.Visible = false;
		ZeigeStimmButtons();

		_stimme = new TaskCompletionSource<int>();
		int index = await _stimme.Task;
		_stimme = null;

		LeereStimmButtons();
		return index;
	}
}
