using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Titel;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Titelverleihung (Migration von TitelVerleihForm): zeigt als Urkunde den feierlichen Erlass des
/// Regenten, mit dem dem Spieler ein neuer Adelstitel verliehen wird. Dazu erklingen eine Fanfare und die
/// passende Sprachausgabe. Der Rechtsklick (bzw. Esc) schließt die Urkunde. Der neue Titel wird bereits vom
/// TitelVerleihungManager der Lib gesetzt.
/// </summary>
public partial class TitelVerleihDialog : DialogBase
{
	// Zu jedem Titel-Typ die Sprachausgabe-Datei (männlich, weiblich) – wie im Original.
	private static readonly Dictionary<string, (string Maennlich, string Weiblich)> VoiceDateien = new()
	{
		["Buerger"] = ("buerger", "buergerin"),
		["Edelmann"] = ("edelmann", "edelfrau"),
		["Ritter"] = ("ritter", "hofdame"),
		["Landherr"] = ("landherr", "landfrau"),
		["Freiherr"] = ("freiherr", "freifrau"),
		["Baron"] = ("baron", "baronin"),
		["Graf"] = ("graf", "graefin"),
		["Herzog"] = ("herzog", "herzogin"),
		["Fuerst"] = ("fuerst", "fuerstin")
	};

	[Export]
	public NodePath LabelTextPath { get; set; }

	private Label _labelText;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
	}

	/// <summary>Zeigt die Urkunde der Titelverleihung und schließt sie beim Rechtsklick.</summary>
	public Task ShowDialog(TitelverleihungErgebnis ergebnis)
	{
		_labelText.Text = ergebnis.UrkundenText;

		SpieleFanfareUndSprachausgabe(ergebnis);

		return ShowAndAwait();
	}

	/// <summary>
	/// Spielt zuerst die Fanfare und – erst nachdem diese verklungen ist – die Sprachausgabe, damit sich
	/// beide wie im Original nicht überlappen.
	/// </summary>
	private async void SpieleFanfareUndSprachausgabe(TitelverleihungErgebnis ergebnis)
	{
		SoundManager.Instance.PlayFanfare();

		double fanfareLaenge = SoundManager.Instance.GetFanfareLaenge();
		if (fanfareLaenge > 0)
			await ToSignal(GetTree().CreateTimer(fanfareLaenge), SceneTreeTimer.SignalName.Timeout);

		SpieleSprachausgabe(ergebnis);
	}

	/// <summary>Spielt zum verliehenen Titel die passende Sprachausgabe (nach der Fanfare).</summary>
	private static void SpieleSprachausgabe(TitelverleihungErgebnis ergebnis)
	{
		if (ergebnis.TitelTyp == null || !VoiceDateien.TryGetValue(ergebnis.TitelTyp, out var dateien))
			return;

		string stamm = ergebnis.Maennlich ? dateien.Maennlich : dateien.Weiblich;
		SoundManager.Instance.SpieleStimme("res://assets/voice/31_wir_verfuegen_hiermit_" + stamm + ".wav");
	}
}
