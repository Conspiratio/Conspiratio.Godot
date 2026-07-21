using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Privilegien.Weltkarte;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Kontrahenten-Liste (Migration von KontrahentenForm): ein paginierter Ziel-Picker für die
/// Personen-Ziel-Privilegien der Weltkarte (Modus 8 = Prozess initiieren, Modus 13 = Hand des Henkers).
/// Menschliche Mitspieler stehen zuerst (dunkelrot), danach die KI. Die Logik liegt im
/// KontrahentenManager der Lib.
/// </summary>
public partial class KontrahentenDialog : Control
{
	private const int EintraegeProSeite = 10;

	[Export]
	public NodePath VBoxEintraegePath { get; set; }

	[Export]
	public NodePath LabelSeitePath { get; set; }

	private VBoxContainer _vBoxEintraege;
	private Label _labelSeite;
	private PackedScene _linkButtonScene;

	private KontrahentenManager _manager;
	private List<KontrahentInfo> _kontrahenten;
	private int _modus;
	private int _seite;
	private int _maxSeite;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_vBoxEintraege = GetNode<VBoxContainer>(VBoxEintraegePath);
		_labelSeite = GetNode<Label>(LabelSeitePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Öffnet die Kontrahenten-Liste für den angegebenen Weltkarte-Modus.</summary>
	public Task ShowDialog(int modus)
	{
		_manager = new KontrahentenManager();
		_modus = modus;
		_kontrahenten = _manager.GetKontrahenten();
		_seite = 0;
		_maxSeite = _kontrahenten.Count == 0 ? 0 : (_kontrahenten.Count - 1) / EintraegeProSeite;

		Fill();

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	private void Fill()
	{
		foreach (Node child in _vBoxEintraege.GetChildren())
		{
			_vBoxEintraege.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < EintraegeProSeite; i++)
		{
			int index = _seite * EintraegeProSeite + i;
			if (index >= _kontrahenten.Count)
				break;

			var kontrahent = _kontrahenten[index];

			var button = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			button.Text = kontrahent.Name;

			// Menschliche Mitspieler werden wie im Original dunkelrot hervorgehoben.
			if (kontrahent.IstMensch)
				button.AddThemeColorOverride("font_color", new Color(0.55f, 0f, 0f));

			int id = kontrahent.Id;
			button.Pressed += () => OnKontrahentPressed(id);

			_vBoxEintraege.AddChild(button);
		}

		_labelSeite.Text = (_seite + 1) + "/" + (_maxSeite + 1);
	}

	private void BlaettereZu(int neueSeite)
	{
		_seite = Mathf.Clamp(neueSeite, 0, _maxSeite);
		Fill();
	}

	private void _on_button_vor_pressed() => BlaettereZu(_seite + 1);
	private void _on_button_zurueck_pressed() => BlaettereZu(_seite - 1);
	private void _on_button_vor5_pressed() => BlaettereZu(_seite + 5);
	private void _on_button_zurueck5_pressed() => BlaettereZu(_seite - 5);

	private async void OnKontrahentPressed(int id)
	{
		SetProcessInput(false);

		await _manager.PersonWasMachen(id, _modus);

		// Wie im Original bleibt die Liste offen; erneute Aktionen fangen die Lib-Sperren ab.
		if (Visible)
			SetProcessInput(true);
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
