using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Privilegien.BauwerkStiften;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Bauwerk-stiften-Privileg (Migration von BauwerkStiftenForm): der Spieler wählt eine Stadt und
/// stiftet ihr für 5.000 Taler ein Bauwerk (Kirche, Kerker, Feuerwehr oder Hospital), was das
/// Permaansehen erhöht. Die Logik liegt im BauwerkStiftenManager der Lib.
/// </summary>
public partial class BauwerkStiftenDialog : DialogBase, IBauwerkStiftenDialog
{
	[Export]
	public NodePath LinkStadtPath { get; set; }

	[Export]
	public NodePath BauwerkeContainerPath { get; set; }

	private controls.LinkButtonWithSounds _linkStadt;
	private VBoxContainer _bauwerkeContainer;
	private controls.LinkButtonWithSounds[] _bauwerkButtons;

	private BauwerkStiftenManager _manager;

	protected override void OnReady()
	{
		_linkStadt = GetNode<controls.LinkButtonWithSounds>(LinkStadtPath);
		_bauwerkeContainer = GetNode<VBoxContainer>(BauwerkeContainerPath);

		_linkStadt.Pressed += OnStadtPressed;

		// Die Bauwerk-Buttons sind Kinder des Containers (in der Szene angelegt).
		var kinder = _bauwerkeContainer.GetChildren();
		_bauwerkButtons = new controls.LinkButtonWithSounds[kinder.Count];
		for (int i = 0; i < kinder.Count; i++)
		{
			int index = i; // Closure-Capture
			_bauwerkButtons[i] = (controls.LinkButtonWithSounds)kinder[i];
			_bauwerkButtons[i].Pressed += () => OnBauwerkPressed(index);
		}
	}

	/// <summary>
	/// Aufruf über das Bauwerk-stiften-Privileg. Öffnet den Dialog asynchron (fire-and-forget), da die
	/// Schnittstelle synchron ist.
	/// </summary>
	DialogResultGame IBauwerkStiftenDialog.ShowDialog()
	{
		_ = ShowBauwerkStiften();
		return DialogResultGame.None;
	}

	private async Task ShowBauwerkStiften()
	{
		_manager = new BauwerkStiftenManager();
		UpdateAnzeige();

		await ShowAndAwait();
	}

	private void UpdateAnzeige()
	{
		_linkStadt.Text = "Stadt: " + _manager.GetStadtName();
		for (int i = 0; i < _bauwerkButtons.Length && i < _manager.AnzahlBauwerke; i++)
			_bauwerkButtons[i].Text = _manager.GetBauwerkBeschriftung(i);
	}

	private void OnStadtPressed()
	{
		_manager.SetNextStadt();
		UpdateAnzeige();
	}

	private async void OnBauwerkPressed(int index)
	{
		SetProcessInput(false);

		if (!_manager.KannBezahlen(index))
		{
			await SW.UI.ShowText.ShowDialog(_manager.GetNichtGenugGoldMeldung(index));
			if (Visible)
				SetProcessInput(true);
			return;
		}

		var antwort = await SW.UI.YesNoQuestion.ShowDialogText(_manager.GetBestaetigungsfrage(index), "Ja", "Nein");
		if (antwort == DialogResultGame.Yes)
			_manager.FuehreStiftungAus(index);

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
