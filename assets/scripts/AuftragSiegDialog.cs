using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Einstellungen;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Siegesbildschirm, wenn ein Spieler seinen Auftrag (Mission) erfüllt hat. Zeigt Glückwunsch,
/// Auftragsname und Spieljahr und bietet an, sich mit Namen in die lokale Bestenliste einzutragen
/// (<see cref="HighscoreManager"/>). Rechtsklick/Esc schließt – danach endet das Spiel (zurück ins
/// Hauptmenü, siehe <c>Kontor</c>).
/// </summary>
public partial class AuftragSiegDialog : DialogBase
{
	[Export] public NodePath LabelTitelPath { get; set; }
	[Export] public NodePath LabelTextPath { get; set; }
	[Export] public NodePath LineEditNamePath { get; set; }
	[Export] public NodePath ButtonEintragenPath { get; set; }
	[Export] public NodePath LabelStatusPath { get; set; }

	private Label _labelTitel;
	private Label _labelText;
	private LineEdit _lineEditName;
	private ButtonWithSounds _buttonEintragen;
	private Label _labelStatus;

	private EnumAuftrag _auftrag;
	private int _spieljahr;
	private int _jahreGespielt;
	private int _mitspielerAnzahl;
	private bool _eingetragen;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_labelText = GetNode<Label>(LabelTextPath);
		_lineEditName = GetNode<LineEdit>(LineEditNamePath);
		_buttonEintragen = GetNode<ButtonWithSounds>(ButtonEintragenPath);
		_labelStatus = GetNode<Label>(LabelStatusPath);

		_buttonEintragen.Pressed += OnEintragen;
	}

	/// <summary>Zeigt den Siegesbildschirm für den erfüllten Auftrag.</summary>
	public Task ShowDialog(EnumAuftrag auftrag, string spielerName, int spieljahr, int jahreGespielt, int mitspielerAnzahl)
	{
		_auftrag = auftrag;
		_spieljahr = spieljahr;
		_jahreGespielt = jahreGespielt;
		_mitspielerAnzahl = mitspielerAnzahl;
		_eingetragen = false;

		var info = AuftragManager.GetInfo(auftrag);

		_labelTitel.Text = "Auftrag erfüllt!";
		_labelText.Text =
			"Glückwunsch! Ihr habt den Auftrag „" + (info?.Name ?? "") + "“ erfüllt\n" +
			"und damit das Spiel gewonnen.\n\n" +
			"Spieljahr: A.D. " + spieljahr + "   (nach " + jahreGespielt + " Jahren)\n" +
			"Menschliche Mitspieler: " + mitspielerAnzahl;
		_lineEditName.Text = spielerName ?? "";
		_labelStatus.Text = "Tragt Euch in die Bestenliste ein:";
		_buttonEintragen.Disabled = false;

		return ShowAndAwait();
	}

	private void OnEintragen()
	{
		if (_eingetragen)
			return;

		new HighscoreManager(ClientSettings.SavegamePath)
			.FuegeEintragHinzu(_auftrag, _lineEditName.Text, _spieljahr, _jahreGespielt, _mitspielerAnzahl);

		_eingetragen = true;
		_buttonEintragen.Disabled = true;
		_labelStatus.Text = "In die Bestenliste eingetragen. Rechtsklick oder Esc schließt.";
	}
}
