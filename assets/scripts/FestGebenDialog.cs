using System;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Privilegien.FestGeben;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Fest-geben-Privileg (Migration von frmFestGeben): der Spieler plant ein Fest, indem er Ort,
/// Größe, Musiker und Jahr wählt. Das Fest wird im gewählten Jahr zu Zugbeginn automatisch gefeiert
/// (siehe Kontor). Die Logik liegt im FestManager der Lib.
/// </summary>
public partial class FestGebenDialog : DialogBase, IFestGebenDialog
{
	[Export]
	public NodePath LinkOrtPath { get; set; }

	[Export]
	public NodePath LinkGroessePath { get; set; }

	[Export]
	public NodePath LinkMusikerPath { get; set; }

	[Export]
	public NodePath LinkJahrPath { get; set; }

	private controls.LinkButtonWithSounds _linkOrt;
	private controls.LinkButtonWithSounds _linkGroesse;
	private controls.LinkButtonWithSounds _linkMusiker;
	private controls.LinkButtonWithSounds _linkJahr;

	private FestManager _festManager;
	private int _jahr;

	protected override void OnReady()
	{
		_linkOrt = GetNode<controls.LinkButtonWithSounds>(LinkOrtPath);
		_linkGroesse = GetNode<controls.LinkButtonWithSounds>(LinkGroessePath);
		_linkMusiker = GetNode<controls.LinkButtonWithSounds>(LinkMusikerPath);
		_linkJahr = GetNode<controls.LinkButtonWithSounds>(LinkJahrPath);

		_linkOrt.Pressed += OnOrtPressed;
		_linkGroesse.Pressed += OnGroessePressed;
		_linkMusiker.Pressed += OnMusikerPressed;
		_linkJahr.Pressed += OnJahrPressed;
	}

	/// <summary>
	/// Aufruf über das Fest-geben-Privileg. Öffnet den Dialog asynchron (fire-and-forget), da die
	/// Schnittstelle synchron ist.
	/// </summary>
	DialogResultGame IFestGebenDialog.ShowDialog()
	{
		_ = ShowFest();
		return DialogResultGame.None;
	}

	private async Task ShowFest()
	{
		_festManager = new FestManager();
		_jahr = _festManager.Jahr;

		UpdateAnzeige();

		await ShowAndAwait();
	}

	private void UpdateAnzeige()
	{
		_linkOrt.Text = "Ort: " + _festManager.GetStadtName();
		_linkGroesse.Text = "Größe: " + _festManager.Groesse;
		_linkMusiker.Text = "Musiker: " + _festManager.Musiker;
		_linkJahr.Text = "Jahr: " + _jahr;
	}

	private void OnOrtPressed()
	{
		_festManager.SetNextStadtID();
		UpdateAnzeige();
	}

	private void OnGroessePressed()
	{
		_festManager.SetNextGroesse();
		UpdateAnzeige();
	}

	private void OnMusikerPressed()
	{
		_festManager.SetNextMusiker();
		UpdateAnzeige();
	}

	private void OnJahrPressed()
	{
		_jahr++;

		if (_jahr > _festManager.GetMaxJahr())
			_jahr = _festManager.Jahr;

		UpdateAnzeige();
	}

	private async void _on_button_planen_pressed()
	{
		SetProcessInput(false);

		try
		{
			string message = _festManager.ErstelleNeuesFest(_festManager.StadtID, _festManager.Groesse, _festManager.Musiker, _jahr);
			SoundManager.Instance.PlayLeftClick();
			await SW.UI.ShowText.ShowDialog(message);
			Close(DialogResultGame.OK);
			return;
		}
		catch (Exception ex)
		{
			// z. B. wenn für dieses Jahr bereits ein Fest geplant ist
			await SW.UI.ShowText.ShowDialog(ex.Message);
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
