using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Verwaltung der spielübergreifenden Profile (aus dem Hauptmenü): Profile anlegen, umbenennen, als
/// aktiv setzen, löschen und ihre spielübergreifende Statistik ansehen. Rechtsklick/Esc schließt.
/// </summary>
public partial class ProfilDialog : DialogBase
{
	[Export]
	public NodePath ItemListProfilePath { get; set; }

	[Export]
	public NodePath LineEditNamePath { get; set; }

	private ItemList _itemListProfile;
	private LineEdit _lineEditName;
	private readonly List<string> _profilIds = new();

	private ProfilManager _profilManager;
	private Main _main;

	protected override void OnReady()
	{
		_itemListProfile = GetNode<ItemList>(ItemListProfilePath);
		_lineEditName = GetNode<LineEdit>(LineEditNamePath);
		_main = GetParentOrNull<Main>();
	}

	/// <summary>Öffnet die Profilverwaltung.</summary>
	public Task ShowDialog()
	{
		_profilManager = new ProfilManager(ClientSettings.SavegamePath);
		_lineEditName.MaxLength = _profilManager.MaxLengthOfProfilName;
		Aktualisiere();
		return ShowAndAwait();
	}

	private void Aktualisiere()
	{
		_itemListProfile.Clear();
		_profilIds.Clear();

		var aktiv = _profilManager.GetAktivesProfil();

		foreach (var profil in _profilManager.GetProfile())
		{
			_profilIds.Add(profil.Id);
			string marke = aktiv != null && profil.Id == aktiv.Id ? "  (aktiv)" : "";
			_itemListProfile.AddItem($"{profil.Name}{marke}   –   {profil.Meta.SpieleGesamt} Spiele, {profil.Meta.GespielteJahre} Jahre");
		}
	}

	private string GetSelektierteId()
	{
		var auswahl = _itemListProfile.GetSelectedItems();
		return auswahl.Length == 0 ? null : _profilIds[auswahl[0]];
	}

	private async Task<bool> PruefeAuswahl(string id)
	{
		if (id != null)
			return true;

		SetProcessInput(false);
		await SW.UI.ShowText.ShowDialog("Bitte wählt zuerst ein Profil aus der Liste.");

		if (Visible)
			SetProcessInput(true);

		return false;
	}

	private void _on_item_list_profile_item_selected(long index)
	{
		var profil = _profilManager.FindeProfil(_profilIds[(int)index]);

		if (profil != null)
			_lineEditName.Text = profil.Name;
	}

	private async void _on_link_button_neu_pressed()
	{
		string name = _lineEditName.Text?.Trim();

		if (string.IsNullOrEmpty(name))
		{
			SetProcessInput(false);
			await SW.UI.ShowText.ShowDialog("Bitte gebt einen Namen für das neue Profil ein.");

			if (Visible)
				SetProcessInput(true);

			return;
		}

		_profilManager.ErstelleProfil(name);
		_lineEditName.Clear();
		Aktualisiere();
	}

	private async void _on_link_button_umbenennen_pressed()
	{
		string id = GetSelektierteId();

		if (!await PruefeAuswahl(id))
			return;

		string name = _lineEditName.Text?.Trim();

		if (string.IsNullOrEmpty(name))
		{
			SetProcessInput(false);
			await SW.UI.ShowText.ShowDialog("Bitte gebt im Eingabefeld den neuen Namen ein.");

			if (Visible)
				SetProcessInput(true);

			return;
		}

		_profilManager.BenenneUm(id, name);
		Aktualisiere();
	}

	private async void _on_link_button_aktiv_pressed()
	{
		string id = GetSelektierteId();

		if (!await PruefeAuswahl(id))
			return;

		_profilManager.SetzeAktivesProfil(id);
		Aktualisiere();
	}

	private async void _on_link_button_statistik_pressed()
	{
		string id = GetSelektierteId();

		if (!await PruefeAuswahl(id))
			return;

		SetProcessInput(false);
		await _main.StatistikDialog.ShowProfil(_profilManager.FindeProfil(id));

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_link_button_loeschen_pressed()
	{
		string id = GetSelektierteId();

		if (!await PruefeAuswahl(id))
			return;

		var profil = _profilManager.FindeProfil(id);

		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText(
			    $"Profil \"{profil.Name}\" mit seiner gesamten spielübergreifenden Statistik wirklich löschen?") == DialogResultGame.Yes)
		{
			_profilManager.LoescheProfil(id);
			_lineEditName.Clear();
			Aktualisiere();
		}

		if (Visible)
			SetProcessInput(true);
	}
}
