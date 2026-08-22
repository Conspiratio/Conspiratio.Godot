using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Kampf;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Söldner-&-Räuber-Karte (Migration von frmSoeldnerRaeuberKarte): zeigt auf der Königreichskarte
/// die acht Stützpunkte (vier Zollburgen und vier Räuberlager). Beim Überfahren wird der Stützpunkt
/// mit einem goldenen Rahmen markiert; an eigenen Stützpunkten weht die Flagge des Spielers. Ein Klick
/// auf einen fremden Stützpunkt öffnet den Kauf-Dialog, ein Klick auf einen eigenen dessen Verwaltung.
/// </summary>
public partial class SoeldnerRaeuberKarte : Control
{
	// Die Stützpunkt-Rechtecke des Originals sind in 2560×1440 angelegt.
	private const float ScaleX = 1600f / 2560f;
	private const float ScaleY = 900f / 1440f;

	// Räuberlager-Platzhalter-Icons (wie im Original) an den Stützpunkten, die im Kartenbild kein
	// eigenes Symbol tragen.
	//
	// Offsets und Größe liegen im selben Koordinatenraum wie die Rechtecke und werden deshalb mit
	// ScaleX/ScaleY umgerechnet: Das Original addiert sie auf die bereits normierten Rechteckkanten
	// (`stuetzpunkt_platzhalter1.Left = s3_left + 45`), sie sind also keine eigene Größe. Zuvor stand
	// hier 1600/1366 – das Icon rutschte dadurch gegenüber seinem Klickrechteck nach rechts unten, und
	// wer das Symbol anklickte, traf daneben.
	private const float PlatzhalterFaktorX = ScaleX;
	private const float PlatzhalterFaktorY = ScaleY;

	private static readonly (int Id, float OffsetX, float OffsetY)[] PlatzhalterDaten =
	{
		(3, 45, 30), (5, 40, 30), (6, 40, 30), (7, 40, 30), (8, 30, 23)
	};

	private TextureRect _background;
	private Panel _hoverRect;
	private Rect2[] _rechtecke;
	private TextureRect[] _flaggen;
	private TextureRect[] _platzhalter;

	private int _hoverStuetzpunkt;
	private readonly SoeldnerRaeuberManager _manager = new SoeldnerRaeuberManager();
	private Main _main;

	public override void _Ready()
	{
		_background = GetNode<TextureRect>("TextureRect");
		_hoverRect = GetNode<Panel>("HoverRect");

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

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_next_or_close"))
		{
			SoundManager.Instance.PlayRightClick();
			Schliessen();
			return;
		}

		if (@event is InputEventMouseMotion motion)
			HoverAktualisieren(motion.GlobalPosition);
		else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _hoverStuetzpunkt != 0)
			StuetzpunktAngeklickt(_hoverStuetzpunkt);
	}

	/// <summary>Öffnet die Söldner-&-Räuber-Karte (aus dem Kontor).</summary>
	public void ZeigeKarte()
	{
		BerechneRechtecke();
		PlatzhalterEinblenden();
		FlaggenAktualisieren();

		_hoverStuetzpunkt = 0;
		_hoverRect.Visible = false;

		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Kampf);
		Show();
		SetProcessInput(true);
	}

	private void Schliessen()
	{
		Hide();
		SetProcessInput(false);
		_main.Kontor.ReturnFromStadt();
	}

	/// <summary>
	/// Wählt einen Stützpunkt so aus, als hätte der Spieler ihn angeklickt. Die Karte wertet sonst nur
	/// echte Mausereignisse aus – die erreichen sie headless nicht, weshalb der automatische
	/// Spieldurchlauf diesen Einstieg braucht, um Kauf und Verwaltung überhaupt zu erreichen.
	/// </summary>
	public void WaehleStuetzpunkt(int stuetzpunktId) => StuetzpunktAngeklickt(stuetzpunktId);

	private async void StuetzpunktAngeklickt(int stuetzpunktId)
	{
		SoundManager.Instance.PlayLeftClick();
		SetProcessInput(false);

		if (_manager.GehoertAktivemSpieler(stuetzpunktId))
		{
			// Eigener Stützpunkt: die Verwaltung öffnen (die Karte wird ausgeblendet).
			Hide();
			_main.StuetzpunktVerwalten.ZeigeVerwaltung(stuetzpunktId);
			return;
		}

		await _main.StuetzpunktKaufenDialog.ShowDialog(stuetzpunktId);
		FlaggenAktualisieren();

		if (Visible)
			SetProcessInput(true);
	}

	/// <summary>Kehrt aus der Stützpunkt-Verwaltung zur Karte zurück.</summary>
	public void ReturnFromVerwaltung()
	{
		FlaggenAktualisieren();
		Show();
		SetProcessInput(true);
	}

	private void BerechneRechtecke()
	{
		_rechtecke = new Rect2[_manager.Anzahl + 1];

		for (int id = 1; id <= _manager.Anzahl; id++)
		{
			float links = _manager.GetRechteck(id, 0) * ScaleX;
			float rechts = _manager.GetRechteck(id, 1) * ScaleX;
			float oben = _manager.GetRechteck(id, 2) * ScaleY;
			float unten = _manager.GetRechteck(id, 3) * ScaleY;

			_rechtecke[id] = new Rect2(links, oben, rechts - links, unten - oben);
		}
	}

	private void PlatzhalterEinblenden()
	{
		if (_platzhalter == null)
		{
			var textur = GD.Load<Texture2D>("res://assets/images/symbole/RaeuberlagerPlatzhalter.png");
			_platzhalter = new TextureRect[PlatzhalterDaten.Length];

			for (int i = 0; i < PlatzhalterDaten.Length; i++)
			{
				var platzhalter = new TextureRect
				{
					Texture = textur,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.Scale,
					MouseFilter = MouseFilterEnum.Ignore,
					Size = new Vector2(40 * PlatzhalterFaktorX, 44 * PlatzhalterFaktorY)
				};

				_platzhalter[i] = platzhalter;
				AddChild(platzhalter);
			}
		}

		for (int i = 0; i < PlatzhalterDaten.Length; i++)
		{
			var daten = PlatzhalterDaten[i];
			var rechteck = _rechtecke[daten.Id];
			_platzhalter[i].Position = rechteck.Position
				+ new Vector2(daten.OffsetX * PlatzhalterFaktorX, daten.OffsetY * PlatzhalterFaktorY);
		}
	}

	private void FlaggenAktualisieren()
	{
		if (_flaggen == null)
		{
			_flaggen = new TextureRect[_manager.Anzahl + 1];

			for (int id = 1; id <= _manager.Anzahl; id++)
			{
				var flagge = new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.Scale,
					MouseFilter = MouseFilterEnum.Ignore,
					Visible = false
				};

				_flaggen[id] = flagge;
				AddChild(flagge);
			}
		}

		for (int id = 1; id <= _manager.Anzahl; id++)
		{
			bool eigen = _manager.GehoertMenschlichemSpieler(id);
			_flaggen[id].Visible = eigen;

			if (eigen)
			{
				_flaggen[id].Texture = GD.Load<Texture2D>("res://assets/images/banner/ban" + _manager.GetBesitzerBanner(id) + ".png");
				_flaggen[id].Position = new Vector2(_rechtecke[id].End.X + 3, _rechtecke[id].Position.Y);
				_flaggen[id].Size = new Vector2(25, 35);
			}
		}
	}

	/// <summary>
	/// Der Mittelpunkt eines Stützpunkts auf der Karte, und wie viele es gibt. Die Karte wird über die
	/// Mausposition bedient, nicht über Knöpfe – der automatische Spieldurchlauf braucht diesen Punkt,
	/// um einen Stützpunkt anzuklicken. Liefert <see cref="Vector2.Zero"/>, solange nichts berechnet ist.
	/// </summary>
	public int AnzahlStuetzpunkte => _manager?.Anzahl ?? 0;

	public Vector2 GetStuetzpunktMitte(int stuetzpunktId)
	{
		if (_rechtecke == null || stuetzpunktId < 0 || stuetzpunktId >= _rechtecke.Length)
			return Vector2.Zero;

		return _rechtecke[stuetzpunktId].GetCenter();
	}

	private void HoverAktualisieren(Vector2 position)
	{
		int neuer = 0;

		for (int id = 1; id <= _manager.Anzahl; id++)
		{
			if (_rechtecke[id].HasPoint(position))
			{
				neuer = id;
				break;
			}
		}

		if (neuer == _hoverStuetzpunkt)
			return;

		_hoverStuetzpunkt = neuer;

		if (_hoverStuetzpunkt != 0)
		{
			_hoverRect.Position = _rechtecke[_hoverStuetzpunkt].Position;
			_hoverRect.Size = _rechtecke[_hoverStuetzpunkt].Size;
			_hoverRect.Visible = true;
		}
		else
		{
			_hoverRect.Visible = false;
		}
	}
}
