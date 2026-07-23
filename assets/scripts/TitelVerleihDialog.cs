using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Titel;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Titelverleihung (Migration von TitelVerleihForm): zeigt als Urkunde den feierlichen Erlass des
/// Regenten, mit dem dem Spieler ein neuer Adelstitel verliehen wird. Dazu erklingen eine Fanfare und die
/// passende Sprachausgabe. Der Rechtsklick (bzw. Esc) schließt die Urkunde. Der neue Titel wird bereits vom
/// TitelVerleihungManager der Lib gesetzt.
/// </summary>
public partial class TitelVerleihDialog : Control
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
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelText = GetNode<Label>(LabelTextPath);

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Zeigt die Urkunde der Titelverleihung und schließt sie beim Rechtsklick.</summary>
	public Task ShowDialog(TitelverleihungErgebnis ergebnis)
	{
		_labelText.Text = ergebnis.UrkundenText;

		SoundManager.Instance.PlayFanfare();
		SpieleSprachausgabe(ergebnis);

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	/// <summary>Spielt zum verliehenen Titel die passende Sprachausgabe (nach der Fanfare).</summary>
	private static void SpieleSprachausgabe(TitelverleihungErgebnis ergebnis)
	{
		if (ergebnis.TitelTyp == null || !VoiceDateien.TryGetValue(ergebnis.TitelTyp, out var dateien))
			return;

		string stamm = ergebnis.Maennlich ? dateien.Maennlich : dateien.Weiblich;
		SoundManager.Instance.SpieleStimme("res://assets/voice/31_wir_verfuegen_hiermit_" + stamm + ".wav");
	}

	private void CloseDialog()
	{
		Hide();
		SetProcessInput(false);
		_dialogClosed?.TrySetResult(true);
	}
}
