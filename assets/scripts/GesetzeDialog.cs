using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Gesetzesanzeige: die drei Ebenen Finanzen, Justiz und Kirche mit ihrer
/// Strenge-Bewertung und den jeweils zehn Gesetzestexten (wie im WinForms-Original).
/// </summary>
public partial class GesetzeDialog : DialogBase
{
	[Export]
	public NodePath LabelUeberschriftPath { get; set; }

	[Export]
	public NodePath VBoxGesetzePath { get; set; }

	private Label _labelUeberschrift;
	private VBoxContainer _vBoxGesetze;
	private SchreibstubeManager _schreibstubeManager;


	// Called when the node enters the scene tree for the first time.
	protected override void OnReady()
	{
		_labelUeberschrift = GetNode<Label>(LabelUeberschriftPath);
		_vBoxGesetze = GetNode<VBoxContainer>(VBoxGesetzePath);
	}

	public async Task ShowDialog(SchreibstubeManager schreibstubeManager)
	{
		_schreibstubeManager = schreibstubeManager;
		ZeigeEbene(0);

		await ShowAndAwait();
	}

	private void ZeigeEbene(int ebene)
	{
		_labelUeberschrift.Text = _schreibstubeManager.GetGesetzesEbenenUeberschrift(ebene);

		foreach (Node child in _vBoxGesetze.GetChildren())
		{
			_vBoxGesetze.RemoveChild(child);
			child.QueueFree();
		}

		foreach (string text in _schreibstubeManager.GetGesetzesTexte(ebene))
		{
			_vBoxGesetze.AddChild(new Label
			{
				Text = text,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				HorizontalAlignment = HorizontalAlignment.Center,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			});
		}
	}

	private void _on_button_finanzen_pressed()
	{
		SoundManager.Instance.PlayLeftClick();
		ZeigeEbene(0);
	}

	private void _on_button_justiz_pressed()
	{
		SoundManager.Instance.PlayLeftClick();
		ZeigeEbene(1);
	}

	private void _on_button_kirche_pressed()
	{
		SoundManager.Instance.PlayLeftClick();
		ZeigeEbene(2);
	}
}
