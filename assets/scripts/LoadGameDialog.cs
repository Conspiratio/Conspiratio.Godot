using System.Collections.Generic;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Lade-Dialog: listet alle vorhandenen Spielstände auf und lädt oder löscht den ausgewählten.
/// </summary>
public partial class LoadGameDialog : Control
{
	[Export]
	public NodePath ItemListSpielstaendePath { get; set; }

	private ItemList _itemListSpielstaende;
	private readonly List<string> _spielstandNamen = new();

	private Main _main;
	private SpeicherManager _speicherManager;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_itemListSpielstaende = GetNode<ItemList>(ItemListSpielstaendePath);
		_main = GetParent<Main>();

		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		HideAndDisableInput();
		_main.LocalGameDialog.ShowAndEnableInput();
	}

	public void ShowAndEnableInput()
	{
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		PopulateSpielstaende();

		Show();
		SetProcessInput(true);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
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

		if (_itemListSpielstaende.ItemCount > 0)
			_itemListSpielstaende.Select(0);
	}

	private string GetAusgewaehlterSpielstand()
	{
		var auswahl = _itemListSpielstaende.GetSelectedItems();
		return auswahl.Length == 0 ? null : _spielstandNamen[auswahl[0]];
	}

	private async void _on_link_button_load_pressed()
	{
		string name = GetAusgewaehlterSpielstand();

		if (name == null)
			return;

		SetProcessInput(false);

		if (_speicherManager.Laden(name, out string fehler))
		{
			ClientSettings.LetzterSpielstand = name;

			await SW.UI.ShowText.ShowDialog("Ladevorgang beendet!");

			HideAndDisableInput();
			_main.Kontor.ContinueLoadedGame();
			return;
		}

		await SW.UI.ShowText.ShowDialog(fehler);

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_link_button_delete_pressed()
	{
		string name = GetAusgewaehlterSpielstand();

		if (name == null)
			return;

		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr den Spielstand\n" + name + "\nwirklich löschen?") == DialogResultGame.Yes)
			_speicherManager.Loeschen(name);

		PopulateSpielstaende();

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_item_list_spielstaende_item_activated(long index)
	{
		_on_link_button_load_pressed();
	}
}
