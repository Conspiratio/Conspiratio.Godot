using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Melde-Dialog für Fehler und Feedback. Der Nutzer beschreibt optional das Problem, entscheidet, ob
/// der aktuelle Spielstand beigelegt wird, und löst mit einem Klick den Aufbau des Diagnose-Pakets
/// samt vorbereitetem E-Mail-Entwurf aus (siehe <see cref="DiagnoseManager"/>). Zusätzlich springt er
/// per Knopf in den Log- bzw. Spielstand-/Profil-Ordner. Rechtsklick/Esc schließt (Standardmuster).
/// </summary>
public partial class DiagnoseDialog : DialogBase
{
	[Export] public NodePath LabelTitelPath { get; set; }
	[Export] public NodePath TextEditBeschreibungPath { get; set; }
	[Export] public NodePath CheckBoxSpielstandPath { get; set; }
	[Export] public NodePath ButtonBerichtPath { get; set; }
	[Export] public NodePath ButtonLogsPath { get; set; }
	[Export] public NodePath ButtonSpielstandOrdnerPath { get; set; }
	[Export] public NodePath LabelStatusPath { get; set; }

	private Label _labelTitel;
	private TextEdit _textEditBeschreibung;
	private CheckBox _checkBoxSpielstand;
	private Label _labelStatus;

	private bool _istFehler;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_textEditBeschreibung = GetNode<TextEdit>(TextEditBeschreibungPath);
		_checkBoxSpielstand = GetNode<CheckBox>(CheckBoxSpielstandPath);
		_labelStatus = GetNode<Label>(LabelStatusPath);

		GetNode<ButtonWithSounds>(ButtonBerichtPath).Pressed += OnBerichtErstellen;
		GetNode<ButtonWithSounds>(ButtonLogsPath).Pressed += OnLogsOeffnen;
		GetNode<ButtonWithSounds>(ButtonSpielstandOrdnerPath).Pressed += OnSpielstandOrdnerOeffnen;
	}

	/// <summary>
	/// Zeigt den Dialog. <paramref name="istFehler"/> stellt Titel/Vorbelegung auf den Fehlerfall um
	/// (z. B. nach einem erfassten Absturz); sonst dient er dem allgemeinen Feedback.
	/// </summary>
	public Task ShowDialog(bool istFehler = false)
	{
		_istFehler = istFehler;
		_labelTitel.Text = istFehler ? "Fehler melden" : "Feedback & Fehler melden";
		_textEditBeschreibung.Text = "";
		_checkBoxSpielstand.ButtonPressed = true;
		_labelStatus.Text = istFehler
			? "Beim letzten Start wurde ein Fehler erfasst. Erstellt bitte einen Bericht."
			: "";

		return ShowAndAwait();
	}

	private void OnBerichtErstellen()
	{
		string zipPfad = DiagnoseManager.Instance.BaueDiagnosePaket(
			_textEditBeschreibung.Text, _checkBoxSpielstand.ButtonPressed, out string fehler);

		if (zipPfad == null)
		{
			_labelStatus.Text = fehler;
			return;
		}

		DiagnoseManager.Instance.OeffneMailEntwurf(zipPfad, _istFehler);

		// Ein gemeldeter Absturz gilt danach als erledigt und wird beim nächsten Start nicht erneut angeboten.
		if (_istFehler)
			DiagnoseManager.Instance.CrashQuittieren();

		_labelStatus.Text = "Bericht erstellt. Euer E-Mail-Programm wurde geöffnet – bitte die im\n"
		                    + "Dateimanager markierte Datei anhängen und die Nachricht absenden.";
	}

	private void OnLogsOeffnen()
	{
		DiagnoseManager.Instance.OeffneLogOrdner();
		_labelStatus.Text = "Der Log-Ordner wurde im Dateimanager geöffnet.";
	}

	private void OnSpielstandOrdnerOeffnen()
	{
		DiagnoseManager.Instance.OeffneSpielstandOrdner();
		_labelStatus.Text = "Der Spielstand- und Profil-Ordner wurde im Dateimanager geöffnet.";
	}
}
