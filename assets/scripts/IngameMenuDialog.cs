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
		// Esc/Rechtsklick schließt das Menü wie "Weiter".
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		GetViewport().SetInputAsHandled();
		SoundManager.Instance.PlayRightClick();
		Beenden(Ergebnis.WeiterSpielen);
	}

	public Task<Ergebnis> ShowDialog()
	{
		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<Ergebnis>(TaskCreationOptions.RunContinuationsAsynchronously);
		return _dialogClosed.Task;
	}

	private async void OnOptionen()
	{
		SetProcessInput(false);
		await _main.OptionenDialog.ShowDialog();

		// Nach den Optionen bleibt das Menü offen.
		if (Visible)
			SetProcessInput(true);
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
