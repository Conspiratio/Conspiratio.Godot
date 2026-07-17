using System;
using Conspiratio.Godot.assets.scripts.managers;
using Godot;

namespace Conspiratio.Godot.assets.scripts.controls;

/// <summary>
/// Portierung des WinForms-NumericButtons: ein flacher Button, der seinen Wert anzeigt und
/// per Klick verändert — obere Hälfte erhöht, untere Hälfte verringert, die X-Position des
/// Klicks bestimmt die Zehnerpotenz (Klick auf die führende Stelle ändert um den größten Schritt).
/// </summary>
[GlobalClass]
public partial class NumericButtonWithSounds : Button
{
	[Signal]
	public delegate void WertChangedEventHandler(int neuerWert);

	private const float OriginalSchriftgroesse = 16;  // Arial 15.75 im Original bestimmt die Ziffernbreite

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

		if (NurEinserSchritte || position.X >= 137)
			return erhoehen;

		if (position.X < 25)
		{
			if (_wert >= 10)
				erhoehen *= Zehnerpotenz(_maximaleStellen - 1);

			return erhoehen;
		}

		for (int i = 1; i <= 7; i++)
		{
			if (position.X < 25 + (OriginalSchriftgroesse - 2) * i)
			{
				if (_maximaleStellen - 1 - i > 0)
					erhoehen *= Zehnerpotenz(_maximaleStellen - 1 - i);

				return erhoehen;
			}
		}

		return erhoehen;
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
