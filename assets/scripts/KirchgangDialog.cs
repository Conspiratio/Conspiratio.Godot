using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Kirchgang (Migration von Kirchgang): Ablass kaufen (halbiert die Sünden), beichten (senkt die
/// Sünden um eins) und ein Waisenkind adoptieren (kostet Taler und Ansehen).
/// </summary>
public partial class KirchgangDialog : Control
{
	private KircheManager _kircheManager;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	public async Task ShowDialog(KircheManager kircheManager)
	{
		_kircheManager = kircheManager;

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private async void _on_link_ablass_pressed()
	{
		SetProcessInput(false);

		int kosten = _kircheManager.GetAblassKosten();

		if (kosten == 0)
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt keine Sünden begangen, die einen Ablasskauf bedürfen.");
		}
		else if (!_kircheManager.KannBezahlen(kosten))
		{
			await SW.UI.ShowText.ShowDialog("Die " + kosten.ToStringGeld(false) + " Taler für den Ablass besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr den Ablass für\n" + kosten.ToStringGeld() + " kaufen?", "Ja", "Nein") == DialogResultGame.Yes)
		{
			_kircheManager.KaufeAblass(kosten);
			SoundManager.Instance.PlayCoins();
			await SW.UI.ShowText.ShowDialog("Ein Teil Eurer Sünden ist Euch vergeben.");
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_link_beichten_pressed()
	{
		SetProcessInput(false);

		if (_kircheManager.HatSchonGebeichtet())
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt dieses Jahr bereits genug Sünden gebeichtet!");
		}
		else if (_kircheManager.GetDeliktpunkte() > 0)
		{
			_kircheManager.Beichte();
			await SW.UI.ShowText.ShowDialog("Ihr begebt Euch zu einem Priester, welchem Ihr einen Teil Eurer Sünden gesteht. Entsetzt und widerwillig gewährt Euch dieser die Absolution mit der Aufforderung, das Gotteshaus zu verlassen.");
		}
		else
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt keine Sünden begangen, die einer Beichte bedürfen.");
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_link_waisenkind_pressed()
	{
		SetProcessInput(false);

		if (!_kircheManager.DarfWaisenkindAdoptieren())
		{
			string vaterMutter = SW.Dynamisch.GetAktHum().GetMaennlich() ? "glücklicher Vater" : "glückliche Mutter";
			await SW.UI.ShowText.ShowDialog("Ihr seid derzeit " + vaterMutter + " eines Kindes und könnt daher kein Waisenkind adoptieren.");
		}
		else
		{
			int preis = _kircheManager.GetWaisenkindPreis();

			if (!_kircheManager.KannBezahlen(preis))
			{
				await SW.UI.ShowText.ShowDialog("Die " + preis.ToStringGeld(false) + " Taler für die Adoption besitzt Ihr nicht.");
			}
			else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr ein Mündel für\n" + preis.ToStringGeld() +
			         " aus dem kirchlichen Waisenhaus adoptieren?\nEuer Ansehen könnte darunter leiden ...", "Ja", "Lieber nicht!") == DialogResultGame.Yes)
			{
				string name = _kircheManager.AdoptiereWaisenkind(preis);
				SoundManager.Instance.PlayCoins();
				await SW.UI.ShowText.ShowDialog("Dank Eurer großzügigen Spende konntet Ihr das Kind " + name +
				                                " aus dem Waisenhaus adoptieren. Euer Ansehen hat gelitten.");
			}
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void HideAndDisableInput()
	{
		Hide();
		SetProcessInput(false);
	}

	private void _on_link_button_close_pressed()
	{
		CloseDialog();
	}

	private void CloseDialog()
	{
		HideAndDisableInput();
		_dialogClosed?.TrySetResult(true);
	}

	private Task CloseDialogTask()
	{
		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}
}
