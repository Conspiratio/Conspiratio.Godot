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
public partial class HausVerwaltungDialog : DialogBase
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
	public NodePath LinkBestechenPath { get; set; }

	[Export]
	public NodePath LabelZustandPath { get; set; }

	private Label _labelText;
	private TextureRect _hausBild;
	private controls.LinkButtonWithSounds _linkBauen;
	private controls.LinkButtonWithSounds _linkRenovieren;
	private controls.LinkButtonWithSounds _linkErweitern;
	private controls.LinkButtonWithSounds _linkVerkaufen;
	private controls.LinkButtonWithSounds _linkBestechen;
	private Label _labelZustand;

	private Texture2D _bildNichtVorhanden;
	private Texture2D _bildImBau;
	private Texture2D _bildHaus;

	private Main _main;
	private AnwesenManager _anwesenManager;
	private int _stadtId;

	protected override void OnReady()
	{
		_labelText = GetNode<Label>(LabelTextPath);
		_hausBild = GetNode<TextureRect>(HausBildPath);
		_linkBauen = GetNode<controls.LinkButtonWithSounds>(LinkBauenPath);
		_linkRenovieren = GetNode<controls.LinkButtonWithSounds>(LinkRenovierenPath);
		_linkErweitern = GetNode<controls.LinkButtonWithSounds>(LinkErweiternPath);
		_linkVerkaufen = GetNode<controls.LinkButtonWithSounds>(LinkVerkaufenPath);
		_linkBestechen = GetNode<controls.LinkButtonWithSounds>(LinkBestechenPath);
		_labelZustand = GetNode<Label>(LabelZustandPath);

		_bildNichtVorhanden = GD.Load<Texture2D>("res://assets/images/anwesen/AnwNV.png");
		_bildImBau = GD.Load<Texture2D>("res://assets/images/anwesen/AnwImBau.png");
		_bildHaus = GD.Load<Texture2D>("res://assets/images/anwesen/AnwHaus1.png");

		_linkBauen.Pressed += OnBauenPressed;
		_linkRenovieren.Pressed += OnRenovierenPressed;
		_linkErweitern.Pressed += OnErweiternPressed;
		_linkVerkaufen.Pressed += OnVerkaufenPressed;
		_linkBestechen.Pressed += OnBestechenPressed;

		_main = GetParent<Main>();
	}

	/// <summary>
	/// Öffnet die Verwaltung des Wohnsitzes der angegebenen Stadt.
	/// </summary>
	public async Task ShowDialog(int stadtId)
	{
		_anwesenManager = new AnwesenManager();
		_stadtId = stadtId;

		Refresh();

		await ShowAndAwait();
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

		// Umgekehrt: Den Baumeister bestechen kann man nur, solange gebaut wird und dabei mehr als ein
		// Jahr zu gewinnen ist. Der Knopf teilt sich den Platz mit "Renovieren", das dann ausgeblendet ist.
		_linkBestechen.Visible = hatHaus && !istFertig && _anwesenManager.KannBauBeschleunigen(_stadtId);

		// Der Preis passt nicht mehr in die Knopfbeschriftung (er liefe in das Haus-Bild hinein) und steht
		// deshalb in der Zustandszeile, die waehrend des Baus ohnehin leer bleibt.
		if (_linkBestechen.Visible)
		{
			int gesparteJahre = _anwesenManager.GetRestlicheBauzeit(_stadtId) - 1;

			_labelZustand.Visible = true;
			_labelZustand.Text = "Bestechung: " + _anwesenManager.GetBaubeschleunigungsPreis(_stadtId).ToStringGeld() +
			                     "\n(spart " + gesparteJahre + (gesparteJahre == 1 ? " Jahr)" : " Jahre)");
		}

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
			// Ohne das Fertigstellungsjahr stand nirgends, wie lange der Bau noch dauert - eine Villa
			// braucht sechs Jahre.
			_labelText.Text = _anwesenManager.GetNameInklPronomen(_stadtId, false) + " wird erst errichtet\n" +
			                  "Fertig im Jahr " + _anwesenManager.GetFertigstellungsjahr(_stadtId);
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

	/// <summary>
	/// Kauft dem Baumeister gegen Bestechungsgeld alle Baujahre bis auf das letzte ab. Der Wohnsitz ist
	/// damit nicht sofort fertig, sondern im nächsten Jahr – wie ein regulärer letzter Bauabschnitt.
	/// </summary>
	private async void OnBestechenPressed()
	{
		SetProcessInput(false);

		int preis = _anwesenManager.GetBaubeschleunigungsPreis(_stadtId);
		int jahre = _anwesenManager.GetRestlicheBauzeit(_stadtId) - 1;

		if (!_anwesenManager.KannBezahlen(preis))
		{
			await SW.UI.ShowText.ShowDialog("Die " + preis.ToStringGeld(false) + " Taler für dieses Vorhaben besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText(
			         "Der Baumeister lässt durchblicken, dass sich die Arbeiten\nfür " + preis.ToStringGeld() +
			         " um " + jahre + " Jahre verkürzen ließen.\nWollt Ihr zahlen?",
			         "Ja", "Lieber nicht!") == DialogResultGame.Yes)
		{
			_anwesenManager.BeschleunigeBau(_stadtId);
			SoundManager.Instance.PlayCoins();
			await SW.UI.ShowText.ShowDialog("Ihr drückt dem Baumeister einen Beutel in die Hand. Im nächsten Jahr könnt Ihr einziehen!");
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

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
