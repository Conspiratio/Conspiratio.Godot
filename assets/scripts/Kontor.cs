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
		// Cheatfenster mit „U" öffnen, sofern beim neuen Spiel der Cheatmodus aktiviert wurde (wie im Original).
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.U } && SW.Dynamisch.Cheatmodus)
		{
			GetViewport().SetInputAsHandled();
			SetProcessInput(false);
			await _main.CheatDialog.ShowDialog();

			if (Visible)
				SetProcessInput(true);

			return;
		}

		// Nur auf das diskrete Drücken reagieren (@event statt globalem Input.IsActionPressed), sonst
		// öffnet ein einzelner Tastendruck das Menü mehrfach und es "flackert".
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		GetViewport().SetInputAsHandled();

		// Rechtsklick auf einen freien Bereich beendet den Zug (wie im Original), Esc öffnet das Ingame-Menü.
		if (@event is InputEventMouseButton)
		{
			await BeendeZug();
			return;
		}

		SetProcessInput(false);

		// Esc öffnet das Ingame-Menü (Optionen, Spieler hinauswerfen, Hauptmenü).
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

			case IngameMenuDialog.Ergebnis.Geladen:
				// Im Ingame-Menü wurde ein Spielstand geladen: das geladene Spiel fortsetzen.
				ContinueLoadedGame();
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
			WerteProfileFuerSpeichern();

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
	/// Faltet vor jedem Speichern den Statistik-Zuwachs der menschlichen Spieler als Delta in ihre
	/// spielübergreifenden Profile. Muss vor dem Schreiben des Spielstands laufen, damit der aktualisierte
	/// Wertungs-Snapshot mitgespeichert wird (siehe ProfilManager.WerteLaufendesSpiel).
	/// </summary>
	private static void WerteProfileFuerSpeichern()
	{
		new ProfilManager(ClientSettings.SavegamePath).WerteLaufendesSpiel();
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
		string ankuendigung = spieler.GetTitelGegendert() + " " + spieler.GetName() + ",\n" + spieler.GetAmtNameUndOrt();

		// Bei aktivem Auftrag den Fortschritt als Erinnerung mit ankündigen.
		if (AuftragManager.IstAuftragAktiv())
			ankuendigung += "\n\n" + new AuftragManager().GetFortschrittText(spieler);

		await _main.NaechsterSpielerDialog.ShowDialog(ankuendigung);

		_rundenManager.BeginneZug();

		if (_rundenManager.SitztAktiverSpielerImKerker())
		{
			_rundenManager.KerkerAufenthaltAbschliessen();

			// Wie im Original ein eigener Vollbild-Kerkerbildschirm (HintKerker) mit dunkler Musik.
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Hinterzimmer);
			await _main.SchuldturmDialog.ShowDialog();
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);

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

		// Mätresse: jährlicher Unterhalt und Skandal-Risiko (Issue #8).
		if (_geradeGeladen == false)
		{
			var maetresse = new MaetresseManager();

			if (maetresse.HatMaetresse())
			{
				string maetresseMeldung = maetresse.VerarbeiteJahr();
				UpdateHud();

				if (maetresseMeldung != null)
					await _main.RundenNachrichtenDialog.ShowDialog(maetresseMeldung);
			}
		}

		// KI-Beleidigung (selten, Issue #17): Eine KI beleidigt den Spieler – er wählt Satisfaktion (Duell
		// im Morgengrauen) oder Verzicht (dann leidet sein Ansehen).
		if (_geradeGeladen == false)
		{
			var fechtDuell = new FechtDuellManager();
			int beleidiger = fechtDuell.PruefeKiBeleidigtSpieler();

			if (beleidiger != 0)
			{
				bool satisfaktion = await SW.UI.YesNoQuestion.ShowDialogText(
					fechtDuell.GetSatisfaktionsFrage(beleidiger), "Duell im Morgengrauen", "Verzichten") == DialogResultGame.Yes;

				if (satisfaktion)
				{
					// Das Duell wird als Vollbild-Szene ausgetragen (Wortgefecht) und erst danach ausgewertet.
					var gefecht = new WortgefechtManager(beleidiger);
					string gegnerName = SW.Dynamisch.GetSpWithID(beleidiger).GetKompletterName();

					bool gewonnen = await _main.DuellDialog.SpieleWortgefecht(gefecht, gegnerName);
					var ergebnis = fechtDuell.WendeDuellAusgangAn(beleidiger, gewonnen);

					await _main.DuellDialog.ZeigeAusgang(ergebnis.SpielerHatGewonnen, ergebnis.GegnerName,
						ergebnis.AmtVerloren, ergebnis.AmtName);
				}
				else
					await _main.RundenNachrichtenDialog.ShowDialog("Ehrensache\n\n" + fechtDuell.VerweigereSatisfaktion(SW.Dynamisch.GetAktiverSpieler()));

				UpdateHud();
			}
		}

		_geradeGeladen = false;

		// Ankündigung der neu zu besetzenden Ämter zu Zugbeginn (wie im Original), sofern es welche gibt.
		string aemterAnkuendigung = new AemterManager().GetFreieAemterAnkuendigung();

		if (aemterAnkuendigung != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Ämterwahl\n\n" + aemterAnkuendigung);

		// Stützpunkt-Handel zwischen Spielern: erst die Ergebnisse eigener Angebote melden,
		// dann eingegangene Kaufangebote für eigene Stützpunkte prüfen.
		var soeldnerManager = new SoeldnerRaeuberManager();
		await soeldnerManager.ZeigeZolleinnahmen();  // eingenommene Zölle der eigenen Zollburgen melden
		await soeldnerManager.ZeigeHandelsnachrichten();
		soeldnerManager.GeneriereKiKaufangebote();  // zufällige KI-Angebote für zum Verkauf angebotene Stützpunkte
		await soeldnerManager.VerarbeiteEingehendeKaufangebote();
		UpdateHud();

		// Spieltipp zu Zugbeginn, sofern die Option "Tipps anzeigen" aktiv ist
		if (ClientSettings.TippsAnzeigen)
			await _main.TippsDialog.ShowDialog();

		// Automatisch speichern (wie im Original zu Beginn jedes Zugs)
		WerteProfileFuerSpeichern();

		if (_speicherManager.Autosave(out string autosaveFehler))
			ClientSettings.LetzterSpielstand = _speicherManager.GetAutosaveName();
		else
			await SW.UI.ShowText.ShowDialog(autosaveFehler);

		// Einen evtl. noch sichtbaren Nachrichtenschirm ausblenden, bevor die Bedienung wieder aktiv wird.
		DialogBase.VerbergeNachrichtenschirm();

		SetProcessInput(true);
	}

	private async Task ZeigeBuch(BuchErgebnis buch)
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

		// Wie im Original auf dem aufgeschlagenen Buch: Produktion links, Exporte rechts.
		await _main.JahresbuchDialog.ShowDialog("Jahresbuch A.D. " + SW.Dynamisch.GetAktuellesJahr(), produktion, exporte);
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

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Kontor A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert();
	}

	private async void _on_button_end_turn_pressed()
	{
		await BeendeZug();
	}

	/// <summary>Beendet den Zug des Spielers (Abrechnung, Zugnachrichten, nächster Spieler). Wird vom
	/// "Runde beenden"-Bereich und vom Rechtsklick auf einen freien Bereich ausgelöst.</summary>
	private async Task BeendeZug()
	{
		SetProcessInput(false);

		if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euren Zug wirklich beenden?") != DialogResultGame.Yes)
		{
			SetProcessInput(true);
			return;
		}

		// Den Spieler merken, dessen Zug hier endet (die Auftragsprüfung folgt nach den Zugnachrichten,
		// die den aktiven Spieler bereits weiterschalten können).
		var spielerAmZugende = SW.Dynamisch.GetAktHum();

		// Fällige (überfällige) Kredite zwangsweise tilgen – notfalls rutscht das Vermögen dabei ins Minus.
		// Vor der Abrechnung, damit ein dadurch negatives Vermögen den anschließenden Schuldenprozess auslösen kann.
		foreach (string kreditMeldung in new SchreibstubeManager().TilgeUeberfaelligeKredite())
			await _main.RundenNachrichtenDialog.ShowDialog("Fälliger Kredit\n\n" + kreditMeldung);

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

		// Auftrag (Mission) prüfen: erfüllt der Spieler seinen Auftrag, hat er das Spiel gewonnen.
		if (await PruefeAuftragErfuellt(spielerAmZugende))
		{
			HideAndDisableInput();
			return;
		}

		await NaechstenSpielerAnkuendigen();
	}

	/// <summary>
	/// Prüft nach dem Zug, ob der Spieler seinen gewählten Auftrag erfüllt hat. Ist das der Fall, wird der
	/// Siegesbildschirm samt Bestenlisten-Eintrag gezeigt und true zurückgegeben (das Spiel endet dann).
	/// Ohne aktiven Auftrag oder bei Nichterfüllung passiert nichts (false).
	/// </summary>
	private async Task<bool> PruefeAuftragErfuellt(Conspiratio.Lib.Gameplay.Personen.HumSpieler spieler)
	{
		if (!AuftragManager.IstAuftragAktiv() || spieler == null)
			return false;

		if (!new AuftragManager().AktualisiereFortschrittUndPruefe(spieler))
			return false;

		var auftrag = AuftragManager.GetAktiverAuftrag();
		int jahr = SW.Dynamisch.GetAktuellesJahr();
		int jahreGespielt = jahr - SW.Statisch.StartJahr;
		int mitspieler = SW.Dynamisch.Spielstand.AktiveSpielerAnzahl;

		await _main.AuftragSiegDialog.ShowDialog(auftrag, spieler.GetName(), jahr, jahreGespielt, mitspieler);
		return true;
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
			await _main.RundenNachrichtenDialog.ShowDialog("Fest\n\n" + festMeldung);
		}

		// Gesetzesverstöße mit Strafen
		foreach (string meldung in zugNachrichten.PruefeVerbrechen())
		{
			UpdateHud();
			await _main.RundenNachrichtenDialog.ShowDialog(meldung);
		}

		zugNachrichten.ErweitereStatistik();

		// Amtseinkommen
		int einkommen = zugNachrichten.KassiereAmtseinkommen();

		if (einkommen > 0)
		{
			UpdateHud();
			SoundManager.Instance.PlayCoins();
			await _main.RundenNachrichtenDialog.ShowDialog("Einkommen\n\nAls " + SW.Dynamisch.GetAmtsnameVonSPIDx(SW.Dynamisch.GetAktiverSpieler()) +
			                                " verdient Ihr dieses Jahr " + einkommen.ToStringGeld() + ".");
		}

		// Anwesen (Bauzeit, Zustand, Renovierung)
		foreach (string meldung in zugNachrichten.AktualisiereAnwesen())
			await _main.RundenNachrichtenDialog.ShowDialog("Eigentümer\n\n" + meldung);

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

			// Daten des Verstorbenen erfassen, bevor der Erbe die Identität übernimmt.
			var verstorbener = SW.Dynamisch.GetAktHum();
			string name = verstorbener.GetName();
			string titel = verstorbener.GetTitelGegendert();
			int geburtsjahr = SW.Dynamisch.GetAktuellesJahr() - verstorbener.GetAlter();
			int todesjahr = SW.Dynamisch.GetAktuellesJahr();
			string todesursache = zugNachrichten.GetZufaelligeTodesursache();
			string grabspruch = new GrabsteinManager().ErmittleGrabspruch(verstorbener.GetSpielerStatistik());

			// Testament vollstrecken: Ohne Erben scheidet der Spieler aus, mit Erben führt dieser die Dynastie fort
			var familie = new FamilieManager();
			var testament = familie.FuehreTestamentAus();

			string erbmeldung;
			if (testament.SpielVorbei)
				erbmeldung = "Da niemand als Erbe bestimmt war, endet mit ihm die Dynastie – und dieses Spiel.";
			else if (testament.ErbeUebernahm)
				erbmeldung = SW.Dynamisch.GetAktHum().GetName() + " tritt das Erbe an und führt die Dynastie fort.";
			else
				erbmeldung = name + " wurde aus dem Spiel entfernt.";

			string titelTeil = string.IsNullOrEmpty(titel) ? "" : ", " + titel;
			string grabinschrift = "Hier ruht " + name + titelTeil + "\n* " + geburtsjahr + "   † " + todesjahr +
			                       "\n\n„" + grabspruch + "“\n\n" + erbmeldung;

			await _main.SpielerTodDialog.ShowDialog(todesursache, grabinschrift);

			if (testament.SpielVorbei)
				return true;

			if (testament.ErbeUebernahm)
			{
				// Der Erbe übernimmt die Identität im selben Slot – der Zug endet und schaltet normal weiter
				UpdateHud();
			}
			else
			{
				// Der nächste Spieler ist durch die Entfernung bereits aktiv, es darf nicht weitergeschaltet werden
				return false;
			}
		}
		// Schuldenprozess (nur für einen lebenden Spieler)
		else if (zugNachrichten.MussSichVorGlaeubigernVerantworten())
		{
			await _main.RundenNachrichtenDialog.ShowDialog("Wegen Euren zahlreichen Schulden müsst Ihr Euch nun vor Euren Gläubigern verantworten!");

			var prozess = zugNachrichten.FuehreSchuldenProzessDurch();

			string urteile = "Die Abstimmung Eurer Gläubiger\n\n";

			for (int i = 0; i < prozess.GeschworenenNamen.Count; i++)
				urteile += prozess.GeschworenenNamen[i] + ": " + (prozess.Urteile[i] ? "schuldig!" : "nicht schuldig!") + "\n";

			await _main.RundenNachrichtenDialog.ShowDialog(urteile);

			await _main.RundenNachrichtenDialog.ShowDialog(prozess.Schuldig
				? "Aufgrund Eurer zahlreichen Schulden müsst Ihr nächstes\nJahr im Schuldturm verbringen"
				: "Ihr seid noch einmal mit dem Schrecken davon gekommen...");

			UpdateHud();
		}

		await _main.RundenNachrichtenDialog.ShowDialog("Resümee\n\nIn diesem Jahr gab es keine weiteren besonderen Vorkommnisse");

		// Hat der letzte Spieler seinen Zug beendet, folgen vor dem Jahreswechsel die Rundenende-Ereignisse:
		// zuerst die Wahlen (solange die KI-Kandidaten noch gemeldet sind), dann die Todesfälle unter den KIs
		// (die Ämter freigeben und so die Wahlen des nächsten Jahres vorbereiten) – wie im Original.

		if (_rundenManager.IstLetzterSpielerImJahr())
		{
			await HalteWahlenAb();
			// KI-Spieler begehen zufällig Straftaten (Issue #18); sie werden per Spione als Beweise
			// erkennbar und bei einer Anklage im nächsten Jahr vor Gericht herangezogen.
			new RundenEndeManager().FuehreKiStraftatenDurch();
			await ZeigeKiTodesfaelle();
			new FamilieManager().VerheirateKis();
			await ZeigeKatastrophe();
			await ZeigeKampfereignisse();
		}

		_rundenManager.SchalteZumNaechstenSpieler();
		return false;
	}

	/// <summary>
	/// Katastrophen am Jahresende (Issue #37): Sturm, Flut, Brand, Erdbeben oder Pest suchen selten, dann
	/// aber heftig eine Stadt, eine Grafschaft oder das ganze Reich heim. Verluste des Spielers werden
	/// unter der Meldung aufgeführt.
	/// </summary>
	private async Task ZeigeKatastrophe()
	{
		var ergebnis = new KatastrophenManager().FuehreKatastrophenDurch();

		if (!ergebnis.Eingetreten)
			return;

		string text = ergebnis.Meldung;

		if (ergebnis.SpielerMeldungen.Count > 0)
			text += "\n\n" + string.Join("\n", ergebnis.SpielerMeldungen);

		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Tod);
		await _main.RundenNachrichtenDialog.ShowDialog("Katastrophe\n\n" + text);
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);

		UpdateHud();
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
			await _main.RundenNachrichtenDialog.ShowDialog("Korruptionsgelder\n\n" + korruption);
		}

		string schmuggel = zugNachrichten.KassiereSchmuggelgelder();

		if (schmuggel != null)
		{
			UpdateHud();
			SoundManager.Instance.PlayCoins();
			await _main.RundenNachrichtenDialog.ShowDialog("Schmuggel\n\n" + schmuggel);
		}

		string kerkerklatsch = zugNachrichten.ErmittleKerkerklatsch();

		if (kerkerklatsch != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Kerkerklatsch\n\n" + kerkerklatsch);

		string spionage = zugNachrichten.ErmittleSpionageNachrichten();

		if (spionage != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Spionage\n\n" + spionage);

		string sabotage = zugNachrichten.ErmittleSabotageNachrichten();

		if (sabotage != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Sabotage\n\n" + sabotage);

		string ermordung = zugNachrichten.FuehreErmordungDurch();

		if (ermordung != null)
			await _main.RundenNachrichtenDialog.ShowDialog("Ermordung\n\n" + ermordung);

		var vergifteterWein = zugNachrichten.FuehreVergiftetenWeinDurch();

		if (vergifteterWein != null)
		{
			foreach (string meldung in vergifteterWein)
				await _main.RundenNachrichtenDialog.ShowDialog("Vergifteter Wein\n\n" + meldung);
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
			await _main.RundenNachrichtenDialog.ShowDialog(meldung.Ueberschrift + "\n\n" + meldung.Text);
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
			await _main.RundenNachrichtenDialog.ShowDialog("Große Ereignisse werfen ihre Schatten voraus!\n" + angebeteter + hochzeit.PartnerName +
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

		// Kindestode – wie im Original als eigener Vollbildschirm (HintKindStirbt) mit Todesmusik.
		var kindestode = familie.PruefeKindestode();

		if (kindestode.Count > 0)
		{
			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Tod);

			foreach (string meldung in kindestode)
				await _main.KindestodDialog.ShowDialog(meldung);

			SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
			UpdateHud();
		}

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

		// Wahlen nach Amtsstufe absteigend (höchstes Amt zuerst). Gewinnt der Spieler ein höheres Amt,
		// zieht er sich aus den niedrigeren Wahlen zurück – diese werden dann hier übersprungen und weiter
		// unten per FuelleRestlicheAemter mit einem KI-Gewinner ausgezählt.
		foreach (int wahlId in aemterManager.GetWahlenMitMenschlicherBeteiligung())
			if (aemterManager.HatMenschlicheBeteiligung(wahlId))
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
			await _main.RundenNachrichtenDialog.ShowDialog("Todesfälle in diesem Jahr\n\n" + string.Join("\n", meldungen.GetRange(i, anzahl)));
		}
	}

	/// <summary>
	/// Militärische Ereignisse am Jahresende (Migration von frmKampfereignisse): die KI-Stützpunkt-Aktionen
	/// und die stattfindenden Kämpfe werden abgewickelt und der Reihe nach angezeigt.
	/// </summary>
	private async Task ZeigeKampfereignisse()
	{
		// Filter aus den Optionen: KI-Stützpunktereignisse ganz ausblenden bzw. Militärereignisse nur bei
		// menschlicher Beteiligung zeigen (die Aktionen/Kämpfe werden unabhängig davon immer abgewickelt).
		var meldungen = new Conspiratio.Lib.Gameplay.Kampf.KampfereignisseManager().ErmittleEreignisse(
			ClientSettings.StuetzpunktereignisseKiAnzeigen, ClientSettings.MilitaerereignisseKiAnzeigen);

		// Während der militärischen Meldungen (Rechtsklick-Wartezeiten) läuft die Kampfmusik als Schleife.
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Kampf);

		// Wie im Original: alle Meldungen auf einer Seite, per Rechtsklick nacheinander angehängt
		// (durch eine Leerzeile getrennt); bei Überlauf scrollt der Text ans Ende.
		await _main.RundenNachrichtenDialog.ShowKampfereignisse(
			"Militärische Ereignisse " + SW.Dynamisch.GetAktuellesJahr(), meldungen);

		// Danach zurück zur normalen Hintergrundmusik.
		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Standard);
	}
}
