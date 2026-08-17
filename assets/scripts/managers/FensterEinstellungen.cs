using Godot;

namespace Conspiratio.Godot.assets.scripts.managers;

/// <summary>
/// Wendet die gespeicherte Fenstermodus-Einstellung (ClientSettings.Vollbild) auf das Fenster an.
/// </summary>
public static class FensterEinstellungen
{
	public static void AlleAnwenden()
	{
		DisplayServer.WindowSetMode(ClientSettings.Vollbild
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);
	}
}
