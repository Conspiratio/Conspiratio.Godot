using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Niederlassung;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Stadtansicht nach der Vorlage des WinForms-Clients: Werkstätten- und Rohstoff-Symbole
/// mit Lagerbeständen und Preisen, zwei Produktionszeilen sowie Haus- und Transport-Symbol.
/// </summary>
public partial class Stadt : Control
{
	private const int AnzahlWerkstaetten = 6;
	private const int MaxRohstoffIcons = 20;
	private const float BestandLabelOriginalBreite = 86;  // Breite des WinForms-Labels, auf die sich die Ziffern-Klickzonen beziehen

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;

	private readonly TextureButton[] _buttonsWerkstatt = new TextureButton[AnzahlWerkstaetten + 1];
	private readonly TextureButton[] _buttonsRohstoff = new TextureButton[AnzahlWerkstaetten + 1];
	private readonly Label[] _labelsPreis = new Label[AnzahlWerkstaetten + 1];
	private readonly Label[] _labelsBestand = new Label[AnzahlWerkstaetten + 1];
	private TextureButton _buttonHaus;
	private TextureButton _buttonTransport;

	private readonly ButtonWithSounds[] _buttonsTaetigkeit = new ButtonWithSounds[2];
	private readonly HBoxContainer[] _detailRows = new HBoxContainer[2];
	private readonly ButtonWithSounds[] _buttonsProdukt = new ButtonWithSounds[2];
	private readonly NumericButtonWithSounds[] _numericsMenge = new NumericButtonWithSounds[2];
	private readonly NumericButtonWithSounds[] _numericsStaette = new NumericButtonWithSounds[2];
	private readonly Label[] _labelsText1 = new Label[2];
	private readonly Label[] _labelsText2 = new Label[2];
	private readonly Label[] _labelsKosten = new Label[2];

	private readonly Texture2D[] _rohstoffIcons = new Texture2D[MaxRohstoffIcons + 1];
	private readonly Texture2D[] _werkstattIcons = new Texture2D[MaxRohstoffIcons + 1];
	private Texture2D _symbolWerkstattKaufbar;
	private Texture2D _symbolNichtVerfuegbar;
	private Texture2D _symbolHaus;
	private Texture2D _symbolHausImBau;
	private Texture2D _symbolHausNichtVorhanden;
	private Resource _cursorPlus;
	private Resource _cursorMinus;
	private Resource _cursorDefault;

	private Main _main;
	private HandelsManager _handelsManager;
	private readonly AnwesenManager _anwesenManager = new AnwesenManager();
	private int _stadtId = 1;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelPlayerNameAndOffice = GetNode<Label>("LabelPlayerNameAndOffice");
		_labelPlaceDate = GetNode<Label>("LabelPlaceDate");
		_labelTaler = GetNode<Label>("LabelTaler");
		_buttonHaus = GetNode<TextureButton>("ButtonHaus");
		_buttonTransport = GetNode<TextureButton>("ButtonTransport");

		for (int i = 1; i <= AnzahlWerkstaetten; i++)
		{
			_buttonsWerkstatt[i] = GetNode<TextureButton>("ButtonWs" + i);
			_buttonsRohstoff[i] = GetNode<TextureButton>("ButtonRoh" + i);
			_labelsPreis[i] = GetNode<Label>("LabelPreis" + i);
			_labelsBestand[i] = GetNode<Label>("LabelBestand" + i);

			int nr = i;
			_buttonsWerkstatt[i].Pressed += () => OnWerkstattPressed(nr);
			_buttonsRohstoff[i].Pressed += () => OnRohstoffPressed(nr);
			_labelsBestand[i].GuiInput += @event => OnBestandGuiInput(nr, @event);
			_labelsBestand[i].MouseExited += () => Input.SetCustomMouseCursor(_cursorDefault);
		}

		for (int slot = 0; slot < 2; slot++)
		{
			_buttonsTaetigkeit[slot] = GetNode<ButtonWithSounds>("ButtonTaetigkeit" + slot);
			_detailRows[slot] = GetNode<HBoxContainer>("HBoxDetail" + slot);
			_buttonsProdukt[slot] = _detailRows[slot].GetNode<ButtonWithSounds>("ButtonProdukt");
			_numericsMenge[slot] = _detailRows[slot].GetNode<NumericButtonWithSounds>("NumericMenge");
			_numericsStaette[slot] = _detailRows[slot].GetNode<NumericButtonWithSounds>("NumericStaette");
			_labelsText1[slot] = _detailRows[slot].GetNode<Label>("LabelText1");
			_labelsText2[slot] = _detailRows[slot].GetNode<Label>("LabelText2");
			_labelsKosten[slot] = _detailRows[slot].GetNode<Label>("LabelKosten");

			int slotKopie = slot;
			_buttonsTaetigkeit[slot].Pressed += () => OnTaetigkeitPressed(slotKopie);
			_buttonsProdukt[slot].Pressed += () => OnProduktPressed(slotKopie);
			_numericsMenge[slot].WertChanged += wert => OnMengeChanged(slotKopie, wert);
			_numericsStaette[slot].WertChanged += wert => OnStaetteChanged(slotKopie, wert);
		}

		_buttonHaus.Pressed += OnHausPressed;
		_buttonTransport.Pressed += OnTransportPressed;

		LoadTextures();

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	private void LoadTextures()
	{
		for (int i = 1; i <= MaxRohstoffIcons; i++)
		{
			_rohstoffIcons[i] = GD.Load<Texture2D>("res://assets/images/rohstoffe/Roh" + i + ".png");
			_werkstattIcons[i] = GD.Load<Texture2D>("res://assets/images/werkstaetten/WSRoh" + i + ".png");
		}

		_symbolWerkstattKaufbar = GD.Load<Texture2D>("res://assets/images/symbole/SymbWS.png");
		_symbolNichtVerfuegbar = GD.Load<Texture2D>("res://assets/images/symbole/SymbNV.png");
		_symbolHaus = GD.Load<Texture2D>("res://assets/images/symbole/SymbAnwHaus1.png");
		_symbolHausImBau = GD.Load<Texture2D>("res://assets/images/symbole/SymbAnwImBau.png");
		_symbolHausNichtVorhanden = GD.Load<Texture2D>("res://assets/images/symbole/SymbAnwNV.png");

		_cursorPlus = ResourceLoader.Load("res://assets/cursor/CurPlus-32x32x24.png");
		_cursorMinus = ResourceLoader.Load("res://assets/cursor/CurMinus-32x32x24.png");
		_cursorDefault = ResourceLoader.Load("res://assets/cursor/CurSword-32x32x24.png");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		// Rechtsklick auf eine vorhandene Werkstätte bedeutet wie im Original: Werkstätte verkaufen
		if (@event is InputEventMouseButton mausklick)
		{
			for (int nr = 1; nr <= AnzahlWerkstaetten; nr++)
			{
				if (_buttonsWerkstatt[nr].GetGlobalRect().HasPoint(mausklick.GlobalPosition) &&
				    _handelsManager.RohstoffIdAnPlatz(_stadtId, nr) != 0 && _handelsManager.HatWerkstatt(_stadtId, nr))
				{
					await WerkstattVerkaufen(nr);
					return;
				}
			}
		}

		SoundManager.Instance.PlayRightClick();
		CloseStadt();
	}

	/// <summary>
	/// Öffnet die Stadtansicht für die angegebene Stadt (angesteuert über die politische Weltkarte).
	/// </summary>
	public void ShowStadt(int stadtId)
	{
		_handelsManager = new HandelsManager();
		_stadtId = stadtId;

		Refresh();

		Show();
		SetProcessInput(true);
	}

	private void CloseStadt()
	{
		// Wie im Original führt der Weg aus der Stadt zurück auf die Handelskarte
		Hide();
		SetProcessInput(false);
		_main.Weltkarte.ZeigeHandelskarte();
	}

	#region Refresh

	private void Refresh()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = SW.Dynamisch.GetStadtwithID(_stadtId).GetGebietsName() + " A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert();

		for (int nr = 1; nr <= AnzahlWerkstaetten; nr++)
			RefreshWerkstatt(nr);

		HausAnzeigen();
		RefreshSlot(0);
		RefreshSlot(1);

		_buttonTransport.TextureNormal = GD.Load<Texture2D>("res://assets/images/symbole/SymbKaravane.png");
		_buttonTransport.TooltipText = "Transport: Karawane beauftragen";
	}

	private void RefreshWerkstatt(int nr)
	{
		int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, nr);

		if (rohstoffId == 0 || rohstoffId > MaxRohstoffIcons)
		{
			_buttonsWerkstatt[nr].Visible = false;
			_buttonsRohstoff[nr].Visible = false;
			_labelsPreis[nr].Visible = false;
			_labelsBestand[nr].Visible = false;
			return;
		}

		bool hatRecht = _handelsManager.HatRohstoffrecht(_stadtId, nr);
		bool hatWerkstatt = hatRecht && _handelsManager.HatWerkstatt(_stadtId, nr);
		string rohstoffName = SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName();

		_buttonsWerkstatt[nr].Visible = true;
		_buttonsWerkstatt[nr].Disabled = !hatRecht;

		if (!hatRecht)
		{
			_buttonsWerkstatt[nr].TextureNormal = _symbolNichtVerfuegbar;
			_buttonsWerkstatt[nr].TooltipText = rohstoffName + ": Euch fehlt das Rohstoffrecht";
		}
		else if (hatWerkstatt)
		{
			_buttonsWerkstatt[nr].TextureNormal = _werkstattIcons[rohstoffId];
			_buttonsWerkstatt[nr].TooltipText = _handelsManager.GetLagerplatzInfo(_stadtId, nr) + "\nRechtsklick: Werkstätte verkaufen";
		}
		else
		{
			_buttonsWerkstatt[nr].TextureNormal = _symbolWerkstattKaufbar;
			_buttonsWerkstatt[nr].TooltipText = "Werkstätte für " + rohstoffName + " kaufen (" + _handelsManager.GetWerkstattKaufpreis(_stadtId, nr).ToStringGeld() + ")";
		}

		_buttonsRohstoff[nr].Visible = hatWerkstatt;
		_buttonsRohstoff[nr].TextureNormal = _rohstoffIcons[rohstoffId];
		_buttonsRohstoff[nr].TooltipText = rohstoffName + " (Klick: kompletten Bestand verkaufen)";

		_labelsPreis[nr].Visible = hatWerkstatt;
		_labelsPreis[nr].Text = SW.Dynamisch.GetStadtwithID(_stadtId).GetRohstoffPreisVonIDX(rohstoffId).ToString();

		_labelsBestand[nr].Visible = hatWerkstatt;
		_labelsBestand[nr].Text = FormatiereBestand(_handelsManager.GetLagerbestand(_stadtId, rohstoffId));
		_labelsBestand[nr].TooltipText = "Obere Hälfte: einkaufen, untere Hälfte: verkaufen\n(die Ziffernposition bestimmt die Menge)";
	}

	private static string FormatiereBestand(int anzahl)
	{
		string text = anzahl.ToString();

		while (text.Length < 6)
			text = "0" + text;

		return text.Substring(0, 3) + "." + text.Substring(3, 3);
	}

	private void HausAnzeigen()
	{
		if (_anwesenManager.HatHaus(_stadtId))
		{
			_buttonHaus.TextureNormal = _anwesenManager.IstFertig(_stadtId) ? _symbolHaus : _symbolHausImBau;
			_buttonHaus.TooltipText = _anwesenManager.GetNameInklPronomen(_stadtId);
		}
		else
		{
			_buttonHaus.TextureNormal = _symbolHausNichtVorhanden;
			_buttonHaus.TooltipText = "Ihr besitzt keinen Wohnsitz in dieser Stadt";
		}
	}

	private void RefreshSlot(int slot)
	{
		var produktionsslot = _handelsManager.GetProduktionsslot(_stadtId, slot);
		var aktionsart = (EnumProduktionsslotAktionsart)produktionsslot.GetTaetigkeit();

		_buttonsTaetigkeit[slot].Text = AktionsartAlsText(aktionsart);

		if (aktionsart == EnumProduktionsslotAktionsart.KeinAuftrag)
		{
			_detailRows[slot].Visible = false;
			return;
		}

		if (aktionsart == EnumProduktionsslotAktionsart.Produzieren)
		{
			int rohstoffId = _handelsManager.KorrigiereProduktionsRohstoff(_stadtId, slot);

			if (rohstoffId == 0)
			{
				// Keine Werkstätte in dieser Stadt: Es gibt nichts einzustellen
				_detailRows[slot].Visible = false;
				return;
			}

			_detailRows[slot].Visible = true;

			// Produktionstext zerlegen, z. B. "Braut Bier;an:Kessel.Kesseln"
			string produktionsText = SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetProdText();
			string verb = produktionsText.Substring(0, produktionsText.IndexOf(';'));
			string mittelteil = produktionsText.Substring(produktionsText.IndexOf(';') + 1, produktionsText.IndexOf(':') - produktionsText.IndexOf(';') - 1);
			string staetteEinzahl = produktionsText.Substring(produktionsText.IndexOf(':') + 1, produktionsText.IndexOf('.') - (produktionsText.IndexOf(':') + 1));
			string staetteMehrzahl = produktionsText.Substring(produktionsText.IndexOf('.') + 1);

			_buttonsProdukt[slot].Text = verb + " mit";

			_numericsMenge[slot].TausenderTrenner = false;
			_numericsMenge[slot].NurEinserSchritte = false;
			_numericsMenge[slot].MaximalerWert = SW.Statisch.GetMaxArbeiterAnzahl();
			_numericsMenge[slot].MaximaleStellen = SW.Statisch.GetMaxArbeiterAnzahl().ToString().Length;
			_numericsMenge[slot].Wert = produktionsslot.GetProduktionArbeiter();

			_labelsText1[slot].Text = (produktionsslot.GetProduktionArbeiter() != 1 ? "Arbeitern " : "Arbeiter ") + mittelteil;

			_numericsStaette[slot].WertAnzeigen = true;
			_numericsStaette[slot].NurEinserSchritte = false;
			_numericsStaette[slot].TausenderTrenner = false;
			_numericsStaette[slot].MaximalerWert = 99;
			_numericsStaette[slot].MaximaleStellen = 2;
			_numericsStaette[slot].Wert = produktionsslot.GetProduktionStaetten();

			_labelsText2[slot].Text = produktionsslot.GetProduktionStaetten() != 1 ? staetteMehrzahl : staetteEinzahl;
			_labelsText2[slot].Visible = true;

			// Reihenfolge wie im Original: Produkt, Arbeiter, Text, Stätten, Text, Kosten
			_detailRows[slot].MoveChild(_buttonsProdukt[slot], 0);
			_detailRows[slot].MoveChild(_numericsMenge[slot], 1);
			_detailRows[slot].MoveChild(_labelsText1[slot], 2);
			_detailRows[slot].MoveChild(_numericsStaette[slot], 3);
			_detailRows[slot].MoveChild(_labelsText2[slot], 4);
			_detailRows[slot].MoveChild(_labelsKosten[slot], 5);
		}
		else  // Verkaufen oder Permanenter Verkauf
		{
			_handelsManager.KorrigiereVerkaufsEinstellungen(_stadtId, slot);
			_detailRows[slot].Visible = true;

			_labelsText1[slot].Text = "Verkauft";

			_numericsMenge[slot].TausenderTrenner = true;
			_numericsMenge[slot].NurEinserSchritte = false;
			_numericsMenge[slot].MaximalerWert = SW.Statisch.GetMaxAnzahlVonEinemRohstoff();
			_numericsMenge[slot].MaximaleStellen = SW.Statisch.GetMaxAnzahlVonEinemRohstoff().ToString().Length;
			_numericsMenge[slot].Wert = produktionsslot.GetVerkaufAnzahl();

			_buttonsProdukt[slot].Text = SW.Dynamisch.GetRohstoffwithID(produktionsslot.GetVerkaufRohstoff()).GetRohName();

			_numericsStaette[slot].WertAnzeigen = false;
			_numericsStaette[slot].NurEinserSchritte = true;
			_numericsStaette[slot].MaximalerWert = SW.Statisch.GetMaxStadtID();
			_numericsStaette[slot].MaximaleStellen = SW.Statisch.GetMaxStadtID().ToString().Length;
			_numericsStaette[slot].Wert = produktionsslot.GetVerkaufStadt();
			_numericsStaette[slot].Text = "in " + SW.Dynamisch.GetStadtwithID(produktionsslot.GetVerkaufStadt()).GetGebietsName();

			_labelsText2[slot].Visible = false;

			// Reihenfolge wie im Original: "Verkauft", Anzahl, Rohstoff, Zielstadt, Kosten
			_detailRows[slot].MoveChild(_labelsText1[slot], 0);
			_detailRows[slot].MoveChild(_numericsMenge[slot], 1);
			_detailRows[slot].MoveChild(_buttonsProdukt[slot], 2);
			_detailRows[slot].MoveChild(_numericsStaette[slot], 3);
			_detailRows[slot].MoveChild(_labelsKosten[slot], 4);
		}

		_labelsKosten[slot].Text = "für " + _handelsManager.BerechneKosten(_stadtId, slot).ToStringGeld();
	}

	private static string AktionsartAlsText(EnumProduktionsslotAktionsart aktionsart)
	{
		switch (aktionsart)
		{
			case EnumProduktionsslotAktionsart.Produzieren:
				return "Produzieren";
			case EnumProduktionsslotAktionsart.Verkaufen:
				return "Verkaufen";
			case EnumProduktionsslotAktionsart.PermanentVerkaufen:
				return "Permanenter Verkauf";
			default:
				return "Kein Auftrag";
		}
	}

	#endregion

	#region Werkstätten und Rohstoffe

	private async void OnWerkstattPressed(int nr)
	{
		SetProcessInput(false);

		if (_handelsManager.HatWerkstatt(_stadtId, nr))
		{
			// Eigene Werkstätte: den Lagerraum-Kauf öffnen (Rechtsklick auf die Werkstätte verkauft sie).
			await _main.LagerraumKaufenDialog.ShowDialog(_stadtId, nr);
		}
		else
		{
			int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, nr);
			int kaufpreis = _handelsManager.GetWerkstattKaufpreis(_stadtId, nr);

			if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr für " + kaufpreis.ToStringGeld() + " in " +
			                                             SW.Dynamisch.GetStadtwithID(_stadtId).GetGebietsName() + " eine Werkstätte für eine\n" +
			                                             SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "-Produktion kaufen?") == DialogResultGame.Yes)
			{
				if (_handelsManager.KaufeWerkstatt(_stadtId, nr, out string fehler))
					SoundManager.Instance.PlayCoins();
				else
					await SW.UI.ShowText.ShowDialog(fehler);
			}
		}

		Refresh();

		if (Visible)
			SetProcessInput(true);
	}

	private async Task WerkstattVerkaufen(int nr)
	{
		SetProcessInput(false);

		int verkaufspreis = _handelsManager.GetWerkstattVerkaufspreis(_stadtId, nr);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Eure Werkstätte für " + verkaufspreis + "\nTaler verkaufen?", "Ja", "Nein") == DialogResultGame.Yes)
		{
			_handelsManager.VerkaufeWerkstatt(_stadtId, nr);
			SoundManager.Instance.PlayCoins();
		}

		Refresh();

		if (Visible)
			SetProcessInput(true);
	}

	private void OnRohstoffPressed(int nr)
	{
		// Wie im Original: Ein Klick auf das Rohstoffsymbol verkauft den kompletten Bestand
		int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, nr);
		int erloes = _handelsManager.VerkaufeRohstoff(_stadtId, rohstoffId, _handelsManager.GetLagerbestand(_stadtId, rohstoffId));

		if (erloes > 0)
			SoundManager.Instance.PlayCoins();

		Refresh();
	}

	private async void OnBestandGuiInput(int nr, InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
			Input.SetCustomMouseCursor(motion.Position.Y >= _labelsBestand[nr].Size.Y / 2 ? _cursorMinus : _cursorPlus);

		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick)
			return;

		// Die X-Position des Klicks bestimmt die Menge (wie im Original über die Ziffernposition)
		float x = klick.Position.X * BestandLabelOriginalBreite / _labelsBestand[nr].Size.X;
		int menge = 1;

		if (x >= 4 && x < 90)
		{
			if (x < 18)
				menge = 100000;
			else if (x < 31)
				menge = 10000;
			else if (x < 46)
				menge = 1000;
			else if (x < 63)
				menge = 100;
			else if (x < 77)
				menge = 10;
		}

		int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, nr);
		bool verkaufen = klick.Position.Y >= _labelsBestand[nr].Size.Y / 2;

		SoundManager.Instance.PlayLeftClick();

		if (verkaufen)
		{
			int erloes = _handelsManager.VerkaufeRohstoff(_stadtId, rohstoffId, menge);

			if (erloes > 0)
				SoundManager.Instance.PlayCoins();
		}
		else
		{
			SetProcessInput(false);

			_handelsManager.KaufeRohstoff(_stadtId, rohstoffId, menge, out string fehler);

			if (fehler != "")
				await SW.UI.ShowText.ShowDialog(fehler);

			if (Visible)
				SetProcessInput(true);
		}

		Refresh();
	}

	#endregion

	#region Haus und Transport

	private async void OnHausPressed()
	{
		SetProcessInput(false);

		// Anwesen-Verwaltung (Haus bauen/umbauen, renovieren, erweitern, verkaufen)
		await _main.HausVerwaltungDialog.ShowDialog(_stadtId);
		Refresh();

		if (Visible)
			SetProcessInput(true);
	}

	private async void OnTransportPressed()
	{
		SetProcessInput(false);

		while (true)
		{
			var karawane = _handelsManager.GetKarawane(_stadtId);

			var antwort = await SW.UI.YesNoQuestion.ShowDialogText(
				"Welche Karawane wollt Ihr mit Eurem Transport beauftragen?\n\n" +
				"Karawanenführer: " + karawane.Beschreibung + "\n" +
				"Fixpreis: " + karawane.Fixpreis + " Taler, je 100 Stück: " + karawane.PreisProStueck + " Taler\n" +
				"Verlässlichkeit: " + karawane.Verlaesslichkeit + " %, Sicherheit: " + karawane.Sicherheit + " %",
				"Nächste Karawane", "Diese beauftragen");

			if (antwort != DialogResultGame.Yes)
				break;

			_handelsManager.NaechsteKarawane(_stadtId);
		}

		Refresh();

		if (Visible)
			SetProcessInput(true);
	}

	#endregion

	#region Produktionsslots

	private void OnTaetigkeitPressed(int slot)
	{
		var aktuelleArt = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();
		var neueArt = (EnumProduktionsslotAktionsart)(((int)aktuelleArt + 1) % 4);

		_handelsManager.SetzeTaetigkeit(_stadtId, slot, neueArt);
		Refresh();
	}

	private void OnProduktPressed(int slot)
	{
		var aktionsart = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();

		if (aktionsart == EnumProduktionsslotAktionsart.Produzieren)
			_handelsManager.NaechsterProduktionsRohstoff(_stadtId, slot);
		else
			_handelsManager.NaechsterVerkaufsRohstoff(_stadtId, slot);

		Refresh();
	}

	private void OnMengeChanged(int slot, int wert)
	{
		var aktionsart = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();

		if (aktionsart == EnumProduktionsslotAktionsart.Produzieren)
			_handelsManager.SetzeProduktionsArbeiter(_stadtId, slot, wert);
		else
			_handelsManager.SetzeVerkaufsAnzahl(_stadtId, slot, wert);

		Refresh();
	}

	private void OnStaetteChanged(int slot, int wert)
	{
		var aktionsart = (EnumProduktionsslotAktionsart)_handelsManager.GetProduktionsslot(_stadtId, slot).GetTaetigkeit();

		if (aktionsart == EnumProduktionsslotAktionsart.Produzieren)
		{
			_handelsManager.SetzeProduktionsStaetten(_stadtId, slot, wert);
		}
		else
		{
			// Zielstadt weiterschalten (die eigene Stadt wird wie im Original übersprungen)
			_handelsManager.SetzeVerkaufsStadtAusWert(_stadtId, slot, wert);
		}

		Refresh();
	}

	#endregion
}
