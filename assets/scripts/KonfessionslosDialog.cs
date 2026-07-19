using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Glaubensfrage für Konfessionslose (Migration von FormKonfessionslos): der Spieler wählt, ob er
/// den evangelischen oder katholischen Glauben annimmt – oder vorerst konfessionslos bleibt.
/// </summary>
public partial class KonfessionslosDialog : Control
{
	private KircheManager _kircheManager;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(KircheManager kircheManager)
	{
		_kircheManager = kircheManager;

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private void _on_link_evangelisch_pressed()
	{
		_kircheManager.NimmReligionAn(false);
		SoundManager.Instance.PlayLeftClick();
		CloseDialog();
	}

	private void _on_link_katholisch_pressed()
	{
		_kircheManager.NimmReligionAn(true);
		SoundManager.Instance.PlayLeftClick();
		CloseDialog();
	}

	private void _on_link_keinen_pressed()
	{
		CloseDialog();
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
