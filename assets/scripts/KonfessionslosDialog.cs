using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Glaubensfrage für Konfessionslose (Migration von FormKonfessionslos): der Spieler wählt, ob er
/// den evangelischen oder katholischen Glauben annimmt – oder vorerst konfessionslos bleibt.
/// </summary>
public partial class KonfessionslosDialog : DialogBase
{
	private KircheManager _kircheManager;

	protected override void OnReady()
	{
		HideAndDisableInput();
	}

	public async Task ShowDialog(KircheManager kircheManager)
	{
		_kircheManager = kircheManager;

		await ShowAndAwait();
	}

	private void _on_link_evangelisch_pressed()
	{
		_kircheManager.NimmReligionAn(false);
		SoundManager.Instance.PlayLeftClick();
		Close(DialogResultGame.OK);
	}

	private void _on_link_katholisch_pressed()
	{
		_kircheManager.NimmReligionAn(true);
		SoundManager.Instance.PlayLeftClick();
		Close(DialogResultGame.OK);
	}

	private void _on_link_keinen_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
