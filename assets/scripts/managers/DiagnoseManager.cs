using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts.managers;

/// <summary>
/// Autoload-Singleton für die Fehlerdiagnose und das Feedback der Endnutzer. Installiert beim Start
/// globale Exception-Handler (schreibt bei einem Absturz eine Crash-Datei mit Stacktrace) und baut auf
/// Knopfdruck ein Diagnose-Paket (Logs + Spielstand + Profil + Systeminfo) als ZIP, das der Nutzer per
/// vorbereitetem E-Mail-Entwurf an <see cref="Kontaktadresse"/> senden kann. Bietet außerdem
/// Schnell-Sprünge in den Log- bzw. Spielstand-/Profil-Ordner.
/// </summary>
public partial class DiagnoseManager : Node
{
	public const string Kontaktadresse = "mail@conspiratio.net";

	// Dateiname der Profilablage (in der Lib privat, daher hier gespiegelt).
	private const string Profildateiname = "profile.json";

	public static DiagnoseManager Instance { get; private set; }

	private string _logVerzeichnis;
	private string _diagnoseVerzeichnis;
	private string _crashDatei;

	public override void _EnterTree()
	{
		Instance = this;

		string userDir = OS.GetUserDataDir();
		_logVerzeichnis = Path.Combine(userDir, "logs");
		_diagnoseVerzeichnis = Path.Combine(userDir, "diagnose");
		_crashDatei = Path.Combine(_diagnoseVerzeichnis, "crash", "last-crash.txt");

		// Globale Handler für sonst nicht abgefangene Fehler – sowohl aus normalem Code als auch aus
		// nicht abgewarteten Tasks. Beide schreiben nur die Crash-Datei und stören den Ablauf nicht.
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	public override void _ExitTree()
	{
		AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
		TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
	}

	// --- Absturzerfassung ---------------------------------------------------------------------------

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		SchreibeCrash(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
	}

	private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
	{
		SchreibeCrash(e.Exception, "TaskScheduler.UnobservedTaskException");
		e.SetObserved();
	}

	private void SchreibeCrash(Exception ex, string quelle)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_crashDatei) ?? _diagnoseVerzeichnis);
			string inhalt = SystemInfoText(null)
			                + "\nQuelle: " + quelle
			                + "\n\n--- Ausnahme ---\n"
			                + (ex?.ToString() ?? "(keine Ausnahmedetails)")
			                + "\n";
			File.WriteAllText(_crashDatei, inhalt);
			GD.PushError("Conspiratio: Unbehandelte Ausnahme erfasst (" + quelle + "): " + ex?.Message);
		}
		catch
		{
			// Im Fehlerfall darf die Fehlererfassung selbst nichts weiter kaputt machen.
		}
	}

	/// <summary>Liegt aus einem früheren Lauf ein noch nicht gemeldeter Absturz vor?</summary>
	public bool LiegtCrashVor()
	{
		return File.Exists(_crashDatei);
	}

	/// <summary>Inhalt der letzten Crash-Datei (für die Vorbelegung im Melde-Dialog) oder leer.</summary>
	public string GetLetzterCrashText()
	{
		try
		{
			return File.Exists(_crashDatei) ? File.ReadAllText(_crashDatei) : "";
		}
		catch
		{
			return "";
		}
	}

	/// <summary>Quittiert (löscht) die Crash-Datei, nachdem der Nutzer sie gemeldet oder verworfen hat.</summary>
	public void CrashQuittieren()
	{
		try
		{
			if (File.Exists(_crashDatei))
				File.Delete(_crashDatei);
		}
		catch
		{
			// egal – beim nächsten echten Absturz wird ohnehin neu geschrieben
		}
	}

	// --- Diagnose-Paket -----------------------------------------------------------------------------

	/// <summary>
	/// Baut ein ZIP mit Logs, optional dem aktuellen/letzten Spielstand und Profil sowie einer
	/// Systeminfo-Datei. Gibt bei Erfolg den absoluten ZIP-Pfad zurück, sonst null (mit Fehlermeldung).
	/// </summary>
	public string BaueDiagnosePaket(string beschreibung, bool mitSpielstand, out string fehler)
	{
		fehler = "";
		string zeitstempel = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
		string paketVerzeichnis = Path.Combine(_diagnoseVerzeichnis, "paket_" + zeitstempel);
		string zipPfad = Path.Combine(_diagnoseVerzeichnis, "conspiratio-report-" + zeitstempel + ".zip");

		try
		{
			Directory.CreateDirectory(paketVerzeichnis);

			// 1. Systeminfo inkl. Nutzerbeschreibung
			File.WriteAllText(Path.Combine(paketVerzeichnis, "system.txt"), SystemInfoText(beschreibung));

			// 2. Log-Dateien (robust kopieren, da die aktuelle Log-Datei noch offen sein kann). Nur die
			//    neuesten Dateien mitnehmen, damit uralte Logs das Paket nicht unnötig aufblähen.
			if (Directory.Exists(_logVerzeichnis))
			{
				string logsZiel = Path.Combine(paketVerzeichnis, "logs");
				Directory.CreateDirectory(logsZiel);
				var neuesteLogs = new DirectoryInfo(_logVerzeichnis)
					.GetFiles("*.log")
					.OrderByDescending(f => f.LastWriteTime)
					.Take(15);
				foreach (var log in neuesteLogs)
					KopiereDateiRobust(log.FullName, Path.Combine(logsZiel, log.Name));
			}

			// 3. Crash-Datei (falls vorhanden)
			if (File.Exists(_crashDatei))
			{
				string crashZiel = Path.Combine(paketVerzeichnis, "crash");
				Directory.CreateDirectory(crashZiel);
				KopiereDateiRobust(_crashDatei, Path.Combine(crashZiel, "last-crash.txt"));
			}

			// 4. Spielstand + Profil
			if (mitSpielstand)
				SammleSpielstand(paketVerzeichnis);

			// 5. Zippen und Arbeitsverzeichnis wieder aufräumen
			if (File.Exists(zipPfad))
				File.Delete(zipPfad);
			ZipFile.CreateFromDirectory(paketVerzeichnis, zipPfad);

			return zipPfad;
		}
		catch (Exception ex)
		{
			fehler = "Das Diagnose-Paket konnte nicht erstellt werden: " + ex.Message;
			return null;
		}
		finally
		{
			try
			{
				if (Directory.Exists(paketVerzeichnis))
					Directory.Delete(paketVerzeichnis, true);
			}
			catch
			{
				// Reste im diagnose-Ordner sind unkritisch.
			}
		}
	}

	/// <summary>
	/// Legt den aktuellen Spielstand (läuft ein Spiel) bzw. den zuletzt gespeicherten Spielstand sowie
	/// die Profildatei ins Paket. Fehler hier sind nicht fatal – das Paket bleibt trotzdem nützlich.
	/// </summary>
	private void SammleSpielstand(string paketVerzeichnis)
	{
		string savegameZiel = Path.Combine(paketVerzeichnis, "savegame");

		try
		{
			bool spielLaeuft = SW.Dynamisch?.Spielstand != null;
			if (spielLaeuft)
			{
				// Den laufenden Zustand frisch serialisieren – das ist der aussagekräftigste Spielstand.
				new SpeicherManager(savegameZiel).Speichern("aktueller_spielstand", out _);
			}
			else if (Directory.Exists(ClientSettings.SavegamePath))
			{
				// Kein Spiel aktiv: den neuesten vorhandenen Spielstand mitnehmen.
				var neuester = new DirectoryInfo(ClientSettings.SavegamePath)
					.GetFiles("*.json")
					.Where(f => !f.Name.Equals(Profildateiname, StringComparison.OrdinalIgnoreCase))
					.OrderByDescending(f => f.LastWriteTime)
					.FirstOrDefault();
				if (neuester != null)
				{
					Directory.CreateDirectory(savegameZiel);
					KopiereDateiRobust(neuester.FullName, Path.Combine(savegameZiel, neuester.Name));
				}
			}

			// Profildatei immer mitnehmen, wenn vorhanden.
			string profil = Path.Combine(ClientSettings.SavegamePath, Profildateiname);
			if (File.Exists(profil))
			{
				Directory.CreateDirectory(savegameZiel);
				KopiereDateiRobust(profil, Path.Combine(savegameZiel, Profildateiname));
			}
		}
		catch
		{
			// Spielstand-Anhang ist optional; ohne ihn ist das Paket immer noch brauchbar.
		}
	}

	// --- E-Mail & Ordner ----------------------------------------------------------------------------

	/// <summary>
	/// Öffnet das Standard-Mailprogramm mit vorausgefülltem Empfänger, Betreff und Text und zeigt das
	/// ZIP im Dateimanager markiert an, damit der Nutzer es nur noch anhängen und senden muss.
	/// </summary>
	public void OeffneMailEntwurf(string zipPfad, bool istFehler)
	{
		string version = (string)ProjectSettings.GetSetting("application/config/version");
		string betreff = (istFehler ? "Conspiratio Fehlerbericht " : "Conspiratio Feedback ") + version;
		string rumpf =
			"Bitte beschreibt hier, was passiert ist bzw. Euer Feedback:\n\n\n"
			+ "------------------------------------------------------------\n"
			+ "Hinweis: Der Dateimanager wurde bereits geöffnet und die Berichtsdatei\n"
			+ "(" + Path.GetFileName(zipPfad) + ") ist markiert. Bitte hängt sie an diese E-Mail an.\n"
			+ "Speicherort: " + zipPfad + "\n";

		string mailto = "mailto:" + Kontaktadresse
		                + "?subject=" + Uri.EscapeDataString(betreff)
		                + "&body=" + Uri.EscapeDataString(rumpf);

		OS.ShellOpen(mailto);
		OS.ShellShowInFileManager(zipPfad, false);
	}

	/// <summary>Öffnet den Ordner mit den Log-Dateien im Dateimanager.</summary>
	public void OeffneLogOrdner()
	{
		Directory.CreateDirectory(_logVerzeichnis);
		OS.ShellShowInFileManager(_logVerzeichnis, true);
	}

	/// <summary>Öffnet den Ordner mit den Spielständen und der Profildatei im Dateimanager.</summary>
	public void OeffneSpielstandOrdner()
	{
		Directory.CreateDirectory(ClientSettings.SavegamePath);
		OS.ShellShowInFileManager(ClientSettings.SavegamePath, true);
	}

	// --- Hilfsfunktionen ----------------------------------------------------------------------------

	private static string SystemInfoText(string beschreibung)
	{
		string text =
			"Conspiratio – Diagnose\n"
			+ "Zeitpunkt: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n"
			+ "Spielversion: " + ProjectSettings.GetSetting("application/config/version") + "\n"
			+ "Godot: " + Engine.GetVersionInfo()["string"] + "\n"
			+ ".NET: " + System.Environment.Version + "\n"
			+ "Betriebssystem: " + OS.GetName() + " (" + OS.GetVersion() + ")\n";

		if (!string.IsNullOrWhiteSpace(beschreibung))
			text += "\n--- Beschreibung des Nutzers ---\n" + beschreibung + "\n";

		return text;
	}

	/// <summary>
	/// Kopiert eine Datei auch dann, wenn sie noch von einem anderen Prozess (z. B. dem laufenden
	/// Godot-Logger) offen gehalten wird, indem sie mit gemeinsamem Lese-/Schreibzugriff geöffnet wird.
	/// </summary>
	private static void KopiereDateiRobust(string quelle, string ziel)
	{
		using var src = new FileStream(quelle, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
		using var dst = new FileStream(ziel, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
		src.CopyTo(dst);
	}
}
