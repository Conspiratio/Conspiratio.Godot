using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Privilegien.Weltkarte;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Ämter-Ebene (Migration von AemterEbene): zeigt für ein Gebiet (Stadt/Land/Reich) die Ämter
/// je politischer, kirchlicher und militärischer Ebene mit ihren Inhabern. Über den Wechsel-Button
/// blättert man durch die Ebenen; ein Klick auf einen Amtsinhaber führt die Modus-Aktion aus
/// (Prozess initiieren / Hand des Henkers). Die Struktur liegt im AemterEbeneManager der Lib.
/// </summary>
public partial class AemterEbeneDialog : Control
{
	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath ButtonWechselPath { get; set; }

	[Export]
	public NodePath VBoxAemterPath { get; set; }

	private Label _labelTitel;
	private controls.ButtonWithSounds _buttonWechsel;
	private VBoxContainer _vBoxAemter;

	private AemterEbeneManager _manager;
	private int _modus;
	private int _ebene;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_buttonWechsel = GetNode<controls.ButtonWithSounds>(ButtonWechselPath);
		_vBoxAemter = GetNode<VBoxContainer>(VBoxAemterPath);

		_buttonWechsel.Pressed += OnWechselPressed;

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Öffnet die Ämter-Ebene für ein Gebiet (Stufe 0/1/2 = Stadt/Land/Reich) im gegebenen Modus.</summary>
	public Task ShowDialog(int objektId, int stufe, int modus)
	{
		_manager = new AemterEbeneManager(objektId, stufe);
		_modus = modus;
		_ebene = 0;

		_labelTitel.Text = _manager.GetTitel(modus);
		Fill();

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	private void OnWechselPressed()
	{
		_ebene = (_ebene + 1) % _manager.AnzahlEbenen;
		Fill();
	}

	private void Fill()
	{
		foreach (Node child in _vBoxAemter.GetChildren())
		{
			_vBoxAemter.RemoveChild(child);
			child.QueueFree();
		}

		_buttonWechsel.Text = _manager.GetEbenenName(_ebene);

		var aemter = _manager.GetAemter(_ebene);

		// Erstes Amt (das ranghöchste) zentriert oben, die übrigen in Reihen zu je drei.
		HBoxContainer aktuelleReihe = null;
		for (int i = 0; i < aemter.Count; i++)
		{
			if (i == 0 || (i - 1) % 3 == 0)
			{
				aktuelleReihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
				aktuelleReihe.AddThemeConstantOverride("separation", 20);
				_vBoxAemter.AddChild(aktuelleReihe);
			}

			var amt = aemter[i];

			var button = new controls.ButtonWithSounds
			{
				Text = amt.AmtName + "\n" + amt.HolderName,
				CustomMinimumSize = new Vector2(210, 72),
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				Disabled = !amt.Besetzt
			};

			int holderId = amt.HolderId;
			button.Pressed += () => OnAmtPressed(holderId);

			aktuelleReihe.AddChild(button);
		}
	}

	private async void OnAmtPressed(int holderId)
	{
		if (holderId == 0)
			return;

		SetProcessInput(false);

		await _manager.PersonWasMachen(holderId, _modus);

		// Wie im Original: nach dem Prozess (Modus 8) schließt die Ämter-Ebene; sonst bleibt sie offen.
		if (_modus == 8)
		{
			CloseDialog();
			return;
		}

		if (Visible)
		{
			Fill();
			SetProcessInput(true);
		}
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
}
