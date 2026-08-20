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

	/// <summary>
	/// Der Ordner, in dem die Spielstände liegen (identisch mit dem WinForms-Client,
	/// damit beide Clients dieselben Spielstände sehen).
	/// </summary>
	public static string SavegamePath =>
		Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Conspiratio");

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
		var config = new ConfigFile();
		config.Load(ConfigPath);
		config.SetValue(section, key, wert);
		config.Save(ConfigPath);
	}
}
