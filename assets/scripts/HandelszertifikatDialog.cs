using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Handelszertifikat-Verleihung (Migration von Handelszertifikat/HandelszertifikatAnzeigen): zeigt als
/// Urkunde den Erlass, mit dem dem Spieler der Handel mit einem neuen Rohstoff gestattet wird. Dazu erklingen
/// eine Fanfare und die passende Sprachausgabe. Der Rechtsklick (bzw. Esc) schließt die Urkunde. Der Vermerk
/// wird bereits vom HandelszertifikatManager der Lib zurückgesetzt.
/// </summary>
public partial class HandelszertifikatDialog : DialogBase
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelText;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
	}

	/// <summary>Zeigt die Urkunde der Handelszertifikat-Verleihung und schließt sie beim Rechtsklick.</summary>
	public Task ShowDialog(HandelszertifikatErgebnis ergebnis)
	{
		_labelText.Text = ergebnis.UrkundenText;

		SoundManager.Instance.PlayFanfare();
		SoundManager.Instance.SpieleStimmen(
			"res://assets/voice/31_auf_grund_eurer_besonderen_erfolge.wav",
			"res://assets/voice/31_wird_euch_ab_heute_gestattet_" + ergebnis.RohstoffName.ToLower() + ".wav");

		return ShowAndAwait();
	}
}
