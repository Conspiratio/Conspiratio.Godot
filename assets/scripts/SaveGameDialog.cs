using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Speichern-Dialog: listet die vorhandenen Spielstände (zum Überschreiben anklickbar) und speichert
/// unter dem im Eingabefeld angegebenen Namen (vorbelegt mit dem aktuellen Spielnamen). Wird aus dem
/// Ingame-Menü (Esc) genutzt und meldet dem Aufrufer über <see cref="ZeigeUndSpeichere"/>, wenn er fertig ist.
/// </summary>
public partial class SaveGameDialog : Control
{
	[Export]
	public NodePath ItemListSpielstaendePath { get; set; }

	[Export]
	public NodePath LineEditNamePath { get; set; }

	private ItemList _itemListSpielstaende;
	private LineEdit _lineEditName;
	private readonly List<string> _spielstandNamen = new();

	private SpeicherManager _speicherManager;
	private TaskCompletionSource<bool> _abgeschlossen;

	public override void _Ready()
	{
		_itemListSpielstaende = GetNode<ItemList>(ItemListSpielstaendePath);
		_lineEditName = GetNode<LineEdit>(LineEditNamePath);
		_lineEditName.MaxLength = SW.Statisch.GetMaxNameLength();

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		Beenden(false);
	}

	/// <summary>Zeigt den Dialog; die Aufgabe endet, wenn gespeichert oder abgebrochen wurde.</summary>
	public Task ZeigeUndSpeichere()
	{
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		PopulateSpielstaende();
		_lineEditName.Text = SW.Dynamisch.SpielName;

		Show();
		MoveToFront();
		SetProcessInput(true);

		_abgeschlossen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _abgeschlossen.Task;
	}

	private void Beenden(bool gespeichert)
	{
		Hide();
		SetProcessInput(false);
		_abgeschlossen?.TrySetResult(gespeichert);
	}

	private void PopulateSpielstaende()
	{
		_itemListSpielstaende.Clear();
		_spielstandNamen.Clear();

		foreach (var spielstand in _speicherManager.GetSpielstaende())
		{
			_spielstandNamen.Add(spielstand.Name);
			_itemListSpielstaende.AddItem(spielstand.Name + "   (" + spielstand.GeaendertAm.ToString("dd.MM.yyyy HH:mm") + ")");
		}
	}

	private void _on_item_list_spielstaende_item_selected(long index)
	{
		_lineEditName.Text = _spielstandNamen[(int)index];
	}

	private void _on_line_edit_name_text_submitted(string _)
	{
		_on_link_button_save_pressed();
	}

	private async void _on_link_button_save_pressed()
	{
		string name = _lineEditName.Text?.Trim();

		if (string.IsNullOrEmpty(name))
			return;

		SetProcessInput(false);

		// Vorhandenen Spielstand nur nach Rückfrage überschreiben.
		if (_spielstandNamen.Contains(name) &&
		    await SW.UI.YesNoQuestion.ShowDialogText("Es gibt bereits einen Spielstand\n" + name + ".\nWollt Ihr ihn überschreiben?") != DialogResultGame.Yes)
		{
			if (Visible)
				SetProcessInput(true);
			return;
		}

		// Vor dem Speichern die spielübergreifenden Profile werten (Delta-Fold), damit der aktualisierte
		// Wertungs-Snapshot mit im Spielstand landet.
		new ProfilManager(ClientSettings.SavegamePath).WerteLaufendesSpiel();

		if (_speicherManager.Speichern(name, out string fehler))
		{
			ClientSettings.LetzterSpielstand = name;

			Hide();
			await SW.UI.ShowText.ShowDialog("Speichervorgang beendet");

			_abgeschlossen?.TrySetResult(true);
			return;
		}

		await SW.UI.ShowText.ShowDialog(fehler);

		if (Visible)
			SetProcessInput(true);
	}
}
