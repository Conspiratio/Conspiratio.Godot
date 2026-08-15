using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Automatischer Durchlauf des Clients (End-to-End): legt ein Spiel an, spielt eine Reihe von Jahren und
/// prüft dabei, dass der Ablauf trägt. Läuft absichtlich **headless** – es werden keine Bilder gemacht,
/// nur der Ablauf und der Spielzustand geprüft. Damit ist der Durchlauf CI-tauglich.
///
/// Gestartet wird er über die eigene Szene:
/// <code>godot --headless --path . "res://scenes/E2eTest.tscn" -- --jahre=5</code>
///
/// Der Treiber beendet den Prozess mit Code 1, sobald etwas schiefgeht – so schlägt der CI-Schritt fehl.
///
/// Wie er den Client bedient: Das Spiel wird über dieselben Lib-Manager angelegt, die auch die Menüs
/// benutzen, dann übernimmt <see cref="Kontor.StartGame"/>. Danach klickt der Treiber wie ein Spieler
/// weiter – alle Overlay-Dialoge liegen in der Gruppe „Dialogs", sodass er jeden offenen Dialog findet,
/// ohne ihn einzeln zu kennen: Ist ein Knopf da, drückt er ihn, sonst schickt er ui_next_or_close.
/// Neue Dialoge werden dadurch automatisch mitbedient.
/// </summary>
public partial class E2eTreiber : Node
{
	/// <summary>Wie viele Jahre gespielt werden, wenn nichts anderes übergeben wurde.</summary>
	private const int StandardJahre = 5;

	/// <summary>Obergrenze an Schritten je Zug – schützt vor einem Dialog, der nie schließt.</summary>
	private const int MaxSchritteProZug = 600;

	/// <summary>So viele Frames ohne offenen Dialog gelten als „der Ablauf ist zur Ruhe gekommen".</summary>
	private const int RuheFrames = 20;

	private readonly List<string> _fehler = new();
	private Main _main;
	private int _dialogKlicks;

	/// <summary>Mit --verbose wird jede Dialogbedienung protokolliert – unentbehrlich, wenn die CI klemmt.</summary>
	private bool _ausfuehrlich;

	public override async void _Ready()
	{
		int jahre = LiesJahreAusKommandozeile();
		_ausfuehrlich = Array.IndexOf(OS.GetCmdlineUserArgs(), "--verbose") >= 0;

		GD.Print("=== E2E-Durchlauf: " + jahre + " Jahre ===");

		try
		{
			await Spiele(jahre);
		}
		catch (Exception ex)
		{
			_fehler.Add("Unerwarteter Fehler: " + ex);
		}

		Bericht(jahre);
		GetTree().Quit(_fehler.Count == 0 ? 0 : 1);
	}

	private async Task Spiele(int jahre)
	{
		_main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
		AddChild(_main);
		await NaechsterFrame();

		LegeSpielAn();

		_main.Kontor.StartGame();

		int startJahr = SW.Dynamisch.GetAktuellesJahr();

		for (int jahr = 0; jahr < jahre; jahr++)
		{
			int jahrVorher = SW.Dynamisch.GetAktuellesJahr();

			if (!await BeendeZug())
			{
				_fehler.Add("Der Zug im Jahr " + jahrVorher + " ließ sich nicht beenden (Dialog blieb offen?).");
				return;
			}

			PruefeZustand(jahrVorher);

			if (SW.Dynamisch.GetAktuellesJahr() == jahrVorher)
			{
				_fehler.Add("Nach dem Zugende stand das Jahr weiterhin auf " + jahrVorher + ".");
				return;
			}

			GD.Print("Jahr " + jahrVorher + " abgeschlossen → " + SW.Dynamisch.GetAktuellesJahr());
		}

		int gespielt = SW.Dynamisch.GetAktuellesJahr() - startJahr;

		if (gespielt != jahre)
			_fehler.Add("Es sollten " + jahre + " Jahre vergehen, tatsächlich waren es " + gespielt + ".");
	}

	/// <summary>Legt ein Einzelspieler-Spiel an – über dieselben Manager, die auch die Menüs benutzen.</summary>
	private static void LegeSpielAn()
	{
		// Die statischen Spieldaten legt der Client sonst beim Klick auf „Lokales Spiel" an
		// (Mainmenu). Ohne sie greifen die folgenden Aufrufe ins Leere.
		SW.Statisch.Initialisieren();

		string pfad = Path.Combine(Path.GetTempPath(), "conspiratio-e2e");
		Directory.CreateDirectory(pfad);

		var neuesSpiel = new NewGameManager(pfad);
		neuesSpiel.CreateNewGame("E2E", 1, false, true, false, out string fehler);

		if (!string.IsNullOrEmpty(fehler))
			throw new Exception("Spiel konnte nicht angelegt werden: " + fehler);

		var setup = new PlayerSetupManager();
		setup.Starte();
		setup.ErstelleSpieler("Testspieler", true, 3, SW.Statisch.GetRelKathID(), 5, true, 1);
		setup.Beende();

		SW.Dynamisch.SetAktiverSpieler(1);
	}

	/// <summary>
	/// Beendet einen Zug: klickt offene Dialoge weg, drückt „Zug beenden" und arbeitet anschließend die
	/// Rundenende-Ereignisse ab, bis der Kontor wieder bedienbar ist.
	/// </summary>
	private async Task<bool> BeendeZug()
	{
		// Erst abwarten, bis der Zugbeginn vollständig durch ist (Ankündigung, Nachrichten, Ereignisse,
		// Autosave) und der Kontor wieder bedienbar ist. Vorher zu drücken würde zwei asynchrone
		// Abläufe verschränken und den Zug hängen lassen.
		if (!await WarteBisKontorBereit())
		{
			_fehler.Add("Der Kontor wurde im Jahr " + SW.Dynamisch.GetAktuellesJahr() + " nicht bedienbereit.");
			return false;
		}

		var zugEnde = _main.Kontor.GetNodeOrNull<BaseButton>("ButtonEndTurn");

		if (zugEnde == null)
		{
			_fehler.Add("Der Knopf „Zug beenden\" wurde im Kontor nicht gefunden.");
			return false;
		}

		int jahrVorher = SW.Dynamisch.GetAktuellesJahr();

		if (_ausfuehrlich)
			GD.Print("  Zugende gedrückt: " + zugEnde.Name + " (" + zugEnde.GetType().Name + ", disabled=" + zugEnde.Disabled + ")");

		zugEnde.EmitSignal(BaseButton.SignalName.Pressed);

		// Der Handler des Knopfes läuft asynchron weiter (async void) – es genügt also nicht, gleich
		// danach nachzusehen. Stattdessen wird gewartet, bis das Jahr umspringt, und alles bedient, was
		// unterwegs aufgeht: Abrechnung, Jahresende-Ereignisse, Wahlen, Katastrophen.
		for (int schritt = 0; schritt < MaxSchritteProZug; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				Bediene(dialog);
				_dialogKlicks++;
			}
			else if (SW.Dynamisch.GetAktuellesJahr() != jahrVorher)
			{
				return true;
			}
			else if (_ausfuehrlich && schritt % 100 == 0)
			{
				GD.Print("  warte (" + schritt + "), Jahr " + SW.Dynamisch.GetAktuellesJahr() + ": " + ZustandDerDialoge()
				         + " | Kontor Eingabe=" + _main.Kontor.IsProcessingInput()
				         + ", aktiver Spieler=" + SW.Dynamisch.GetAktiverSpieler());
			}

			await NaechsterFrame();
		}

		return false;
	}

	/// <summary>Alle sichtbaren Dialoge mit ihrem Eingabezustand – nur für die Fehlersuche.</summary>
	private string ZustandDerDialoge()
	{
		var teile = new List<string>();

		foreach (Node knoten in GetTree().GetNodesInGroup("Dialogs"))
		{
			if (knoten is Control dialog && dialog.IsVisibleInTree())
				teile.Add(dialog.Name + (dialog.IsProcessingInput() ? " (wartend)" : " (passiv)"));
		}

		return teile.Count == 0 ? "kein Dialog sichtbar" : string.Join(", ", teile);
	}

	/// <summary>
	/// Bedient offene Dialoge, bis der Kontor wieder Eingaben annimmt – das ist sein Zeichen dafür, dass
	/// der Zugbeginn abgeschlossen ist. Liefert false, wenn das binnen <see cref="MaxSchritteProZug"/>
	/// Frames nicht eintritt; dann steckt der Ablauf fest, und genau das soll der Test melden.
	/// </summary>
	private async Task<bool> WarteBisKontorBereit()
	{
		int ruhig = 0;

		for (int schritt = 0; schritt < MaxSchritteProZug; schritt++)
		{
			var dialog = FindeOffenenDialog();

			if (dialog != null)
			{
				Bediene(dialog);
				_dialogKlicks++;
				ruhig = 0;
			}
			else
			{
				// Der Zugbeginn ist eine Kette asynchroner Schritte; zwischen zwei Dialogen ist kurz
				// keiner offen. Erst eine anhaltende Ruhephase bei bedienbarem Kontor heißt „fertig".
				ruhig++;

				if (ruhig >= RuheFrames && _main.Kontor.IsProcessingInput())
					return true;
			}

			await NaechsterFrame();
		}

		return false;
	}

	private Control FindeOffenenDialog()
	{
		Control offen = null;

		foreach (Node knoten in GetTree().GetNodesInGroup("Dialogs"))
		{
			// Sichtbarkeit allein genügt nicht: Der Rundennachrichten-Schirm bleibt zwischen zwei
			// Meldungen bewusst stehen (damit nichts durchblitzt) und schaltet dabei nur die Eingabe ab.
			// Wartend – und damit zu bedienen – ist ein Dialog erst, wenn er auch Eingaben verarbeitet.
			if (knoten is Control dialog && dialog.IsVisibleInTree() && dialog.IsProcessingInput())
				offen = dialog;   // der zuletzt gefundene liegt oben
		}

		return offen;
	}

	/// <summary>
	/// Bedient einen Dialog wie ein Spieler: Verlangt er eine Auswahl, wird der erste Knopf gedrückt;
	/// sonst genügt ui_next_or_close (Rechtsklick/Esc).
	/// </summary>
	private void Bediene(Control dialog)
	{
		var knopf = FindeSichtbarenKnopf(dialog);

		if (knopf != null)
		{
			if (_ausfuehrlich)
				GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → Knopf \"" + BeschriftungVon(knopf) + "\"");

			knopf.EmitSignal(BaseButton.SignalName.Pressed);
			return;
		}

		if (_ausfuehrlich)
			GD.Print("  [" + _dialogKlicks + "] " + dialog.Name + " → ui_next_or_close");

		// Drücken **und** loslassen: Die Dialoge prüfen in _Input über Input.IsActionPressed. Bliebe die
		// Aktion gedrückt, würde sie bei jedem weiteren Ereignis erneut auslösen und den Ablauf zerlegen.
		Input.ParseInputEvent(new InputEventAction { Action = "ui_next_or_close", Pressed = true });
		Input.ParseInputEvent(new InputEventAction { Action = "ui_next_or_close", Pressed = false });
	}

	private static string BeschriftungVon(BaseButton knopf)
	{
		return knopf switch
		{
			Button b => b.Text,
			LinkButton l => l.Text,
			_ => knopf.Name
		};
	}

	private static BaseButton FindeSichtbarenKnopf(Node knoten)
	{
		foreach (Node kind in knoten.GetChildren())
		{
			if (kind is BaseButton knopf && knopf.IsVisibleInTree() && !knopf.Disabled)
				return knopf;

			var tiefer = FindeSichtbarenKnopf(kind);

			if (tiefer != null)
				return tiefer;
		}

		return null;
	}

	/// <summary>Prüft nach jedem Zug, dass der Spielzustand plausibel geblieben ist.</summary>
	private void PruefeZustand(int jahr)
	{
		var spieler = SW.Dynamisch.GetAktHum();

		if (spieler == null)
		{
			_fehler.Add("Jahr " + jahr + ": Es gibt keinen aktiven Spieler mehr.");
			return;
		}

		if (spieler.GetGesundheit() < 0 || spieler.GetGesundheit() > SW.Statisch.GetMaxGesundheit())
			_fehler.Add("Jahr " + jahr + ": Gesundheit außerhalb des gültigen Bereichs (" + spieler.GetGesundheit() + ").");

		if (spieler.GetAlter() <= 0)
			_fehler.Add("Jahr " + jahr + ": Das Alter ist " + spieler.GetAlter() + ".");

		if (string.IsNullOrWhiteSpace(spieler.GetName()))
			_fehler.Add("Jahr " + jahr + ": Der Spieler hat keinen Namen mehr.");
	}

	private static int LiesJahreAusKommandozeile()
	{
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--jahre=") && int.TryParse(argument.Substring("--jahre=".Length), out int wert) && wert > 0)
				return wert;
		}

		return StandardJahre;
	}

	private SignalAwaiter NaechsterFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

	private void Bericht(int jahre)
	{
		GD.Print("--- Ergebnis ---");
		GD.Print("Gespielte Jahre: " + (SW.Dynamisch.GetAktuellesJahr() - SW.Statisch.StartJahr) + " von " + jahre + " geplant");
		GD.Print("Bediente Dialoge: " + _dialogKlicks);

		if (_fehler.Count == 0)
		{
			GD.Print("E2E-Durchlauf bestanden.");
			return;
		}

		GD.PrintErr("E2E-Durchlauf fehlgeschlagen (" + _fehler.Count + "):");

		foreach (string fehler in _fehler)
			GD.PrintErr("  - " + fehler);
	}
}
