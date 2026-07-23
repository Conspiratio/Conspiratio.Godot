using Godot;

namespace Conspiratio.Godot.assets.scripts.managers;

/// <summary>
/// Wendet die gespeicherten Lautstärke-Einstellungen (ClientSettings) auf die Audio-Busse an.
/// Die Sounds sind den Bussen "Musik", "Effekt" und "Stimmen" zugeordnet (siehe default_bus_layout.tres);
/// die Titelmusik läuft auf "Musik", Klick- und Münz-Sounds auf "Effekt".
/// </summary>
public static class AudioEinstellungen
{
	public const string BusMusik = "Musik";
	public const string BusEffekt = "Effekt";
	public const string BusStimmen = "Stimmen";

	/// <summary>Setzt die Lautstärke eines Busses (0–100 %). Bei 0 % wird der Bus stummgeschaltet.</summary>
	public static void SetLautstaerke(string bus, int prozent)
	{
		int index = AudioServer.GetBusIndex(bus);

		if (index < 0)
			return;

		AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(prozent / 100f));
		AudioServer.SetBusMute(index, prozent <= 0);
	}

	/// <summary>Wendet alle gespeicherten Lautstärke- und Stummschalt-Einstellungen an.</summary>
	public static void AlleAnwenden()
	{
		SetLautstaerke(BusMusik, ClientSettings.MusikLautstaerke);
		SetLautstaerke(BusEffekt, ClientSettings.EffektLautstaerke);
		SetLautstaerke(BusStimmen, ClientSettings.StimmenLautstaerke);

		// "Musik ausschalten" hat Vorrang und schaltet den Musik-Bus zusätzlich stumm.
		if (ClientSettings.MusikAusschalten)
		{
			int index = AudioServer.GetBusIndex(BusMusik);

			if (index >= 0)
				AudioServer.SetBusMute(index, true);
		}
	}
}
