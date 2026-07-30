using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Spieltipps (Migration von TippsAnzeigen): zeigt zu Zugbeginn einen zufälligen Tipp; über "Zurück"
/// und "Weiter" lässt sich durch die Tipps blättern. Der Rechtsklick schließt die Anzeige. Die Tipps
/// liefert der TippsManager der Lib.
/// </summary>
public partial class TippsDialog : DialogBase
{
	[Export]
	public NodePath LabelUeberschriftPath { get; set; }

	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelUeberschrift;
	private Label _labelText;

	private readonly TippsManager _manager = new TippsManager();
	private int _index;

	protected override void OnReady()
	{
		_labelUeberschrift = GetNode<Label>(LabelUeberschriftPath);
		_labelText = GetNode<Label>(LabelTextPath);
	}

	/// <summary>Öffnet die Tipp-Anzeige mit einem zufälligen Tipp.</summary>
	public Task ShowDialog()
	{
		_index = _manager.ZufaelligerIndex();
		ZeigeTipp();

		return ShowAndAwait();
	}

	private void ZeigeTipp()
	{
		_labelUeberschrift.Text = "Tipp #" + (_index + 1);
		_labelText.Text = _manager.GetTipp(_index);
	}

	private void _on_button_zurueck_pressed()
	{
		_index = _manager.VorherigerIndex(_index);
		ZeigeTipp();
	}

	private void _on_button_weiter_pressed()
	{
		_index = _manager.NaechsterIndex(_index);
		ZeigeTipp();
	}
}
