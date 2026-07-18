using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Schreibstube nach der Vorlage des WinForms-Clients: sechs Klickbereiche
/// (Bewerbung, Geldleiher, Gesetze, Kreditbuch, Privilegien, Kontrahenten) mit goldenen
/// Hover-Beschriftungen; Rechtsklick auf einen Bereich zeigt seine Beschreibung.
/// </summary>
public partial class Schreibstube : Control
{
	private static readonly string[] AreaNamen = { "AreaBewerbung", "AreaGeldleiher", "AreaGesetze", "AreaKreditbuch", "AreaPrivilegien", "AreaKontrahenten" };

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;
	private TextureRect _background;
	private Texture2D _hintergrundOhneFreieAemter;
	private Texture2D _hintergrundMitFreienAemtern;

	private Main _main;
	private SchreibstubeManager _schreibstubeManager;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelPlayerNameAndOffice = GetNode<Label>("LabelPlayerNameAndOffice");
		_labelPlaceDate = GetNode<Label>("LabelPlaceDate");
		_labelTaler = GetNode<Label>("LabelTaler");
		_background = GetNode<TextureRect>("TextureRect");

		_hintergrundOhneFreieAemter = GD.Load<Texture2D>("res://assets/backgrounds/BackgroundSchreibstube1.png");
		_hintergrundMitFreienAemtern = GD.Load<Texture2D>("res://assets/backgrounds/BackgroundSchreibstube2.png");

		// Die Klickbereiche sind unsichtbar; ihre goldene Beschriftung erscheint nur bei MouseOver
		foreach (string areaName in AreaNamen)
		{
			var area = GetNode<Button>(areaName);
			area.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			var label = GetNode<Label>("Label" + areaName.Substring("Area".Length));
			area.MouseEntered += () => label.Visible = true;
			area.MouseExited += () => label.Visible = false;
		}

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		// Rechtsklick auf einen Bereich zeigt wie im Original seine Beschreibung an
		if (@event is InputEventMouseButton mausklick)
		{
			foreach (string areaName in AreaNamen)
			{
				var area = GetNode<Button>(areaName);

				if (!area.Visible || !area.GetGlobalRect().HasPoint(mausklick.GlobalPosition))
					continue;

				SetProcessInput(false);
				await SW.UI.ShowText.ShowDialog(GetBereichsBeschreibung(areaName));

				if (Visible)
					SetProcessInput(true);

				return;
			}
		}

		SoundManager.Instance.PlayRightClick();
		CloseSchreibstube();
	}

	private static string GetBereichsBeschreibung(string areaName)
	{
		switch (areaName)
		{
			case "AreaBewerbung":
				return "Hier könnt Ihr Euch auf freie Ämter bewerben";
			case "AreaGeldleiher":
				return "Hier könnt Ihr Kredite nehmen";
			case "AreaGesetze":
				return "Hier könnt Ihr die Gesetze des Königreichs einsehen";
			case "AreaKreditbuch":
				return "Hier könnt Ihr Kredite tilgen";
			case "AreaPrivilegien":
				return "Hier könnt Ihr Eure Privilegien einsehen";
			default:
				return "Hier könnt Ihr Eure Kontrahenten einsehen";
		}
	}

	/// <summary>
	/// Öffnet die Schreibstube für den aktiven Spieler.
	/// </summary>
	public void ShowSchreibstube()
	{
		_schreibstubeManager = new SchreibstubeManager();

		// Mit freien Ämtern liegt wie im Original eine Bewerbungsmappe auf dem Tisch
		bool freieAemter = _schreibstubeManager.GibtEsFreieAemter();
		_background.Texture = freieAemter ? _hintergrundMitFreienAemtern : _hintergrundOhneFreieAemter;
		GetNode<Button>("AreaBewerbung").Visible = freieAemter;

		UpdateHud();

		Show();
		SetProcessInput(true);
	}

	private void CloseSchreibstube()
	{
		Hide();
		SetProcessInput(false);
		_main.Kontor.ReturnFromStadt();
	}

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Schreibstube A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert() + " Taler";
	}

	private async void _on_area_geldleiher_pressed()
	{
		SetProcessInput(false);

		if (_schreibstubeManager.KannKreditNehmen() == false)
		{
			await SW.UI.ShowText.ShowDialog("Niemand ist mehr bereit, Euch in diesem Jahr noch weitere Taler vorzustrecken");
		}
		else
		{
			var angebot = _schreibstubeManager.ErstelleKreditAngebot();

			if (await SW.UI.YesNoQuestion.ShowDialogText(angebot.GetAngebotsText(), "Annehmen", "Ablehnen") == DialogResultGame.Yes)
			{
				_schreibstubeManager.NimmKredit(angebot);
				SoundManager.Instance.PlayCoins();
				UpdateHud();
			}
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_kreditbuch_pressed()
	{
		SetProcessInput(false);

		await _main.KreditbuchDialog.ShowDialog(_schreibstubeManager);
		UpdateHud();

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_gesetze_pressed()
	{
		SetProcessInput(false);

		await _main.GesetzeDialog.ShowDialog(_schreibstubeManager);

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_bewerbung_pressed()
	{
		SetProcessInput(false);

		await _main.BewerbDialog.ShowDialog(new AemterManager());

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_nicht_implementiert_pressed()
	{
		SetProcessInput(false);

		// TODO: Privilegien und Kontrahenten migrieren
		await SW.UI.ShowText.ShowDialog("Wurde noch nicht implementiert");

		if (Visible)
			SetProcessInput(true);
	}
}
