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
		get
		{
			var config = new ConfigFile();
			config.Load(ConfigPath);
			return (string)config.GetValue("spiel", "letzter_spielstand", "");
		}
		set
		{
			var config = new ConfigFile();
			config.Load(ConfigPath);
			config.SetValue("spiel", "letzter_spielstand", value);
			config.Save(ConfigPath);
		}
	}
}
