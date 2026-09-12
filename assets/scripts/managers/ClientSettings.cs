using System;
using System.IO;
using Godot;

namespace Conspiratio.Godot.assets.scripts.managers;

/// <summary>
/// Client-seitige Einstellungen, die unabhängig vom Spielstand gespeichert werden
/// (Pendant zu den Properties.Settings des WinForms-Clients).
/// </summary>
public static class ClientSettings
{
	private const string ConfigPath = "user://client.cfg";

	/// <summary>Umgelenkter Spielstandordner (siehe <see cref="UeberschreibeSavegamePath"/>); sonst null.</summary>
	private static string _savegamePathUmgelenkt;

	/// <summary>
	/// Der Ordner, in dem die Spielstände liegen (identisch mit dem WinForms-Client,
	/// damit beide Clients dieselben Spielstände sehen).
	/// </summary>
	public static string SavegamePath =>
		_savegamePathUmgelenkt ??
		Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Conspiratio");

	/// <summary>
	/// Lenkt Spielstände, Profile und Bestenlisten in einen anderen Ordner um – ausschließlich für den
	/// E2E-Treiber, der sonst in die echten Spielstände des Spielers schreibt.
	///
	/// <b>Warum das nötig ist:</b> Der Autosave zu Zugbeginn speichert unter
	/// „&lt;Spielname&gt;_&lt;Jahr&gt;“ und löscht dabei „&lt;Spielname&gt;_&lt;Jahr−2&gt;“
	/// (<c>SpeicherManager.Autosave</c>). Der Treiber nennt sein Spiel „E2E“ – ein lokaler Durchlauf hat
	/// damit reihenweise gleichnamige Spielstände des Spielers gelöscht und nebenbei dessen
	/// <c>profile.json</c> fortgeschrieben. Ein Testlauf darf am echten Ordner nichts ändern.
	/// </summary>
	public static void UeberschreibeSavegamePath(string pfad)
	{
		_savegamePathUmgelenkt = pfad;
		Directory.CreateDirectory(pfad);
	}

	public static string LetzterSpielstand
	{
		get => GetString("spiel", "letzter_spielstand", "");
		set => SetValue("spiel", "letzter_spielstand", value);
	}

	// --- Optionen (Pendant zum WinForms-Einstellungsfenster) ---

	public static bool MusikAusschalten
	{
		get => GetBool("optionen", "musik_ausschalten", false);
		set => SetValue("optionen", "musik_ausschalten", value);
	}

	public static bool TippsAnzeigen
	{
		get => GetBool("optionen", "tipps_anzeigen", true);
		set => SetValue("optionen", "tipps_anzeigen", value);
	}

	/// <summary>Wird ein Duell als Wortgefecht selbst ausgetragen, oder nur als Inszenierung gezeigt?</summary>
	public static bool DuelleInteraktiv
	{
		get => GetBool("optionen", "duelle_interaktiv", true);
		set => SetValue("optionen", "duelle_interaktiv", value);
	}

	public static bool Vollbild
	{
		get => GetBool("optionen", "vollbild", true);
		set => SetValue("optionen", "vollbild", value);
	}

	public static bool StatistikAnzeigen
	{
		get => GetBool("optionen", "statistik_anzeigen", true);
		set => SetValue("optionen", "statistik_anzeigen", value);
	}

	/// <summary>
	/// Vorgabe für neue Spiele, ob die Todesfälle unter den KI-Spielern am Jahresende gemeldet werden.
	/// Der wirksame Wert steht im Spielstand (<c>SW.Dynamisch.TodesfaelleAnzeigen</c>) – die Einstellung
	/// wurde bisher allein beim Anlegen eines Spiels gesetzt und war danach nicht mehr zu ändern.
	/// </summary>
	public static bool TodesfaelleAnzeigen
	{
		get => GetBool("optionen", "todesfaelle_anzeigen", true);
		set => SetValue("optionen", "todesfaelle_anzeigen", value);
	}

	public static bool StuetzpunktereignisseKiAnzeigen
	{
		get => GetBool("optionen", "stuetzpunktereignisse_ki_anzeigen", true);
		set => SetValue("optionen", "stuetzpunktereignisse_ki_anzeigen", value);
	}

	public static bool MilitaerereignisseKiAnzeigen
	{
		get => GetBool("optionen", "militaerereignisse_ki_anzeigen", true);
		set => SetValue("optionen", "militaerereignisse_ki_anzeigen", value);
	}

	public static int MusikLautstaerke
	{
		get => GetInt("optionen", "musik_lautstaerke", 100);
		set => SetValue("optionen", "musik_lautstaerke", value);
	}

	public static int EffektLautstaerke
	{
		get => GetInt("optionen", "effekt_lautstaerke", 100);
		set => SetValue("optionen", "effekt_lautstaerke", value);
	}

	public static int StimmenLautstaerke
	{
		get => GetInt("optionen", "stimmen_lautstaerke", 100);
		set => SetValue("optionen", "stimmen_lautstaerke", value);
	}

	/// <summary>Aggressivität der KI-Spieler als Vorgabe für neue Spiele in Prozent (1–100, Standard 50).</summary>
	public static int KiAggressivitaetProzent
	{
		get => GetInt("optionen", "ki_aggressivitaet_prozent", 50);
		set => SetValue("optionen", "ki_aggressivitaet_prozent", value);
	}

	private static string GetString(string section, string key, string standard)
	{
		var config = new ConfigFile();
		config.Load(ConfigPath);
		return (string)config.GetValue(section, key, standard);
	}

	private static bool GetBool(string section, string key, bool standard)
	{
		var config = new ConfigFile();
		config.Load(ConfigPath);
		return (bool)config.GetValue(section, key, standard);
	}

	private static int GetInt(string section, string key, int standard)
	{
		var config = new ConfigFile();
		config.Load(ConfigPath);
		return (int)config.GetValue(section, key, standard);
	}

	private static void SetValue(string section, string key, Variant wert)
	{
		// Im umgelenkten Betrieb (E2E-Durchlauf) bleibt auch die client.cfg unangetastet: Der Lauf schreibt
		// sonst den zuletzt gespielten Spielstand auf einen Namen, den es nur im Temp-Ordner gibt, und ein
		// zufällig betätigter Knopf im Optionsfenster verstellte die echten Einstellungen des Spielers.
		if (_savegamePathUmgelenkt != null)
			return;

		var config = new ConfigFile();
		config.Load(ConfigPath);
		config.SetValue(section, key, wert);
		config.Save(ConfigPath);
	}
}
