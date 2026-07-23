using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Credits-Fenster (Migration von CreditsAusfuehren): zeigt auf dem Original-Hintergrund die
/// Mitwirkenden (Programm, Grafiken, Wiki, Musik, Sounds, Icons). Der Rechtsklick (bzw. Esc) schließt
/// die Credits und kehrt zum Hauptmenü zurück.
/// </summary>
public partial class CreditsDialog : Control
{
	private const string CreditsText =
		"Programm:\nDerEinzehnte, SirToby\n\n\n" +
		"Grafiken:\nBeetle\n\n\n" +
		"Wiki:\nPommBaer\n\n\n" +
		"Musik:\nJason Shaw (Audionautix.com),\nStrobotone,\nMichael Ziege\n\n\n" +
		"Sounds:\ncsmag, BraveFrog, sarson,\nflorian_reinke, bevibeldesign\n\n\n" +
		"Vector Icons: Vecteezy.com";

	public override void _Ready()
	{
		GetNode<Label>("TextureRect/LabelCredits").Text = CreditsText;

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		Hide();
		SetProcessInput(false);
	}

	/// <summary>Zeigt die Credits an; der Rechtsklick schließt sie wieder.</summary>
	public void ShowDialog()
	{
		Show();
		SetProcessInput(true);
	}
}
