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
	private VBoxContainer _vBoxKandidaten;
	private VBoxContainer _vBoxStimmen;
	private controls.LinkButtonWithSounds _linkWeiter;
	private PackedScene _linkButtonScene;

	private AemterManager _aemterManager;
	private readonly List<Label> _kandidatenLabels = new List<Label>();
	private List<WahlKandidat> _kandidaten;

	private TaskCompletionSource<bool> _weiter;
	private TaskCompletionSource<int> _stimme;

	public override void _Ready()
	{
		_labelTitle = GetNode<Label>(LabelTitlePath);
		_labelInfo = GetNode<Label>(LabelInfoPath);
		_vBoxKandidaten = GetNode<VBoxContainer>(VBoxKandidatenPath);
		_vBoxStimmen = GetNode<VBoxContainer>(VBoxStimmenPath);
		_linkWeiter = GetNode<controls.LinkButtonWithSounds>(LinkWeiterPath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

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

		Show();
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
		BaueKandidaten();
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

	private void BaueKandidaten()
	{
		foreach (Node child in _vBoxKandidaten.GetChildren())
		{
			_vBoxKandidaten.RemoveChild(child);
			child.QueueFree();
		}

		_kandidatenLabels.Clear();

		foreach (var kandidat in _kandidaten)
		{
			var label = new Label
			{
				Text = kandidat.Name,
				HorizontalAlignment = HorizontalAlignment.Center
			};

			_kandidatenLabels.Add(label);
			_vBoxKandidaten.AddChild(label);
		}
	}

	private void AktualisiereStimmen(int[] tally)
	{
		for (int i = 0; i < _kandidatenLabels.Count; i++)
		{
			_kandidatenLabels[i].Text = _kandidaten[i].Name + (tally[i] > 0 ? "   (" + tally[i] + ")" : "");
		}
	}

	private void ZeigeStimmButtons()
	{
		LeereStimmButtons();

		for (int i = 0; i < _kandidaten.Count; i++)
		{
			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = "Für " + _kandidaten[i].Name + " stimmen";

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
		_weiter = new TaskCompletionSource<bool>();
		await _weiter.Task;
		_weiter = null;
	}

	private async Task<int> WarteAufStimme()
	{
		_linkWeiter.Visible = false;
		ZeigeStimmButtons();

		_stimme = new TaskCompletionSource<int>();
		int index = await _stimme.Task;
		_stimme = null;

		LeereStimmButtons();
		return index;
	}
}
