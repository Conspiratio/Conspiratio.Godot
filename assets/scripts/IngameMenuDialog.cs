using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Ingame-Menü des laufenden Spiels (auf Esc/Rechtsklick im Kontor): erlaubt den Zugriff auf die
/// Optionen und das Hinauswerfen des aktiven Spielers, ohne sofort ins Hauptmenü zu springen. Das
/// Ergebnis teilt dem Kontor mit, wie es weitergeht.
/// </summary>
public partial class IngameMenuDialog : Control
{
	public enum Ergebnis
	{
		WeiterSpielen,
		ZumHauptmenue,
		SpielerEntferntWeiter,
		SpielerEntferntEnde
	}

	[Export]
	public NodePath ButtonWeiterPath { get; set; }

	[Export]
	public NodePath ButtonOptionenPath { get; set; }

	[Export]
	public NodePath ButtonSpielerRausPath { get; set; }

	[Export]
	public NodePath ButtonHauptmenuePath { get; set; }

	private Main _main;
	private TaskCompletionSource<Ergebnis> _dialogClosed;

	public override void _Ready()
	{
		GetNode<ButtonWithSounds>(ButtonWeiterPath).Pressed += () => Beenden(Ergebnis.WeiterSpielen);
		GetNode<ButtonWithSounds>(ButtonOptionenPath).Pressed += OnOptionen;
		GetNode<ButtonWithSounds>(ButtonSpielerRausPath).Pressed += OnSpielerRaus;
		GetNode<ButtonWithSounds>(ButtonHauptmenuePath).Pressed += () => Beenden(Ergebnis.ZumHauptmenue);

		_main = GetParent<Main>();

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		// Nur auf das diskrete Drücken reagieren (@event statt globalem Input.IsActionPressed), sonst
		// feuert die Aktion mehrfach pro Tastendruck und das Menü "flackert".
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		GetViewport().SetInputAsHandled();
		SoundManager.Instance.PlayRightClick();
		Beenden(Ergebnis.WeiterSpielen);
	}

	public Task<Ergebnis> ShowDialog()
	{
		Show();

		// Eingabe erst nach dem aktuellen Input-Frame aktivieren, damit das öffnende Esc/Rechtsklick
		// nicht sofort wieder ankommt und das Menü schließt.
		SetProcessInput(false);
		CallDeferred(MethodName.AktiviereEingabe);

		_dialogClosed = new TaskCompletionSource<Ergebnis>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _dialogClosed.Task;
	}

	private void AktiviereEingabe()
	{
		if (Visible)
			SetProcessInput(true);
	}

	private async void OnOptionen()
	{
		SetProcessInput(false);

		// Das Menü ausblenden, damit die Optionen im Vordergrund erscheinen; danach wieder anzeigen.
		Hide();
		await _main.OptionenDialog.ShowDialog();
		Show();
		CallDeferred(MethodName.AktiviereEingabe);
	}

	private async void OnSpielerRaus()
	{
		SetProcessInput(false);

		// Die Lib holt die Bestätigung selbst ein und meldet den Rauswurf.
		bool? ergebnis = await SW.Dynamisch.AktivenSpielerEntfernen();

		if (ergebnis == null)
		{
			// Abgebrochen: Menü bleibt offen.
			if (Visible)
				SetProcessInput(true);
			return;
		}

		Beenden(ergebnis.Value ? Ergebnis.SpielerEntferntEnde : Ergebnis.SpielerEntferntWeiter);
	}

	private void Beenden(Ergebnis ergebnis)
	{
		Hide();
		SetProcessInput(false);
		_dialogClosed?.TrySetResult(ergebnis);
	}
}
