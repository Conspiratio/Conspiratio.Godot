using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Privilegien.Weltkarte;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Kontrahenten-Liste (Migration von KontrahentenForm): ein paginierter Ziel-Picker für die
/// Personen-Ziel-Privilegien der Weltkarte (Modus 8 = Prozess initiieren, Modus 13 = Hand des Henkers).
/// Menschliche Mitspieler stehen zuerst (dunkelrot), danach die KI. Die Logik liegt im
/// KontrahentenManager der Lib.
/// </summary>
public partial class KontrahentenDialog : DialogBase
{
	/// <summary>Reine Übersicht aus der Schreibstube: ein Klick öffnet die Kontrahenten-Details (wie WinForms-Modus 14).</summary>
	public const int ModusUebersicht = 14;

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
	private Main _main;

	protected override void OnReady()
	{
		_vBoxEintraege = GetNode<VBoxContainer>(VBoxEintraegePath);
		_labelSeite = GetNode<Label>(LabelSeitePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
		_main = GetParentOrNull<Main>();
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

		return ShowAndAwait();
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

		// Reine Übersicht (Schreibstube): Klick öffnet die Kontrahenten-Details statt einer Zielaktion.
		if (_modus == ModusUebersicht)
			await _main.KontrahentDetailsDialog.ShowDialog(id);
		else
			await _manager.PersonWasMachen(id, _modus);

		// Wie im Original bleibt die Liste offen; erneute Aktionen fangen die Lib-Sperren ab.
		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
