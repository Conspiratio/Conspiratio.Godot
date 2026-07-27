using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Kampf;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Conspiratio.Lib.Gameplay.Titel;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Kontor : Control
{
	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;
	private Color _talerStandardFarbe;

	private RundenManager _rundenManager;
	private SpeicherManager _speicherManager;
	private Main _main;
	private bool _geradeGeladen;

	[Export]
	public NodePath LabelPlayerNameAndOfficePath { get; set; }

	[Export]
	public NodePath LabelPlaceDatePath { get; set; }

	[Export]
	public NodePath LabelTalerPath { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_labelPlayerNameAndOffice = GetNode<Label>(LabelPlayerNameAndOfficePath);
		_labelPlaceDate = GetNode<Label>(LabelPlaceDatePath);
		_labelTaler = GetNode<Label>(LabelTalerPath);

		// Klick auf die Taler öffnet – wie im Original – die politische Weltkarte im Beziehungs-/
		// Bestechungsmodus (Personen-Modus 0). MouseOver hebt die Zahl golden hervor.
		_labelTaler.MouseFilter = Control.MouseFilterEnum.Stop;
		_talerStandardFarbe = _labelTaler.GetThemeColor("font_color");
		_labelTaler.GuiInput += OnTalerGuiInput;
		_labelTaler.MouseEntered += () => _labelTaler.AddThemeColorOverride("font_color", new Color(0.8f, 0.05f, 0.05f));
		_labelTaler.MouseExited += () => _labelTaler.AddThemeColorOverride("font_color", _talerStandardFarbe);

		_main = GetParent<Main>();

		// Die Klickbereiche des Kontors sind unsichtbar; ihre goldene Beschriftung erscheint nur bei MouseOver
		foreach (string areaName in new[] { "AreaHandel", "AreaHinterzimmer", "AreaSchreibstube", "AreaKirche", "AreaKampf", "AreaFenster" })
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

		SetProcessInput(false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override async void _Input(InputEvent @event)
	{
		// Nur auf das diskrete Drücken reagieren (@event statt globalem Input.IsActionPressed), sonst
		// öffnet ein einzelner Tastendruck das Menü mehrfach und es "flackert".
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		GetViewport().SetInputAsHandled();
		SetProcessInput(false);

		// Esc/Rechtsklick öffnet das Ingame-Menü (Optionen, Spieler hinauswerfen, Hauptmenü).
		var ergebnis = await _main.IngameMenuDialog.ShowDialog();

		switch (ergebnis)
		{
			case IngameMenuDialog.Ergebnis.ZumHauptmenue:
				await ZumHauptmenue();
				break;

			case IngameMenuDialog.Ergebnis.SpielerEntferntEnde:
				// Kein menschlicher Spieler mehr übrig: Spiel beenden (Statistik, zurück zum Hauptmenü).
				await ZeigeStatistikWennAktiv();
				Hide();
				break;

			case IngameMenuDialog.Ergebnis.SpielerEntferntWeiter:
				// Der aktive Slot wird jetzt vom nächsten Spieler eingenommen: dessen Zug ankündigen.
				await NaechstenSpielerAnkuendigen();
				break;

			default:  // WeiterSpielen
				SetProcessInput(true);
				break;
		}
	}

	/// <summary>Der bisherige Weg zurück ins Hauptmenü (mit optionalem Speichern und Statistik).</summary>
	private async Task ZumHauptmenue()
	{
		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euer Spiel vorher speichern?") == DialogResultGame.Yes)
		{
			if (_speicherManager.Speichern(SW.Dynamisch.SpielName, out string fehler))
			{
				ClientSettings.LetzterSpielstand = SW.Dynamisch.SpielName;
				await SW.UI.ShowText.ShowDialog("Speichervorgang beendet");
			}
			else
			{
				await SW.UI.ShowText.ShowDialog(fehler);
			}
		}

		await ZeigeStatistikWennAktiv();
		Hide();
	}

	/// <summary>Zeigt am Spielende die Spielerstatistik, sofern die Option "Statistik anzeigen" aktiv ist.</summary>
	private async Task ZeigeStatistikWennAktiv()
	{
		if (ClientSettings.StatistikAnzeigen)
			await _main.StatistikDialog.ShowDialog();
	}

	/// <summary>
	/// Startet das eigentliche Spiel, nachdem alle Spieler erstellt wurden.
	/// </summary>
	public async void StartGame()
	{
		_rundenManager = new RundenManager();
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		_geradeGeladen = false;

		Show();
		await NaechstenSpielerAnkuendigen();
	}

	/// <summary>
	/// Setzt ein geladenes Spiel fort (das Jahresbuch des Vorjahres wird dabei nicht erneut abgewickelt).
	/// </summary>
	public async void ContinueLoadedGame()
	{
		_rundenManager = new RundenManager();
		_speicherManager = new SpeicherManager(ClientSettings.SavegamePath);
		_geradeGeladen = true;

		Show();
		await NaechstenSpielerAnkuendigen();
	}

	private async Task NaechstenSpielerAnkuendigen()
	{
		UpdateHud();
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);

		var spieler = SW.Dynamisch.GetAktHum();
		await _main.NaechsterSpielerDialog.ShowDialog(spieler.GetTitelGegendert() + " " + spieler.GetName() +
		                                              ",\n" + spieler.GetAmtNameUndOrt());

		_rundenManager.BeginneZug();

		if (_rundenManager.SitztAktiverSpielerImKerker())
		{
			_rundenManager.KerkerAufenthaltAbschliessen();
			await SW.UI.ShowText.ShowDialog("Ihr verbringt dieses Jahr im Schuldturm...");

			// Im Schuldturm wird der Zug übersprungen (der Spieler altert dabei nicht)
			_rundenManager.SchalteZumNaechstenSpieler();
			await NaechstenSpielerAnkuendigen();
			return;
		}

		// Ab dem zweiten Jahr: Die Aufträge des Vorjahres im Buch abwickeln (Exporte und Produktion).
		// Nach dem Laden eines Spielstands entfällt das, der Zug wird an Ort und Stelle fortgesetzt.
		if (_geradeGeladen == false && SW.Dynamisch.GetAktuellesJahr() != SW.Statisch.StartJahr)
		{
			var buch = new BuchManager().ErstelleJahresbuchFuerAktivenSpieler();
			UpdateHud();
			await ZeigeBuch(buch);
		}

		// Handelszertifikat-Verleihung (falls dem Spieler eines zusteht) – wie im Original zu Zugbeginn
		if (_geradeGeladen == false)
		{
			var zertifikatManager = new HandelszertifikatManager();

			if (zertifikatManager.StehtZertifikatverleihungAn())
			{
				await _main.HandelszertifikatDialog.ShowDialog(zertifikatManager.Vollziehe());
				UpdateHud();
			}
		}

		_geradeGeladen = false;

		// Stützpunkt-Handel zwischen Spielern: erst die Ergebnisse eigener Angebote melden,
		// dann eingegangene Kaufangebote für eigene Stützpunkte prüfen.
		var soeldnerManager = new SoeldnerRaeuberManager();
		await soeldnerManager.ZeigeHandelsnachrichten();
		await soeldnerManager.VerarbeiteEingehendeKaufangebote();
		UpdateHud();

		// Spieltipp zu Zugbeginn, sofern die Option "Tipps anzeigen" aktiv ist
		if (ClientSettings.TippsAnzeigen)
			await _main.TippsDialog.ShowDialog();

		// Automatisch speichern (wie im Original zu Beginn jedes Zugs)
		if (_speicherManager.Autosave(out string autosaveFehler))
			ClientSettings.LetzterSpielstand = _speicherManager.GetAutosaveName();
		else
			await SW.UI.ShowText.ShowDialog(autosaveFehler);

		SetProcessInput(true);
	}

	private static async Task ZeigeBuch(BuchErgebnis buch)
	{
		string produktion;

		if (buch.EtwasProduziert == false)
		{
			produktion = "Im letzten Jahr habt Ihr keine Waren produziert.";
		}
		else
		{
			produktion = "Produktion\n\n";

			for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
			{
				if (buch.ProduzierteWaren[rohstoffId] == 0)
					continue;

				produktion += string.Format(SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetTextQualitaetProduktion(),
					BuchManager.QualitaetAlsText(buch.ProduktionsQualitaetProzent[rohstoffId])) + ": " +
					buch.ProduzierteWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "\n";
			}

			if (buch.EtwasVerloren)
			{
				produktion += "\nAus Mangel an Lagerraum opfert Ihr folgende Waren an Bedürftige. Welch' edle Tat!\n";

				for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
				{
					if (buch.VerloreneWaren[rohstoffId] != 0)
						produktion += buch.VerloreneWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "\n";
				}
			}
		}

		await SW.UI.ShowText.ShowDialog(produktion);

		string exporte;

		if (buch.EtwasExportiert == false)
		{
			exporte = "Im letzten Jahr habt Ihr keine Waren exportiert.";
		}
		else
		{
			exporte = "Exporte\n\n";

			for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
			{
				if (buch.ExportierteWaren[rohstoffId] != 0)
					exporte += buch.ExportierteWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() +
					           " für " + buch.ExportErloese[rohstoffId].ToStringGeld() + "\n";
			}

			if (buch.EtwasGestohlen)
			{
				exporte += "\nAufgrund von Überfällen habt Ihr folgende Waren verloren.\n";

				for (int rohstoffId = 1; rohstoffId < SW.Statisch.GetMaxRohID(); rohstoffId++)
				{
					if (buch.GestohleneWaren[rohstoffId] > 0)
						exporte += buch.GestohleneWaren[rohstoffId] + " " + SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName() + "\n";
				}
			}
		}

		if (buch.EtwasExportiert)
			SoundManager.Instance.PlayCoins();

		await SW.UI.ShowText.ShowDialog(exporte);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	/// <summary>
	/// Wird von untergeordneten Ansichten (Stadt, Schreibstube, ...) beim Schließen aufgerufen.
	/// </summary>
	public void ReturnFromStadt()
	{
		UpdateHud();
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
		Show();
		SetProcessInput(true);
	}

	private void _on_area_handel_pressed()
	{
		SetProcessInput(false);
		Hide();

		// Wie im Original führt der Handel zunächst auf die politische Weltkarte zur Stadtwahl
		_main.Weltkarte.ZeigeHandelskarte();
	}

	private void OnTalerGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			OnTalerAngeklickt();
	}

	// Klick auf die Taler öffnet die politische Weltkarte im Beziehungs-/Bestechungsmodus (Personen-Modus 0).
	private async void OnTalerAngeklickt()
	{
		SoundManager.Instance.PlayLeftClick();
		SetProcessInput(false);
		Hide();

		await _main.Weltkarte.OeffnePersonenKarte(0, true);

		ReturnFromStadt();
	}

	private void _on_area_schreibstube_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.Schreibstube.ShowSchreibstube();
	}

	private void _on_area_kirche_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.Kirche.ShowKirche();
	}

	private void _on_area_hinterzimmer_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.Hinterzimmer.ShowHinterzimmer();
	}

	private void _on_area_kampf_pressed()
	{
		SetProcessInput(false);
		Hide();
		_main.SoeldnerRaeuberKarte.ZeigeKarte();
	}

	/// <summary>
	/// "Geld zum Fenster rauswerfen": der Spieler zieht sich freiwillig aus dem Spiel zurück. Die
	/// Bestätigung und das Entfernen erledigt die Lib; danach folgt entweder der nächste Spieler oder –
	/// wenn niemand mehr übrig ist – das Spielende (zurück ins Hauptmenü).
	/// </summary>
	private async void _on_area_fenster_pressed()
	{
		SetProcessInput(false);

		bool? ergebnis = await SW.Dynamisch.AktivenSpielerEntfernen();

		if (ergebnis == null)
		{
			// Abgebrochen – der Spieler bleibt im Spiel.
			if (Visible)
				SetProcessInput(true);
			return;
		}

		if (ergebnis.Value)
		{
			// Kein Mitstreiter mehr übrig – zurück ins Hauptmenü.
			HideAndDisableInput();
			return;
		}

		// Der Spieler wurde entfernt, der nächste ist bereits aktiv.
		await NaechstenSpielerAnkuendigen();
	}

	private async void _on_area_nicht_implementiert_pressed()
	{
		SetProcessInput(false);

		// TODO: Söldner & Räuber migrieren (AreaKampf)
		await SW.UI.ShowText.ShowDialog("Wurde noch nicht implementiert");

		if (Visible)
			SetProcessInput(true);
	}

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Kontor A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert();
	}

	private async void _on_button_end_turn_pressed()
	{
		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euren Zug wirklich beenden?") != DialogResultGame.Yes)
		{
			SetProcessInput(true);
			return;
		}

		// Jahresabrechnung berechnen, verbuchen und anzeigen
		var abrechnung = new AbrechnungsManager().ErstelleAbrechnungFuerAktivenSpieler();
		UpdateHud();
		await _main.AbrechnungDialog.ShowDialog(abrechnung);

		// Zug abschließen (Altern und Zug-Flags), damit die Zugnachrichten mit dem neuen Alter rechnen
		_rundenManager.SchliesseZugAb();

		bool spielVorbei = await ZeigeZugnachrichten();

		if (spielVorbei)
		{
			// Zurück ins Hauptmenü
			HideAndDisableInput();
			return;
		}

		await NaechstenSpielerAnkuendigen();
	}

	/// <summary>
	/// Zeigt die Zugnachrichten-Ereignisse an und schaltet danach auf den nächsten Spieler weiter.
	/// </summary>
	/// <returns>True, wenn das Spiel vorbei ist (kein menschlicher Spieler mehr im Spiel).</returns>
	private async Task<bool> ZeigeZugnachrichten()
	{
		var zugNachrichten = new ZugNachrichtenManager();

		// Prüfen, ob dem Spieler dieses Jahr ein höherer Titel zusteht (die Verleihung folgt nach der Hochzeit)
		SW.Dynamisch.VersuchTitelVerleihen(SW.Dynamisch.GetAktiverSpieler());

		// Familienereignisse zu Zugbeginn (Geburt, Hochzeit, Titelverleihung, Kindestod, Brautwerbung)
		await ZeigeFamilienereignisse();

		// Fällt in diesem Jahr ein geplantes Fest an, wird es gefeiert
		string festMeldung = new Conspiratio.Lib.Gameplay.Privilegien.FestGeben.FestManager().FeiereFaelligesFest();

		if (festMeldung != null)
		{
			UpdateHud();
			await SW.UI.ShowText.ShowDialog("Fest\n\n" + festMeldung);
		}

		// Gesetzesverstöße mit Strafen
		foreach (string meldung in zugNachrichten.PruefeVerbrechen())
		{
			UpdateHud();
			await SW.UI.ShowText.ShowDialog(meldung);
		}

		zugNachrichten.ErweitereStatistik();

		// Amtseinkommen
		int einkommen = zugNachrichten.KassiereAmtseinkommen();

		if (einkommen > 0)
		{
			UpdateHud();
			SoundManager.Instance.PlayCoins();
			await SW.UI.ShowText.ShowDialog("Einkommen\n\nAls " + SW.Dynamisch.GetAmtsnameVonSPIDx(SW.Dynamisch.GetAktiverSpieler()) +
			                                " verdient Ihr dieses Jahr " + einkommen.ToStringGeld() + ".");
		}

		// Anwesen (Bauzeit, Zustand, Renovierung)
		foreach (string meldung in zugNachrichten.AktualisiereAnwesen())
			await SW.UI.ShowText.ShowDialog("Eigentümer\n\n" + meldung);

		// Kartenspiel "17 und 4" gegen eine im Hinterzimmer eingeladene KI
		await _main.KartenspielDialog.ShowKartenspiel();
		UpdateHud();

		// Verdeckte Zugende-Ereignisse (in der Reihenfolge des Originals)
		await ZeigeVerdeckteEreignisse(zugNachrichten);

		// Jährliche Zufallsereignisse (Finanz-, Ansehens-, Gesundheits- und Datumsereignisse)
		await ZeigeZufallsereignisse();

		// Anklagen der KI-Spieler erzeugen und die Gerichtsverhandlungen mit dem Spieler abwickeln
		SW.Dynamisch.AnklagenVonKISpielernErstellen();
		await _main.GerichtDialog.ShowGericht();
		UpdateHud();

		// Sterbeprüfung
		if (zugNachrichten.StirbtAktiverSpieler())
		{
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Tod);
			await SW.UI.ShowText.ShowDialog(zugNachrichten.GetZufaelligeTodesursache());

			string name = SW.Dynamisch.GetAktHum().GetName();

			// Testament vollstrecken: Ohne Erben scheidet der Spieler aus, mit Erben führt dieser die Dynastie fort
			var familie = new FamilieManager();
			var testament = familie.FuehreTestamentAus();
			await SW.UI.ShowText.ShowDialog("Hier das Testament...\n\nEuer Vermächtnis geht an: " + testament.ErbeBezeichnung);

			if (testament.SpielVorbei)
			{
				await SW.UI.ShowText.ShowDialog("Der Spieler " + name + " ist verstorben.\nDa niemand als Erbe bestimmt war, befinden sich keine weiteren\nMitstreiter in diesem Spiel. Das Spiel wird daher beendet.");
				return true;
			}

			if (testament.ErbeUebernahm)
			{
				// Der Erbe übernimmt die Identität im selben Slot – der Zug endet und schaltet normal weiter
				UpdateHud();
				await SW.UI.ShowText.ShowDialog("Der Spieler " + name + " ist verstorben.\n" + SW.Dynamisch.GetAktHum().GetName() +
				                                " tritt das Erbe an und führt die Dynastie fort.");
			}
			else
			{
				await SW.UI.ShowText.ShowDialog("Der Spieler " + name + " ist verstorben und wurde aus dem Spiel entfernt.");

				// Der nächste Spieler ist durch die Entfernung bereits aktiv, es darf nicht weitergeschaltet werden
				return false;
			}
		}
		// Schuldenprozess (nur für einen lebenden Spieler)
		else if (zugNachrichten.MussSichVorGlaeubigernVerantworten())
		{
			await SW.UI.ShowText.ShowDialog("Wegen Euren zahlreichen Schulden müsst Ihr Euch nun vor Euren Gläubigern verantworten!");

			var prozess = zugNachrichten.FuehreSchuldenProzessDurch();

			string urteile = "Die Abstimmung Eurer Gläubiger\n\n";

			for (int i = 0; i < prozess.GeschworenenNamen.Count; i++)
				urteile += prozess.GeschworenenNamen[i] + ": " + (prozess.Urteile[i] ? "schuldig!" : "nicht schuldig!") + "\n";

			await SW.UI.ShowText.ShowDialog(urteile);

			await SW.UI.ShowText.ShowDialog(prozess.Schuldig
				? "Aufgrund Eurer zahlreichen Schulden müsst Ihr nächstes\nJahr im Schuldturm verbringen"
				: "Ihr seid noch einmal mit dem Schrecken davon gekommen...");

			UpdateHud();
		}

		await SW.UI.ShowText.ShowDialog("Resümee\n\nIn diesem Jahr gab es keine weiteren besonderen Vorkommnisse");

		// Hat der letzte Spieler seinen Zug beendet, folgen vor dem Jahreswechsel die Rundenende-Ereignisse:
		// zuerst die Wahlen (solange die KI-Kandidaten noch gemeldet sind), dann die Todesfälle unter den KIs
		// (die Ämter freigeben und so die Wahlen des nächsten Jahres vorbereiten) – wie im Original.
		if (_rundenManager.IstLetzterSpielerImJahr())
		{
			await HalteWahlenAb();
			await ZeigeKiTodesfaelle();
			new FamilieManager().VerheirateKis();
			await ZeigeKampfereignisse();
		}

		_rundenManager.SchalteZumNaechstenSpieler();
		return false;
	}

	/// <summary>
	/// Zeigt die verdeckten Zugende-Ereignisse in der Reihenfolge des Originals: Korruptions- und
	/// Schmuggelgelder, Kerkerklatsch, Spionage- und Sabotage-Meldungen, eine beauftragte Ermordung
	/// sowie einen beauftragten vergifteten Wein.
	/// </summary>
	private async Task ZeigeVerdeckteEreignisse(ZugNachrichtenManager zugNachrichten)
	{
		string korruption = zugNachrichten.KassiereKorruptionsgelder();

		if (korruption != null)
		{
			UpdateHud();
			SoundManager.Instance.PlayCoins();
			await SW.UI.ShowText.ShowDialog("Korruptionsgelder\n\n" + korruption);
		}

		string schmuggel = zugNachrichten.KassiereSchmuggelgelder();

		if (schmuggel != null)
		{
			UpdateHud();
			SoundManager.Instance.PlayCoins();
			await SW.UI.ShowText.ShowDialog("Schmuggel\n\n" + schmuggel);
		}

		string kerkerklatsch = zugNachrichten.ErmittleKerkerklatsch();

		if (kerkerklatsch != null)
			await SW.UI.ShowText.ShowDialog("Kerkerklatsch\n\n" + kerkerklatsch);

		string spionage = zugNachrichten.ErmittleSpionageNachrichten();

		if (spionage != null)
			await SW.UI.ShowText.ShowDialog("Spionage\n\n" + spionage);

		string sabotage = zugNachrichten.ErmittleSabotageNachrichten();

		if (sabotage != null)
			await SW.UI.ShowText.ShowDialog("Sabotage\n\n" + sabotage);

		string ermordung = zugNachrichten.FuehreErmordungDurch();

		if (ermordung != null)
			await SW.UI.ShowText.ShowDialog("Ermordung\n\n" + ermordung);

		var vergifteterWein = zugNachrichten.FuehreVergiftetenWeinDurch();

		if (vergifteterWein != null)
		{
			foreach (string meldung in vergifteterWein)
				await SW.UI.ShowText.ShowDialog("Vergifteter Wein\n\n" + meldung);
		}
	}

	/// <summary>
	/// Zeigt die jährlichen Zufallsereignisse des Spielers (Finanz-, Ansehens-, Gesundheits- und
	/// Datumsereignisse). Die Auswirkungen sind bereits im Manager verbucht; hier werden nur die
	/// Meldungen angezeigt und die Spielerleiste aktualisiert.
	/// </summary>
	private async Task ZeigeZufallsereignisse()
	{
		var meldungen = new Conspiratio.Lib.Gameplay.Ereignisse.ZufallsereignisseManager().ErmittleEreignisse();

		foreach (var meldung in meldungen)
		{
			UpdateHud();
			await SW.UI.ShowText.ShowDialog(meldung.Ueberschrift + "\n\n" + meldung.Text);
		}
	}

	/// <summary>
	/// Zeigt zu Zugbeginn die Familienereignisse in der Reihenfolge des Originals: Geburt eines Kindes,
	/// Hochzeit (wenn der umworbene Partner voll verliebt ist), Kindestode und die jährliche Brautwerbung.
	/// </summary>
	private async Task ZeigeFamilienereignisse()
	{
		var familie = new FamilieManager();

		// Geburt eines Kindes (mit passender Ereignis-Musik)
		if (familie.StehtGeburtAn())
		{
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Geburt);
			await _main.GeburtDialog.ShowDialog(familie);
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
			UpdateHud();
		}

		// Hochzeit, wenn der umworbene Partner voll verliebt ist (mit passender Ereignis-Musik)
		if (familie.StehtHochzeitAn())
		{
			var hochzeit = familie.FuehreHochzeitDurch();
			string angebeteter = hochzeit.PartnerMaennlich ? "Euer Angebeteter " : "Eure Angebetete ";

			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Hochzeit);
			await SW.UI.ShowText.ShowDialog("Große Ereignisse werfen ihre Schatten voraus!\n" + angebeteter + hochzeit.PartnerName +
			                                " hat sich endlich bereit erklärt, Euch zu heiraten. Ihr schwebt im siebten Himmel...");
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
			UpdateHud();
		}

		// Titelverleihung, sofern dem Spieler ein höherer Titel zusteht und es einen Regenten gibt
		var titelManager = new TitelVerleihungManager();

		if (titelManager.StehtTitelverleihungAn())
		{
			await _main.TitelVerleihDialog.ShowDialog(titelManager.Vollziehe());
			UpdateHud();
		}

		// Kindestode
		foreach (string meldung in familie.PruefeKindestode())
			await SW.UI.ShowText.ShowDialog(meldung);

		// Jährliche Brautwerbung um den umworbenen Partner
		if (familie.StehtBrautwerbungAn())
		{
			await _main.BrautwerbungDialog.ShowDialog(familie);
			UpdateHud();
		}
	}

	/// <summary>
	/// Hält am Jahresende die anstehenden Wahlen ab: Wahlen mit menschlicher Beteiligung werden
	/// interaktiv ausgezählt, alle übrigen freien Ämter werden mit einem zufälligen KI-Gewinner besetzt.
	/// </summary>
	private async Task HalteWahlenAb()
	{
		var aemterManager = new AemterManager();

		foreach (int wahlId in aemterManager.GetWahlenMitMenschlicherBeteiligung())
			await _main.WahlDialog.ShowWahl(aemterManager, wahlId);

		aemterManager.FuelleRestlicheAemter();
	}

	/// <summary>
	/// Führt am Jahresende die Todesfälle unter den KI-Spielern durch. Die Tode treten immer ein
	/// (freigewordene Ämter werden zu Wahlen des nächsten Jahres); angezeigt werden sie in Seiten zu je
	/// zehn Meldungen, sofern die Option "Todesfälle anzeigen" aktiv ist.
	/// </summary>
	private async Task ZeigeKiTodesfaelle()
	{
		var rundenEndeManager = new RundenEndeManager();
		var meldungen = rundenEndeManager.FuehreKiTodesfaelleDurch();

		if (!rundenEndeManager.SollenTodesfaelleAngezeigtWerden() || meldungen.Count == 0)
			return;

		for (int i = 0; i < meldungen.Count; i += 10)
		{
			int anzahl = System.Math.Min(10, meldungen.Count - i);
			await SW.UI.ShowText.ShowDialog("Todesfälle in diesem Jahr\n\n" + string.Join("\n", meldungen.GetRange(i, anzahl)));
		}
	}

	/// <summary>
	/// Militärische Ereignisse am Jahresende (Migration von frmKampfereignisse): die KI-Stützpunkt-Aktionen
	/// und die stattfindenden Kämpfe werden abgewickelt und der Reihe nach angezeigt.
	/// </summary>
	private async Task ZeigeKampfereignisse()
	{
		var meldungen = new Conspiratio.Lib.Gameplay.Kampf.KampfereignisseManager().ErmittleEreignisse();

		foreach (string meldung in meldungen)
			await SW.UI.ShowText.ShowDialog("Militärische Ereignisse " + SW.Dynamisch.GetAktuellesJahr() + "\n\n" + meldung);
	}
}
