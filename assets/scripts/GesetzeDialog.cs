using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Gesetzesanzeige: die drei Ebenen Finanzen, Justiz und Kirche mit ihrer
/// Strenge-Bewertung und den jeweils zehn Gesetzestexten (wie im WinForms-Original).
/// </summary>
public partial class GesetzeDialog : Control
{
	[Export]
	public NodePath LabelUeberschriftPath { get; set; }

	[Export]
	public NodePath VBoxGesetzePath { get; set; }

	private Label _labelUeberschrift;
	private VBoxContainer _vBoxGesetze;
	private SchreibstubeManager _schreibstubeManager;

	private TaskCompletionSource<bool> _dialogClosed;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelUeberschrift = GetNode<Label>(LabelUeberschriftPath);
		_vBoxGesetze = GetNode<VBoxContainer>(VBoxGesetzePath);

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(SchreibstubeManager schreibstubeManager)
	{
		_schreibstubeManager = schreibstubeManager;
		ZeigeEbene(0);

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
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

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void CloseDialog()
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(true);
	}

	private Task CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}
}
