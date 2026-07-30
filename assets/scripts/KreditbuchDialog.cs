using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Das Kreditbuch: listet die offenen Kredite des aktiven Spielers und erlaubt die Tilgung.
/// </summary>
public partial class KreditbuchDialog : DialogBase
{
	[Export]
	public NodePath VBoxKreditePath { get; set; }

	private VBoxContainer _vBoxKredite;
	private PackedScene _linkButtonScene;
	private SchreibstubeManager _schreibstubeManager;


	// Called when the node enters the scene tree for the first time.
	protected override void OnReady()
	{
		_vBoxKredite = GetNode<VBoxContainer>(VBoxKreditePath);
		_linkButtonScene = GD.Load<PackedScene>("res://scenes/controls/LinkButtonWithSounds.tscn");
	}

	public async Task ShowDialog(SchreibstubeManager schreibstubeManager)
	{
		_schreibstubeManager = schreibstubeManager;
		FillKredite();

		await ShowAndAwait();
	}

	private void FillKredite()
	{
		foreach (Node child in _vBoxKredite.GetChildren())
		{
			_vBoxKredite.RemoveChild(child);
			child.QueueFree();
		}

		var kredite = _schreibstubeManager.GetOffeneKredite();

		if (kredite.Count == 0)
		{
			_vBoxKredite.AddChild(new Label
			{
				Text = "Zum Glück steht Ihr zur Zeit bei niemandem in der Kreide",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(600, 0)
			});
			return;
		}

		foreach (var kredit in kredite)
		{
			var zeile = new HBoxContainer();
			zeile.AddThemeConstantOverride("separation", 20);

			zeile.AddChild(new Label
			{
				Text = kredit.Beschreibung,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(480, 0),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ShrinkCenter
			});

			var buttonTilgen = _linkButtonScene.Instantiate<controls.LinkButtonWithSounds>();
			buttonTilgen.Text = "Tilgen";
			buttonTilgen.SizeFlagsVertical = SizeFlags.ShrinkCenter;

			int kreditId = kredit.KreditId;
			buttonTilgen.Pressed += () => OnTilgenPressed(kreditId);

			zeile.AddChild(buttonTilgen);
			_vBoxKredite.AddChild(zeile);
		}
	}

	private async void OnTilgenPressed(int kreditId)
	{
		SetProcessInput(false);

		if (_schreibstubeManager.TilgeKredit(kreditId))
		{
			SoundManager.Instance.PlayCoins();
			FillKredite();
		}
		else
		{
			await SW.UI.ShowText.ShowDialog("Dafür fehlen Euch die Taler.");
		}

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
