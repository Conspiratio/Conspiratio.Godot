using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Testament (Migration von Testamentanzeigen): der Spieler bestimmt zu Lebzeiten seinen Erben,
/// indem er durch die möglichen Erben (Erzbistum, Ehepartner, Kinder) klickt. Implementiert
/// ITestamentAnzeigenDialog, damit das Testament-Privileg den Dialog öffnen kann.
/// </summary>
public partial class TestamentDialog : Control, ITestamentAnzeigenDialog
{
	/// <summary>
	/// Aufruf über das Testament-Privileg (zu Lebzeiten, tod = false). Der Todesfall wird direkt
	/// über den Kontor abgewickelt und läuft nicht über diese Schnittstelle.
	/// </summary>
	void ITestamentAnzeigenDialog.ShowDialog(bool tod)
	{
		// Fire-and-forget: die Schnittstelle ist synchron, der Godot-Dialog läuft asynchron
		_ = ShowDialog(new FamilieManager());
	}

	[Export]
	public NodePath ButtonErbePath { get; set; }

	private controls.ButtonWithSounds _buttonErbe;

	private FamilieManager _familieManager;
	private List<ErbeOption> _optionen;
	private int _index;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_buttonErbe = GetNode<controls.ButtonWithSounds>(ButtonErbePath);
		_buttonErbe.Pressed += OnErbePressed;

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(FamilieManager familieManager)
	{
		_familieManager = familieManager;
		_optionen = _familieManager.GetErbeOptionen();

		int aktuellerErbe = _familieManager.GetAktuellerErbeId();
		_index = 0;

		for (int i = 0; i < _optionen.Count; i++)
		{
			if (_optionen[i].ErbeId == aktuellerErbe)
			{
				_index = i;
				break;
			}
		}

		UpdateErbeButton();

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private void UpdateErbeButton()
	{
		_buttonErbe.Text = _optionen[_index].Bezeichnung;
	}

	private void OnErbePressed()
	{
		_index = (_index + 1) % _optionen.Count;
		_familieManager.SetzeErbe(_optionen[_index].ErbeId);
		UpdateErbeButton();
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
