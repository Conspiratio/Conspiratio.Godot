using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Lade-Dialog: listet alle vorhandenen Spielstände auf und lädt oder löscht den ausgewählten.
/// Wird sowohl aus dem lokalen Spielmenü als auch aus dem Ingame-Menü (Esc) genutzt; er navigiert
/// nicht selbst, sondern meldet dem Aufrufer über <see cref="ZeigeUndLade"/>, ob geladen wurde.
/// </summary>
public partial class LoadGameDialog : Control
{
	[Export]
	public NodePath ItemListSpielstaendePath { get; set; }

	private ItemList _itemListSpielstaende;
	private readonly List<string> _spielstandNamen = new();

	private SpeicherManager _speicherManager;
	private TaskCompletionSource<bool> _abgeschlossen;

	public override void _Ready()
	{
		_itemListSpielstaende = GetNode<ItemList>(ItemListSpielstaendePath);

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

	/// <summary>
	/// Zeigt die Spielstandliste und wartet auf die Auswahl des Spielers: liefert <c>true</c>, wenn ein
	/// Spielstand geladen wurde (der Spielstand ist dann bereits gesetzt), sonst <c>false</c> (Abbruch).
	/// Der Aufrufer entscheidet danach, wie es weitergeht (Spiel fortsetzen bzw. zurück ins Menü).
	/// </summary>
	public Task<bool> ZeigeUndLade()
	{
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		PopulateSpielstaende();

		Show();
		MoveToFront();
		SetProcessInput(true);

		_abgeschlossen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _abgeschlossen.Task;
	}

	private void Beenden(bool geladen)
	{
		Hide();
		SetProcessInput(false);
		_abgeschlossen?.TrySetResult(geladen);
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

			// Zuerst ausblenden, dann bestätigen (sonst bleibt der Dialog unter der Meldung sichtbar).
			Hide();
			await SW.UI.ShowText.ShowDialog("Ladevorgang beendet!");

			_abgeschlossen?.TrySetResult(true);
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
