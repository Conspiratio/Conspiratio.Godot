using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Fechtunterricht (Issue #17): Der Spieler kann wiederholt Fechtstunden nehmen, um seine Duell-Fähigkeit
/// zu steigern. Der Preis pro Stunde steigt mit jeder genommenen Stunde. Rechtsklick/Esc schließt.
/// </summary>
public partial class FechtunterrichtDialog : DialogBase
{
	[Export] public NodePath LabelTextPath { get; set; }
	[Export] public NodePath ButtonStundePath { get; set; }
	[Export] public NodePath LabelStatusPath { get; set; }

	private Label _labelText;
	private ButtonWithSounds _buttonStunde;
	private Label _labelStatus;

	private FechtDuellManager _manager;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_buttonStunde = GetNode<ButtonWithSounds>(ButtonStundePath);
		_labelStatus = GetNode<Label>(LabelStatusPath);

		_buttonStunde.Pressed += OnStundeNehmen;
	}

	public Task ShowDialog()
	{
		_manager = new FechtDuellManager();
		_labelStatus.Text = "";
		Aktualisiere();

		return ShowAndAwait();
	}

	private void Aktualisiere()
	{
		_labelText.Text = _manager.GetAngebotstext();
		_buttonStunde.Disabled = !_manager.KannFechtstundeBezahlen();
	}

	private void OnStundeNehmen()
	{
		if (_manager.NimmFechtstunde(out string meldung))
			SoundManager.Instance.PlayCoins();

		_labelStatus.Text = meldung;
		Aktualisiere();
	}
}
