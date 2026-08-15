using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Privilegien;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die politische Weltkarte nach der WinForms-Vorlage: Städte werden beim Überfahren mit einem
/// goldenen Rahmen markiert (die Rechtecke kommen aus der Lib), außerhalb der Städte werden
/// Länder bzw. das Reich über Kartenvarianten hervorgehoben. An Städten mit eigenem Haus oder
/// eigener Werkstätte weht das Banner des Spielers.
/// </summary>
public partial class Weltkarte : Control, IPolitischeWeltkarteDialog
{
	private const float ScaleX = 1600f / 1366f;
	private const float ScaleY = 900f / 768f;

	private TextureRect _background;
	private Panel _hoverRect;
	private Texture2D _karteStandard;
	private readonly Texture2D[] _kartenLaender = new Texture2D[5];  // 1..4 = Länder
	private Texture2D _karteReich;

	private Rect2[] _stadtRechtecke;
	private TextureRect[] _flaggen;

	private int _hoverStadt;
	private int _hoverRegion;
	private bool _nurStaedteMarkieren = true;
	private bool _handelsModus;
	private bool _preisModus;
	private int _preisLevel;
	private bool _personenModus;
	private int _personenModusModus;
	private controls.ButtonWithSounds _listeButton;
	private TaskCompletionSource<int> _stadtWahl;
	private TaskCompletionSource<bool> _personenKarte;

	private readonly HandelsManager _handelsManager = new HandelsManager();
	private Main _main;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_background = GetNode<TextureRect>("TextureRect");
		_hoverRect = GetNode<Panel>("HoverRect");

		_karteStandard = GD.Load<Texture2D>("res://assets/images/landkarten/PWK.png");

		for (int i = 1; i <= 4; i++)
			_kartenLaender[i] = GD.Load<Texture2D>("res://assets/images/landkarten/PWK-10" + i + ".png");

		_karteReich = GD.Load<Texture2D>("res://assets/images/landkarten/PWK-201.png");

		// Goldener Rahmen für die überfahrene Stadt
		var rahmen = new StyleBoxFlat
		{
			DrawCenter = false,
			BorderColor = Colors.Gold,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2
		};
		_hoverRect.AddThemeStyleboxOverride("panel", rahmen);
		_hoverRect.MouseFilter = MouseFilterEnum.Ignore;
		_hoverRect.Visible = false;

		_listeButton = GetNode<controls.ButtonWithSounds>("ListeButton");
		_listeButton.Pressed += OnListePressed;
		_listeButton.Visible = false;

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public override void _Input(InputEvent @event)
	{
		// Rechtsklick auf eine Stadt in der Handelsansicht öffnet ihre Stadtinformationen (wie im Original);
		// nur außerhalb einer Stadt schließt der Rechtsklick die Karte.
		if (_handelsModus && _hoverStadt != 0 &&
		    @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			GetViewport().SetInputAsHandled();
			_ = ZeigeStadtinformationen(_hoverStadt);
			return;
		}

		if (Input.IsActionPressed("ui_next_or_close"))
		{
			SoundManager.Instance.PlayRightClick();
			Schliessen();
			return;
		}

		if (@event is InputEventMouseMotion motion)
		{
			HoverAktualisieren(motion.GlobalPosition);
		}
		else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick)
		{
			// Der Kontrahenten-Listen-Button verarbeitet seinen Klick selbst.
			if (_personenModus && _listeButton.Visible && _listeButton.GetGlobalRect().HasPoint(klick.GlobalPosition))
				return;

			if (_hoverStadt != 0)
				StadtAngeklickt(_hoverStadt);
			else if (_personenModus && _hoverRegion != 0)
				RegionAngeklickt(_hoverRegion);
		}
	}

	#region Öffnen und Schließen

	/// <summary>
	/// Öffnet die Karte als Handelsansicht: Klick auf eine Stadt öffnet ihre Stadtansicht,
	/// die eigenen Banner zeigen Häuser und Werkstätten.
	/// </summary>
	public void ZeigeHandelskarte()
	{
		_handelsModus = true;
		_preisModus = false;
		_personenModus = false;
		_stadtWahl = null;
		_nurStaedteMarkieren = true;

		OeffneKarte(true);
	}

	/// <summary>
	/// Öffnet die Karte zur Stadtwahl (Modus 6 des Originals).
	/// </summary>
	/// <returns>Die gewählte Stadt-ID oder 0 bei Abbruch.</returns>
	public Task<int> WaehleStadt()
	{
		_handelsModus = false;
		_preisModus = false;
		_personenModus = false;
		_stadtWahl = new TaskCompletionSource<int>();
		_nurStaedteMarkieren = true;

		OeffneKarte(false);
		return _stadtWahl.Task;
	}

	/// <summary>
	/// Anbindung für die Lib (Privilegien-Modi der Weltkarte).
	/// </summary>
	public void ShowDialogModus(int mod, bool flaggenEinblenden = false)
	{
		// Preisansichten der Privilegien: Händler (9), Kaufmann (10), Großkaufmann (11).
		// Im Original wird für Kaufmann/Großkaufmann modus auf 9 gesetzt und Level = modus - 9.
		if (mod == 9 || mod == 10 || mod == 11)
		{
			_preisLevel = mod == 9 ? 0 : mod - 9;
			_preisModus = true;
			_handelsModus = false;
			_stadtWahl = null;
			_nurStaedteMarkieren = true;

			OeffneKarte(false);
			return;
		}

		// Personen-Ziel-Modi: Privilegien (Prozess = 8, Vergifteter Wein = 12, Henkershand = 13,
		// Duell = 14) und Hinterzimmer (Beziehungen = 0, Sabotage = 1, Anschwärzen = 2, Spionage = 3,
		// Ermordung = 4, Erpressung = 5). Über die Interface-Methode (Privilegien) wird die Karte im
		// Hintergrund geöffnet (fire-and-forget).
		if (mod >= 0 && mod <= 5 || mod == 8 || mod == 12 || mod == 13 || mod == 14)
		{
			_ = OeffnePersonenKarteIntern(mod, flaggenEinblenden);
			return;
		}

		// Übrige (noch nicht migrierte) Modi.
		_ = SW.UI.ShowText.ShowDialog("Wurde noch nicht implementiert");
	}

	/// <summary>
	/// Öffnet die Karte in einem Personen-Ziel-Modus und liefert einen Task, der beim Schließen der
	/// Karte abgeschlossen wird (für das Hinterzimmer, das danach wieder erscheinen soll).
	/// </summary>
	public Task OeffnePersonenKarte(int modus, bool flaggenEinblenden = false)
	{
		return OeffnePersonenKarteIntern(modus, flaggenEinblenden);
	}

	private Task OeffnePersonenKarteIntern(int modus, bool flaggenEinblenden)
	{
		_personenModus = true;
		_personenModusModus = modus;
		_handelsModus = false;
		_preisModus = false;
		_stadtWahl = null;
		_nurStaedteMarkieren = false; // Länder und Reich sind ebenfalls anklickbar

		_personenKarte = new TaskCompletionSource<bool>();
		OeffneKarte(flaggenEinblenden);
		return _personenKarte.Task;
	}

	private void OeffneKarte(bool flaggenEinblenden)
	{
		BerechneStadtRechtecke();
		FlaggenAktualisieren(flaggenEinblenden);

		_background.Texture = _karteStandard;
		_hoverStadt = 0;
		_hoverRegion = 0;
		_hoverRect.Visible = false;

		// Die Kontrahenten-Liste steht nur in den Personen-Ziel-Modi zur Verfügung.
		_listeButton.Visible = _personenModus;

		Show();
		SetProcessInput(true);
	}

	private void Schliessen()
	{
		Hide();
		SetProcessInput(false);

		if (_stadtWahl != null)
		{
			_stadtWahl.TrySetResult(0);
			_stadtWahl = null;
			return;
		}

		// Im Preis- und Personen-Modus wurde die Karte über ein Privileg geöffnet – zurück zum
		// darunterliegenden Privilegien-/Schreibstube-Kontext, nicht ins Kontor.
		if (_preisModus)
		{
			_preisModus = false;
			return;
		}

		if (_personenModus)
		{
			_personenModus = false;
			_listeButton.Visible = false;

			// Einen offenen Anschwärz-Vorgang beim Schließen der Karte verwerfen (wie im Original).
			SW.Dynamisch.SetAnschwaerzID(0);

			_personenKarte?.TrySetResult(true);
			_personenKarte = null;
			return;
		}

		_main.Kontor.ReturnFromStadt();
	}

	private async void StadtAngeklickt(int stadtId)
	{
		SoundManager.Instance.PlayLeftClick();

		// Preismodus: Stadt anklicken öffnet die Rohstoffpreis-Ansicht; die Karte bleibt offen,
		// damit weitere Städte betrachtet werden können (Rechtsklick schließt die Karte).
		if (_preisModus)
		{
			SetProcessInput(false);
			await _main.RohstoffpreiseDialog.ShowDialog(stadtId, _preisLevel);

			if (Visible)
				SetProcessInput(true);
			return;
		}

		// Personen-Modus: Stadt anklicken öffnet die städtische Ämter-Ebene (Stufe 0).
		if (_personenModus)
		{
			SetProcessInput(false);
			await _main.AemterEbeneDialog.ShowDialog(stadtId, 0, _personenModusModus);
			NachAemterEbene();
			return;
		}

		Hide();
		SetProcessInput(false);

		if (_stadtWahl != null)
		{
			_stadtWahl.TrySetResult(stadtId);
			_stadtWahl = null;
			return;
		}

		if (_handelsModus)
			_main.Stadt.ShowStadt(stadtId);
	}

	// Öffnet die Stadtinformationen über der geöffneten Handelskarte; danach bleibt die Karte offen.
	private async Task ZeigeStadtinformationen(int stadtId)
	{
		SoundManager.Instance.PlayRightClick();

		SetProcessInput(false);
		await _main.StadtInformationenDialog.ShowDialog(stadtId);

		if (Visible)
			SetProcessInput(true);
	}

	/// <summary>
	/// Personen-Modus: Klick auf eine Länderregion (101–104) öffnet die Ämter-Ebene des Landes
	/// (Stufe 1), ein Klick auf das restliche Reich (201) die Ämter-Ebene des Reichs (Stufe 2).
	/// </summary>
	private async void RegionAngeklickt(int region)
	{
		SoundManager.Instance.PlayLeftClick();
		SetProcessInput(false);

		if (region >= 101 && region <= 104)
			await _main.AemterEbeneDialog.ShowDialog(region - 100, 1, _personenModusModus);
		else if (region == 201)
			await _main.AemterEbeneDialog.ShowDialog(1, 2, _personenModusModus);

		NachAemterEbene();
	}

	/// <summary>
	/// Verhalten nach dem Schließen der Ämter-Ebene: Bei den Privilegien (Prozess = 8, Vergifteter
	/// Wein = 12, Henkershand = 13, Duell = 14) schließt sich – wie im Original – auch die Karte; bei den
	/// Hinterzimmer-Modi bleibt sie offen.
	/// </summary>
	private void NachAemterEbene()
	{
		if (_personenModusModus == 5 || _personenModusModus == 8 || _personenModusModus == 12 || _personenModusModus == 13 || _personenModusModus == 14)
			Schliessen();
		else if (Visible)
			SetProcessInput(true);
	}

	private async void OnListePressed()
	{
		SetProcessInput(false);
		await _main.KontrahentenDialog.ShowDialog(_personenModusModus);

		if (Visible)
			SetProcessInput(true);
	}

	#endregion

	#region Stadtrechtecke und Flaggen

	private void BerechneStadtRechtecke()
	{
		_stadtRechtecke = new Rect2[SW.Statisch.GetMaxStadtID()];

		for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
		{
			float links = SW.Statisch.GetStadtRechteck(stadtId, 0) * ScaleX;
			float rechts = SW.Statisch.GetStadtRechteck(stadtId, 1) * ScaleX;
			float oben = SW.Statisch.GetStadtRechteck(stadtId, 2) * ScaleY;
			float unten = SW.Statisch.GetStadtRechteck(stadtId, 3) * ScaleY;

			_stadtRechtecke[stadtId] = new Rect2(links, oben, rechts - links, unten - oben);
		}
	}

	private void FlaggenAktualisieren(bool einblenden)
	{
		if (_flaggen == null)
		{
			_flaggen = new TextureRect[SW.Statisch.GetMaxStadtID()];

			for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
			{
				var flagge = new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.Scale,
					MouseFilter = MouseFilterEnum.Ignore,
					Visible = false
				};

				_flaggen[stadtId] = flagge;
				AddChild(flagge);
			}
		}

		var spieler = SW.Dynamisch.GetAktHum();
		var bannerTextur = GD.Load<Texture2D>("res://assets/images/banner/ban" + spieler.GetBanner() + ".png");

		for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
		{
			// Die Flagge weht, wenn der Spieler in der Stadt eine Präsenz hat (Haus oder Werkstätte mit Lager)
			bool flaggeZeigen = einblenden && _handelsManager.HatPraesenzInStadt(stadtId);

			_flaggen[stadtId].Visible = flaggeZeigen;

			if (flaggeZeigen)
			{
				_flaggen[stadtId].Texture = bannerTextur;
				_flaggen[stadtId].Position = new Vector2(_stadtRechtecke[stadtId].End.X + 4, _stadtRechtecke[stadtId].Position.Y);
				_flaggen[stadtId].Size = new Vector2(29, 41);
			}
		}
	}

	#endregion

	#region Hover

	/// <summary>
	/// Der Mittelpunkt einer Stadt auf der Karte. Die Karte wird über Mausposition bedient, nicht über
	/// Knöpfe – der automatische Spieldurchlauf braucht diesen Punkt, um eine Stadt anzuklicken.
	/// Liefert <see cref="Vector2.Zero"/>, solange die Rechtecke noch nicht berechnet sind.
	/// </summary>
	public Vector2 GetStadtMitte(int stadtId)
	{
		if (_stadtRechtecke == null || stadtId < 0 || stadtId >= _stadtRechtecke.Length)
			return Vector2.Zero;

		return _stadtRechtecke[stadtId].GetCenter();
	}

	private void HoverAktualisieren(Vector2 position)
	{
		int neueStadt = 0;

		for (int stadtId = SW.Statisch.GetMinStadtID(); stadtId < SW.Statisch.GetMaxStadtID(); stadtId++)
		{
			if (_stadtRechtecke[stadtId].HasPoint(position))
			{
				neueStadt = stadtId;
				break;
			}
		}

		int neueRegion = 0;

		if (neueStadt == 0 && !_nurStaedteMarkieren)
			neueRegion = ErmittleRegion(position);

		if (neueStadt == _hoverStadt && neueRegion == _hoverRegion)
			return;

		_hoverStadt = neueStadt;
		_hoverRegion = neueRegion;

		if (_hoverStadt != 0)
		{
			_hoverRect.Position = _stadtRechtecke[_hoverStadt].Position;
			_hoverRect.Size = _stadtRechtecke[_hoverStadt].Size;
			_hoverRect.Visible = true;
		}
		else
		{
			_hoverRect.Visible = false;
		}

		if (_hoverRegion >= 101 && _hoverRegion <= 104)
			_background.Texture = _kartenLaender[_hoverRegion - 100];
		else if (_hoverRegion == 201)
			_background.Texture = _karteReich;
		else
			_background.Texture = _karteStandard;
	}

	/// <summary>
	/// Ermittelt die Länderregion unter dem Mauszeiger (Zonen aus dem Original, skaliert auf 1600×900).
	/// </summary>
	private static int ErmittleRegion(Vector2 position)
	{
		float x = position.X;
		float y = position.Y;

		if (x < 1010 && y < 327)
			return 101;

		if (x >= 1011 && y > 88 && y < 420)
			return 102;

		if ((x < 1010 && y > 327 && y < 606) || (x < 176 && y > 607))
			return 103;

		if ((x > 417 && x < 1117 && y > 648) || (x > 1116 && y > 548))
			return 104;

		return 201;
	}

	#endregion
}
