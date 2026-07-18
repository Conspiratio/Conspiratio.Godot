using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Anwesen-Verwaltung nach der WinForms-Vorlage HausBauen: zeigt den Wohnsitz der aktiven Stadt
/// (kein Haus / im Bau / fertig) und bietet je nach Zustand Bauen bzw. Umbauen, Renovieren, Erweitern
/// und Verkaufen an. Die eigentliche Logik liegt im AnwesenManager der Lib.
/// </summary>
public partial class HausVerwaltungDialog : Control
{
	[Export]
	public NodePath LabelTextPath { get; set; }

	[Export]
	public NodePath HausBildPath { get; set; }

	[Export]
	public NodePath LinkBauenPath { get; set; }

	[Export]
	public NodePath LinkRenovierenPath { get; set; }

	[Export]
	public NodePath LinkErweiternPath { get; set; }

	[Export]
	public NodePath LinkVerkaufenPath { get; set; }

	[Export]
	public NodePath LabelZustandPath { get; set; }

	private Label _labelText;
	private TextureRect _hausBild;
	private controls.LinkButtonWithSounds _linkBauen;
	private controls.LinkButtonWithSounds _linkRenovieren;
	private controls.LinkButtonWithSounds _linkErweitern;
	private controls.LinkButtonWithSounds _linkVerkaufen;
	private Label _labelZustand;

	private Texture2D _bildNichtVorhanden;
	private Texture2D _bildImBau;
	private Texture2D _bildHaus;

	private Main _main;
	private AnwesenManager _anwesenManager;
	private int _stadtId;
	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_hausBild = GetNode<TextureRect>(HausBildPath);
		_linkBauen = GetNode<controls.LinkButtonWithSounds>(LinkBauenPath);
		_linkRenovieren = GetNode<controls.LinkButtonWithSounds>(LinkRenovierenPath);
		_linkErweitern = GetNode<controls.LinkButtonWithSounds>(LinkErweiternPath);
		_linkVerkaufen = GetNode<controls.LinkButtonWithSounds>(LinkVerkaufenPath);
		_labelZustand = GetNode<Label>(LabelZustandPath);

		_bildNichtVorhanden = GD.Load<Texture2D>("res://assets/images/anwesen/AnwNV.png");
		_bildImBau = GD.Load<Texture2D>("res://assets/images/anwesen/AnwImBau.png");
		_bildHaus = GD.Load<Texture2D>("res://assets/images/anwesen/AnwHaus1.png");

		_linkBauen.Pressed += OnBauenPressed;
		_linkRenovieren.Pressed += OnRenovierenPressed;
		_linkErweitern.Pressed += OnErweiternPressed;
		_linkVerkaufen.Pressed += OnVerkaufenPressed;

		_main = GetParent<Main>();

		HideAndDisableInput();
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>
	/// Öffnet die Verwaltung des Wohnsitzes der angegebenen Stadt.
	/// </summary>
	public async Task ShowDialog(int stadtId)
	{
		_anwesenManager = new AnwesenManager();
		_stadtId = stadtId;

		Refresh();

		Show();
		SetProcessInput(true);
		await CloseDialogTask();
	}

	private void Refresh()
	{
		bool hatHaus = _anwesenManager.HatHaus(_stadtId);
		bool istFertig = hatHaus && _anwesenManager.IstFertig(_stadtId);

		_linkBauen.Text = hatHaus ? "Umbauen" : "Bauen";

		// Renovieren, Erweitern und Verkaufen nur bei fertigem Wohnsitz
		_linkRenovieren.Visible = istFertig;
		_linkErweitern.Visible = istFertig;
		_linkVerkaufen.Visible = istFertig;
		_labelZustand.Visible = istFertig;

		if (!hatHaus)
		{
			_labelText.Text = "Ihr besitzt hier keinen Wohnsitz";
			_hausBild.Texture = _bildNichtVorhanden;
		}
		else if (istFertig)
		{
			_labelText.Text = _anwesenManager.GetNameInklPronomen(_stadtId);
			_hausBild.Texture = _bildHaus;
			_labelZustand.Text = "Zustand: " + _anwesenManager.GetZustandInProzent(_stadtId) + " %";
		}
		else
		{
			_labelText.Text = _anwesenManager.GetNameInklPronomen(_stadtId, false) + " wird erst errichtet";
			_hausBild.Texture = _bildImBau;
		}
	}

	private async void OnBauenPressed()
	{
		SetProcessInput(false);

		if (!_anwesenManager.IstFertig(_stadtId) && _anwesenManager.HatHaus(_stadtId))
		{
			// Wohnsitz wird gerade errichtet oder umgebaut
			await SW.UI.ShowText.ShowDialog("Euer Wohnsitz wird bereits umgebaut");
		}
		else
		{
			// modus 0 = neu bauen, 1 = umbauen (falls schon ein fertiger Wohnsitz existiert)
			int modus = _anwesenManager.HatHaus(_stadtId) ? 1 : 0;
			await _main.HausWaehlenDialog.ShowDialog(_anwesenManager, _stadtId, modus);
			Refresh();
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void OnRenovierenPressed()
	{
		SetProcessInput(false);

		if (!_anwesenManager.BenoetigtRenovierung(_stadtId))
		{
			await SW.UI.ShowText.ShowDialog("Euer Wohnsitz befindet sich in tadellosem Zustand und benötigt keine Renovierungsarbeiten.");
		}
		else if (_anwesenManager.RenovierungBereitsBeauftragt(_stadtId))
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt in diesem Jahr bereits eine Renovierung in Auftrag gegeben.");
		}
		else
		{
			int preis = _anwesenManager.GetRenovierungsPreis(_stadtId);

			if (!_anwesenManager.KannBezahlen(preis))
			{
				await SW.UI.ShowText.ShowDialog("Die " + preis.ToStringGeld(false) + " Taler für dieses Vorhaben besitzt Ihr nicht.");
			}
			else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euren Wohnsitz wirklich\nfür " + preis.ToStringGeld() + " renovieren lassen?", "Ja", "Lieber nicht!") == DialogResultGame.Yes)
			{
				_anwesenManager.Renoviere(_stadtId);
				SoundManager.Instance.PlayCoins();
				await SW.UI.ShowText.ShowDialog("Ihr lasst Handwerker kommen! Im nächsten Jahr ist die Renovierung abgeschlossen.");
			}
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void OnErweiternPressed()
	{
		SetProcessInput(false);

		if (_anwesenManager.HatVerfuegbareErweiterungen(_stadtId))
		{
			await _main.HausErweiterungenDialog.ShowDialog(_anwesenManager, _stadtId);
			Refresh();
		}
		else
		{
			await SW.UI.ShowText.ShowDialog("Ihr besitzt bereits alle möglichen Erweiterungen für diesen Wohnsitz.");
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void OnVerkaufenPressed()
	{
		SetProcessInput(false);

		if (!_anwesenManager.KannVerkaufen(_stadtId))
		{
			await SW.UI.ShowText.ShowDialog("Ihr könnt diesen Wohnsitz nicht verkaufen, da Ihr noch Werkstätten in " +
			                                SW.Dynamisch.GetStadtwithID(_stadtId).GetGebietsName() + " besitzt");
		}
		else
		{
			int wert = _anwesenManager.GetVerkaufswert(_stadtId);

			if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr Euren Wohnsitz wirklich\nfür " + wert.ToStringGeld() + " verkaufen?", "Ja", "Lieber nicht!") == DialogResultGame.Yes)
			{
				_anwesenManager.VerkaufeHaus(_stadtId);
				SoundManager.Instance.PlayCoins();
				await SW.UI.ShowText.ShowDialog("Ihr habt Euren Wohnsitz für " + wert.ToStringGeld() + " verkauft!");
				Refresh();
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
