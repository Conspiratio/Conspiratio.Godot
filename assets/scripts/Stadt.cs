using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Niederlassung;
using Conspiratio.Lib.Gameplay.Personen;
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

	// Rohstoffbereich (Preis/Bestand) liegt auf der Steinwand: Gold mit dunklem Rand für Lesbarkeit.
	private static readonly Color GoldFarbe = new(1f, 0.85f, 0.15f);
	private static readonly Color OutlineDunkel = new(0.12f, 0.08f, 0.03f);
	// Produktionszeilen liegen auf dem Pergament: dunkles Braun-Schwarz (wie in den Dialogen).
	private static readonly Color PergamentText = new(0.16f, 0.11f, 0.05f);

	/// <summary>
	/// Gold, dem der Glanz fehlt: die Preiszeile eines gesättigten Marktes. Der Ton wird stumpfer,
	/// je größer der Marktabschlag ist — bei <c>MaxAbschlagProzent</c> ganz erreicht.
	///
	/// Bewusst <b>kein</b> Rot: Das ist in dieser Ansicht schon vergeben (ungültige Nullwerte in den
	/// Produktionszeilen, siehe <see cref="FaerbeNumeric"/>), und zwei Bedeutungen auf einer Farbe
	/// lassen sich nicht auseinanderhalten. Auch nicht dunkler als hier: Die Zeile steht auf der
	/// Steinwand und lebt vom Kontrast zu <see cref="OutlineDunkel"/>.
	/// </summary>
	private static readonly Color GoldStumpf = new(0.80f, 0.74f, 0.62f);

	/// <summary>
	/// Dasselbe Zeichen auf Pergament, aber als <b>Kontur</b> statt als Schriftfarbe: die Zielstadt der
	/// Verkaufszeile, wenn dort schon Ware liegt. Die Deckkraft wächst mit dem Abschlag, bei 0 ist die
	/// Kontur unsichtbar.
	///
	/// Warum nicht die Schriftfarbe wie bei der Preiszeile: Auf dem hellen Pergament kostet jeder
	/// warme Farbton Kontrast. Dunkelbraun auf Pergament trägt rund 5:1, derselbe Ocker als Füllung
	/// nur noch rund 1,7:1 — die Zielstadt wäre ausgerechnet dann am schlechtesten zu lesen, wenn ihr
	/// Markt am vollsten ist. Als Kontur um die unverändert dunkle Schrift kostet dasselbe Signal
	/// keinen Kontrast. Wieder kein Rot: Das gehört den ungültigen Werten.
	/// </summary>
	private static readonly Color PergamentGesaettigt = new(0.72f, 0.45f, 0.05f);

	/// <summary>Stärke der Sättigungskontur auf dem Zielstadt-Knopf.</summary>
	private const int SaettigungsKonturStaerke = 5;

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;

	private readonly TextureButton[] _buttonsWerkstatt = new TextureButton[AnzahlWerkstaetten + 1];
	private readonly TextureButton[] _buttonsRohstoff = new TextureButton[AnzahlWerkstaetten + 1];
	private readonly Label[] _labelsPreis = new Label[AnzahlWerkstaetten + 1];
	private readonly Label[] _labelsBestand = new Label[AnzahlWerkstaetten + 1];
	private readonly Label[] _labelsFertigkeit = new Label[AnzahlWerkstaetten + 1];
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
	private readonly ProduktionsfertigkeitManager _fertigkeit = new ProduktionsfertigkeitManager();
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
			_labelsFertigkeit[i] = GetNode<Label>("LabelFertigkeit" + i);

			// Rohstoffbereich (auf Steinwand): Gold mit dunklem Rand statt des schwarzen Theme-Defaults.
			// Die Fertigkeitszeile bleibt bewusst beim vollen Gold: Das Ausbleichen weiter oben trägt
			// seit Stufe B den Marktabschlag, und eine zweite Bedeutung auf demselben Kanal ließe sich
			// nicht mehr auseinanderhalten. Die Fertigkeit bekommt Worte, nicht Farbe.
			foreach (var label in new[] { _labelsPreis[i], _labelsBestand[i], _labelsFertigkeit[i] })
			{
				label.AddThemeColorOverride("font_color", GoldFarbe);
				label.AddThemeColorOverride("font_outline_color", OutlineDunkel);
				label.AddThemeConstantOverride("outline_size", 4);
			}

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

			// Produktionszeile liegt auf dem Pergament: Produkt- und Zahlen-Buttons (Theme-Default gold)
			// auf das dunkle Pergament-Schwarz setzen; die Wort-Labels sind bereits schwarz.
			_buttonsProdukt[slot].AddThemeColorOverride("font_color", PergamentText);
			_numericsMenge[slot].AddThemeColorOverride("font_color", PergamentText);
			_numericsStaette[slot].AddThemeColorOverride("font_color", PergamentText);

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
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		// Rechtsklick auf eine vorhandene Werkstätte bedeutet wie im Original: Werkstätte verkaufen.
		// Sonst bietet die Warenzeile das Buch des hiesigen Händlers an, wo es eines gibt – es hängt
		// bewusst an der Ware und nicht an einem eigenen Knopf, denn es gibt es nur dort, wo die Ware
		// zur Hauptproduktion der Stadt gehört. Der Ortsbezug ist der ganze Sinn der Sache.
		if (@event is InputEventMouseButton mausklick)
		{
			for (int nr = 1; nr <= AnzahlWerkstaetten; nr++)
			{
				int rohstoffId = _handelsManager.RohstoffIdAnPlatz(_stadtId, nr);

				if (rohstoffId == 0)
					continue;

				bool aufWerkstatt = _buttonsWerkstatt[nr].Visible &&
				                    _buttonsWerkstatt[nr].GetGlobalRect().HasPoint(mausklick.GlobalPosition);
				bool aufRohstoff = _buttonsRohstoff[nr].Visible &&
				                   _buttonsRohstoff[nr].GetGlobalRect().HasPoint(mausklick.GlobalPosition);

				if (!aufWerkstatt && !aufRohstoff)
					continue;

				if (aufWerkstatt && _handelsManager.HatWerkstatt(_stadtId, nr))
				{
					await WerkstattVerkaufen(nr);
					return;
				}

				if (_fertigkeit.GibtEsBuch(_stadtId, rohstoffId))
				{
					await BuchKaufen(rohstoffId);
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
			_labelsFertigkeit[nr].Visible = false;
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
			_buttonsWerkstatt[nr].TooltipText = "Werkstätte für " + rohstoffName + " kaufen (" +
			                                    _handelsManager.GetWerkstattKaufpreis(_stadtId, nr).ToStringGeld() + ")" +
			                                    BuchHinweis(rohstoffId);
		}

		_buttonsRohstoff[nr].Visible = hatWerkstatt;
		_buttonsRohstoff[nr].TextureNormal = _rohstoffIcons[rohstoffId];
		_buttonsRohstoff[nr].TooltipText = rohstoffName + " (Klick: kompletten Bestand verkaufen)" +
		                                   BuchHinweis(rohstoffId);

		_labelsPreis[nr].Visible = hatWerkstatt;
		_labelsPreis[nr].Text = SW.Dynamisch.GetStadtwithID(_stadtId).GetRohstoffPreisVonIDX(rohstoffId).ToString();
		_labelsPreis[nr].TooltipText = Marktlage.BeschreibeMarktpreis(_stadtId, rohstoffId);

		// Stufe B: Dem Preis sieht man an, dass er gedrückt ist - ohne dass man dafür hovern muss.
		_labelsPreis[nr].AddThemeColorOverride("font_color",
			GoldFarbe.Lerp(GoldStumpf, Marktlage.ErmittleSaettigungsAnteil(SW.Dynamisch.GetStadtwithID(_stadtId), rohstoffId)));

		_labelsBestand[nr].Visible = hatWerkstatt;
		_labelsBestand[nr].Text = FormatiereBestand(_handelsManager.GetLagerbestand(_stadtId, rohstoffId));
		_labelsBestand[nr].TooltipText = "Obere Hälfte: einkaufen, untere Hälfte: verkaufen\n(die Ziffernposition bestimmt die Menge)";

		int koennen = SW.Dynamisch.GetAktHum().GetProduktionsfertigkeit(rohstoffId);

		_labelsFertigkeit[nr].Visible = hatWerkstatt;
		_labelsFertigkeit[nr].Text = ProduktionsfertigkeitManager.FertigkeitAlsText(koennen);
		_labelsFertigkeit[nr].TooltipText = rohstoffName + ": " +
		                                    ProduktionsfertigkeitManager.FertigkeitAlsText(koennen) +
		                                    " (" + koennen + " von " + HumSpieler.MaxProduktionsfertigkeit + ")\n" +
		                                    "Mit steigendem Können werden schlechte Jahre seltener –\n" +
		                                    "gute werden nicht besser.";
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

	/// <summary>
	/// Der Tooltip auf der Ware der Auftragszeile: Er nennt das Arbeiter-pro-Stätte-Verhältnis, das die
	/// Ware verlangt, und wie die aktuelle Einstellung dazu steht. Ohne diese Angabe stand nirgends im
	/// Spiel, wie viele Arbeiter eine Werkstätte eigentlich braucht – zu wenige drücken den Ertrag
	/// anteilig (<c>Produktionsslot.GetProduktion</c>), zu viele kosten nur Lohn.
	/// </summary>
	private static string ErmittleVerhaeltnisTooltip(int rohstoffId, Produktionsslot produktionsslot,
	                                                 string staetteEinzahl, string staetteMehrzahl)
	{
		var rohstoff = SW.Dynamisch.GetRohstoffwithID(rohstoffId);
		int arbeiterJeStaette = rohstoff.GetArbeiter() / rohstoff.GetWerkstaetten();

		int staetten = produktionsslot.GetProduktionStaetten();
		int arbeiter = produktionsslot.GetProduktionArbeiter();
		int benoetigt = staetten * arbeiterJeStaette;

		string text = rohstoff.GetRohName() + "\n" +
		              rohstoff.GetArbeiter() + " Arbeiter je " + rohstoff.GetWerkstaetten() + " " +
		              (rohstoff.GetWerkstaetten() == 1 ? staetteEinzahl : staetteMehrzahl);

		if (staetten <= 0)
			return text;

		text += "\nFür " + staetten + " " + (staetten == 1 ? staetteEinzahl : staetteMehrzahl) +
		        " benötigt Ihr " + benoetigt + " Arbeiter";

		if (arbeiter < benoetigt)
			text += "\nEs fehlen " + (benoetigt - arbeiter) + " – der Ertrag sinkt entsprechend";
		else if (arbeiter > benoetigt)
			text += "\n" + (arbeiter - benoetigt) + " zu viel – sie kosten Lohn ohne Mehrertrag";

		return text;
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
			_buttonsProdukt[slot].TooltipText = ErmittleVerhaeltnisTooltip(rohstoffId, produktionsslot, staetteEinzahl, staetteMehrzahl);

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

			// Ungültige (nicht produzierende) Werte rot einfärben – wie im Original das rote "0 Bottichen".
			FaerbeNumeric(_numericsMenge[slot], produktionsslot.GetProduktionArbeiter() > 0);
			FaerbeNumeric(_numericsStaette[slot], produktionsslot.GetProduktionStaetten() > 0);

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

			// Verkaufsmenge 0 = kein Verkauf: rot; sonst schwarz.
			FaerbeNumeric(_numericsMenge[slot], produktionsslot.GetVerkaufAnzahl() > 0);

			// Die Zielstadt bekommt dagegen die Sättigungskontur: Nach der Messung in
			// <see cref="BeschreibeZielstadt"/> ist in der Zeile kein Platz für eine Zahl, Farbe aber
			// kostet keine Breite - und ohne sie bliebe ein voller Zielmarkt bis zum Hovern verborgen.
			float saettigung = Marktlage.ErmittleSaettigungsAnteil(
				SW.Dynamisch.GetStadtwithID(produktionsslot.GetVerkaufStadt()),
				produktionsslot.GetVerkaufRohstoff());

			_numericsStaette[slot].AddThemeColorOverride("font_color", PergamentText);
			_numericsStaette[slot].AddThemeColorOverride("font_outline_color",
				new Color(PergamentGesaettigt, saettigung));
			_numericsStaette[slot].AddThemeConstantOverride("outline_size", SaettigungsKonturStaerke);

			// Was die Ware in der gewählten Zielstadt einbringt, hängt am Zahlen-Knopf der Zielstadt -
			// also an dem Bedienelement, mit dem der Spieler die Städte durchschaltet.
			_numericsStaette[slot].TooltipText = BeschreibeZielstadt(produktionsslot.GetVerkaufStadt(),
			                                                         produktionsslot.GetVerkaufRohstoff());

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

	/// <summary>
	/// Was ein Stück in der Zielstadt derzeit einbringt und warum - Stufe C aus
	/// docs/saettigungsrabatt-sichtbar-konzept.md.
	///
	/// Der Export ist die Stelle, an der die Entscheidung fällt, und bis hierher war die Sättigung der
	/// Zielstadt unsichtbar: Der Spieler lieferte blind in einen vollen Markt und sättigte ihn dabei
	/// weiter, weil <c>BuchManager</c> die Menge in den Vorrat der <b>Ziel</b>stadt bucht.
	///
	/// <b>Warum ein Tooltip und keine sichtbare Spalte in der Zeile:</b> Dafür ist kein Platz. Die
	/// Verkaufszeile endet im ungünstigsten Fall (längster Stadtname "Frozen Castle", fünfstellige
	/// Fuhrkosten) bei x = 1017, das Pergament des Hintergrundbildes bei x = 1101 - gemessen bleiben
	/// also 84 px, rund sechs Zeichen. Schon "zu je 20 Taler" braucht 180 px, mit Abschlagsangabe
	/// 407 px; die Zeile liefe damit weit auf die Steinwand hinaus. Der <c>HBoxContainer</c> reicht
	/// zwar bis 1360, das Pergament ist aber Teil von BackgroundStadt.png und nicht dehnbar.
	///
	/// Der Preis stimmt Stück für Stück: <c>BuchManager</c> liest ihn einmal je Lieferung und rechnet
	/// die gesamte Menge damit ab - eine Lieferung drückt ihren eigenen Preis also nicht. Sie drückt
	/// den des Folgejahres, weil die Menge erst am Rundenende in den Vorrat wandert. Es bleibt aber
	/// eine Schätzung: Abgerechnet wird beim Buch zu Beginn des nächsten Zuges, nach
	/// <c>RohPreiseRandomSchwanken</c>. Der letzte Satz des Tooltips sagt das ausdrücklich.
	/// </summary>
	private static string BeschreibeZielstadt(int zielStadtId, int rohstoffId)
	{
		return Marktlage.BeschreibeMarktpreis(zielStadtId, rohstoffId) + "\n" +
		       "Abgerechnet wird beim Karawanenzug zu Beginn des nächsten Jahres — bis dahin kann sich der Preis bewegen.";
	}

	// Färbt einen Zahlen-Button je nach Gültigkeit: schwarz auf Pergament bzw. rot bei ungültigem (0-)Wert.
	private void FaerbeNumeric(NumericButtonWithSounds numeric, bool gueltig)
	{
		numeric.AddThemeColorOverride("font_color", gueltig ? PergamentText : new Color(0.65f, 0.05f, 0.05f));

		// Die Saettigungskontur des Verkaufsmodus abraeumen: Theme-Overrides ueberdauern den Wechsel
		// der Taetigkeit, sonst behielte ein auf "Produzieren" umgestellter Slot den Ockerrand.
		numeric.AddThemeConstantOverride("outline_size", 0);
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

	/// <summary>
	/// Die Tooltip-Zeile zum Buch, oder eine leere Zeichenkette, wo es keines gibt. Der Hinweis ist
	/// nötig, weil ein Rechtsklick sonst nirgends ankündigt, was er tut – anders als beim Verkauf der
	/// Werkstätte, dessen Zeile schon dort stand.
	/// </summary>
	private string BuchHinweis(int rohstoffId)
	{
		if (!_fertigkeit.GibtEsBuch(_stadtId, rohstoffId))
			return "";

		return "\nRechtsklick: Schrift über die Herstellung kaufen (" +
		       _fertigkeit.GetBuchpreis(rohstoffId).ToStringGeld() + ")";
	}

	/// <summary>
	/// Die Schrift des hiesigen Händlers: einmalig je Ware, und nur dort zu haben, wo die Ware zur
	/// Hauptproduktion der Stadt gehört. Sie ist der billigste Weg zur Produktionsfertigkeit – dafür
	/// muss man hinreisen.
	/// </summary>
	private async Task BuchKaufen(int rohstoffId)
	{
		SetProcessInput(false);

		string rohstoffName = SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName();
		int koennen = SW.Dynamisch.GetAktHum().GetProduktionsfertigkeit(rohstoffId);

		if (await SW.UI.YesNoQuestion.ShowDialogText(
			    "Ein Händler bietet Euch eine Schrift über die\nHerstellung von " + rohstoffName + " an.\n\n" +
			    "Sie kostet " + _fertigkeit.GetBuchpreis(rohstoffId).ToStringGeld() + ".\n" +
			    "Euer Können darin ist bislang " + ProduktionsfertigkeitManager.FertigkeitAlsText(koennen) + ".",
			    "Kaufen und lesen", "Nicht nötig") == DialogResultGame.Yes)
		{
			if (_fertigkeit.KaufeBuch(_stadtId, rohstoffId, out string meldung))
				SoundManager.Instance.PlayCoins();

			await SW.UI.ShowText.ShowDialog(meldung);
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
