using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Infoanzeige zu einer Wahl (Migration von BewerbInfos): zeigt die Wähler und die Mitbewerber
/// bzw. den Hinweis, dass die Wahl durch ein Los entschieden wird.
/// </summary>
public partial class BewerbInfosDialog : Control
{
	[Export]
	public NodePath LabelWaehlerPath { get; set; }

	[Export]
	public NodePath LabelMitbewerberPath { get; set; }

	private Label _labelWaehler;
	private Label _labelMitbewerber;

	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelWaehler = GetNode<Label>(LabelWaehlerPath);
		_labelMitbewerber = GetNode<Label>(LabelMitbewerberPath);

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(AemterManager aemterManager, int wahlId)
	{
		var details = aemterManager.GetWahlDetails(wahlId);

		if (details.IstLoswahl)
			_labelWaehler.Text = "Wähler:\nDie Wahl wird durch ein Los entschieden";
		else
			_labelWaehler.Text = "Wähler:\n" + string.Join("\n", details.WaehlerNamen);

		_labelMitbewerber.Text = "Mitbewerber:\n" + string.Join("\n", details.MitbewerberNamen);

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void _on_link_button_close_pressed()
	{
		CloseDialog();
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
