using System;
using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts.controls;

/// <summary>
/// Portierung des WinForms-NumericButtons: ein flacher Button, der seinen Wert anzeigt und
/// per Klick verändert — obere Hälfte erhöht, untere Hälfte verringert, die X-Position des
/// Klicks bestimmt die Zehnerpotenz (Klick auf die führende Stelle ändert um den größten Schritt).
///
/// <b>Die Trefferzonen werden aus den echten Schriftmaßen berechnet, nicht aus festen Pixeln.</b>
/// Das Original rechnete mit den Maßen seiner Arial 15.75 (Textbeginn bei 25 px, je Ziffer
/// <c>Schriftgröße − 2</c> = 13,75 px). Das Theme dieses Clients zeichnet den Button aber mit
/// Schriftgröße 32: Die Zonen waren damit etwa halb so breit wie die Ziffern, und ein Klick traf
/// regelmäßig eine andere Stelle als die, auf der der Zeiger stand. Gemessen wird jetzt die
/// tatsächlich gezeichnete Zeichenkette – das deckt zugleich den Fall ohne Tausendertrenner ab, wo
/// <see cref="FormatiereWert"/> nicht mit Nullen auffüllt und der Text darum kürzer ist als das
/// unterstellte Stellenfeld.
/// </summary>
[GlobalClass]
public partial class NumericButtonWithSounds : Button
{
	[Signal]
	public delegate void WertChangedEventHandler(int neuerWert);

	private int _wert;
	private int _maximalerWert = 999999999;
	private int _minimalerWert;
	private int _maximaleStellen = 9;
	private bool _tausenderTrenner = true;
	private bool _wertAnzeigen = true;

	private Resource _cursorPlus;
	private Resource _cursorMinus;
	private Resource _cursorDefault;

	public bool NurEinserSchritte { get; set; }

	public int Wert
	{
		get => _wert;
		set
		{
			_wert = value;

			if (_wert < 0)
				_wert = 0;

			if (_wert > _maximalerWert)
				_wert = _maximalerWert;

			if (_wert < _minimalerWert)
				_wert = _minimalerWert;

			if (_wertAnzeigen)
				Text = FormatiereWert(_wert);
		}
	}

	public int MaximaleStellen
	{
		get => _maximaleStellen;
		set
		{
			_maximaleStellen = value;
			Wert = _wert;
		}
	}

	public int MaximalerWert
	{
		get => _maximalerWert;
		set
		{
			_maximalerWert = value;
			Wert = _wert;
		}
	}

	public int MinimalerWert
	{
		get => _minimalerWert;
		set
		{
			_minimalerWert = value;
			Wert = _wert;
		}
	}

	public bool TausenderTrenner
	{
		get => _tausenderTrenner;
		set
		{
			_tausenderTrenner = value;
			Wert = _wert;
		}
	}

	public bool WertAnzeigen
	{
		get => _wertAnzeigen;
		set
		{
			_wertAnzeigen = value;

			if (_wertAnzeigen)
				Wert = _wert;
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Flat = true;
		Alignment = HorizontalAlignment.Left;
		AddThemeColorOverride("font_hover_color", Colors.Red);

		_cursorPlus = ResourceLoader.Load("res://assets/cursor/CurPlus-32x32x24.png");
		_cursorMinus = ResourceLoader.Load("res://assets/cursor/CurMinus-32x32x24.png");
		_cursorDefault = ResourceLoader.Load("res://assets/cursor/CurSword-32x32x24.png");

		MouseExited += () => Input.SetCustomMouseCursor(_cursorDefault);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
			Input.SetCustomMouseCursor(motion.Position.Y > Size.Y / 2 ? _cursorMinus : _cursorPlus);

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick)
		{
			SoundManager.Instance.PlayLeftClick();

			Wert += ErmittleErhoehung(klick.Position);
			EmitSignal(SignalName.WertChanged, Wert);
			AcceptEvent();
		}
	}

	private int ErmittleErhoehung(Vector2 position)
	{
		int erhoehen = position.Y > Size.Y / 2 ? -1 : 1;

		if (NurEinserSchritte)
			return erhoehen;

		return erhoehen * Zehnerpotenz(ErmittleStelleUnterCursor(position.X));
	}

	/// <summary>
	/// Welche Dezimalstelle liegt unter der X-Position? 0 = Einer, 1 = Zehner und so fort.
	///
	/// Die Zeichen werden von links durchgegangen und jeweils gefragt, bis wohin der Text bis
	/// einschließlich dieses Zeichens reicht; getroffen ist das erste, dessen Ende rechts vom Zeiger
	/// liegt. Kumulativ gemessen statt Zeichen für Zeichen, weil eine Proportionalschrift
	/// unterschneidet und sich die Einzelbreiten sonst zu etwas anderem aufsummieren als der
	/// gezeichnete Text breit ist.
	///
	/// Ein Tausenderpunkt braucht keine Sonderbehandlung: Rechts von ihm stehen genauso viele Ziffern
	/// wie rechts der Ziffer links von ihm, er liefert also dieselbe Stelle.
	/// </summary>
	private int ErmittleStelleUnterCursor(float x)
	{
		string text = Text;

		if (string.IsNullOrEmpty(text))
			return 0;

		var schrift = GetThemeFont("font");

		if (schrift == null)
			return 0;

		int schriftgroesse = GetThemeFontSize("font_size");

		// Der Text beginnt nicht am Rand des Knopfes, sondern hinter dem Innenabstand der StyleBox.
		var rahmen = GetThemeStylebox("normal");
		float textBeginn = rahmen?.GetOffset().X ?? 0;

		for (int i = 0; i < text.Length; i++)
		{
			float bisHierher = schrift.GetStringSize(text.Substring(0, i + 1), HorizontalAlignment.Left, -1,
													 schriftgroesse).X;

			if (x < textBeginn + bisHierher)
				return Math.Min(ZaehleZiffernNach(text, i), _maximaleStellen - 1);
		}

		// Rechts neben dem Text: die Einerstelle, wie überall sonst der kleinste Schritt.
		return 0;
	}

	/// <summary>Wie viele Ziffern stehen rechts von Position <paramref name="index"/>?</summary>
	private static int ZaehleZiffernNach(string text, int index)
	{
		int ziffern = 0;

		for (int i = index + 1; i < text.Length; i++)
			if (char.IsDigit(text[i]))
				ziffern++;

		return ziffern;
	}

	private static int Zehnerpotenz(int stellen)
	{
		return Convert.ToInt32(Math.Pow(10, stellen));
	}

	private string FormatiereWert(int wert)
	{
		string text = wert.ToString();

		if (!_tausenderTrenner)
			return text;

		// Mit Nullen auffüllen bis zur maximalen Länge
		while (text.Length < _maximaleStellen)
			text = "0" + text;

		// Tausenderpunkte setzen
		if (text.Length > 6)
			return text.Substring(0, text.Length - 6) + "." + text.Substring(text.Length - 6, 3) + "." + text.Substring(text.Length - 3, 3);

		if (text.Length > 3)
			return text.Substring(0, text.Length - 3) + "." + text.Substring(text.Length - 3, 3);

		return text;
	}
}
