using System.Collections.Generic;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Zeigt die Kenndaten einer Stadt (Migration von StadtInformationen): Reichtum, Umsatzsteuer,
/// Einwohner, Kriminalität sowie Haupt-/Nebenproduktion, Nachfrage, mögliche Werkstätten und den
/// Lagerstand des Landes je Rohstoff. Die Icon-Reihen werden – wie im Original – aus den Daten
/// des StadtInformationenManager aufgebaut. Der Rechtsklick schließt die Anzeige.
/// </summary>
public partial class StadtInformationenDialog : Control
{
	// Alle Layout-Koordinaten stammen aus dem WinForms-Original (Entwurf 632×596) und werden
	// mit diesem Faktor auf die feste Godot-Auflösung skaliert (Pergament 885×835).
	private const float S = 1.4f;

	[Export]
	public NodePath TitelPath { get; set; }

	[Export]
	public NodePath UmsatzWertPath { get; set; }

	[Export]
	public NodePath EinwohnerWertPath { get; set; }

	[Export]
	public NodePath IconEbenePath { get; set; }

	private Label _titel;
	private Label _umsatzWert;
	private Label _einwohnerWert;
	private Control _iconEbene;

	private readonly Dictionary<int, Texture2D> _rohstoffIcons = new();
	private Texture2D _symbReichtum;
	private Texture2D _symbCrime;

	private TaskCompletionSource<bool> _dialogClosed;

	public override void _Ready()
	{
		_titel = GetNode<Label>(TitelPath);
		_umsatzWert = GetNode<Label>(UmsatzWertPath);
		_einwohnerWert = GetNode<Label>(EinwohnerWertPath);
		_iconEbene = GetNode<Control>(IconEbenePath);

		_symbReichtum = GD.Load<Texture2D>("res://assets/images/symbole/SymbReichtum.png");
		_symbCrime = GD.Load<Texture2D>("res://assets/images/symbole/SymbCrime.png");

		Hide();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseDialog();
	}

	/// <summary>Öffnet die Stadtinformationen für die angegebene Stadt.</summary>
	public Task ShowDialog(int stadtId)
	{
		var mgr = new StadtInformationenManager(stadtId);

		_titel.Text = mgr.StadtName;
		_umsatzWert.Text = mgr.Umsatzsteuer;
		_einwohnerWert.Text = mgr.Einwohner.ToString();

		foreach (Node kind in _iconEbene.GetChildren())
			kind.QueueFree();

		BaueReichtum(mgr.Reichtum);
		BaueKriminalitaet(mgr.Kriminalitaet);
		BaueRohstoffreihe(mgr.Hauptproduktion, 301, 270, 46, 52);
		BaueRohstoffreihe(mgr.Nebenproduktion, 457, 285, 30, 36);
		BaueRohstoffreihe(mgr.Nachfrage, 301, 330, 46, 52);
		BaueRohstoffreihe(mgr.Werkstaetten, 301, 391, 46, 52);
		BaueLagerstand(mgr.Lagerstand);

		Show();
		SetProcessInput(true);

		_dialogClosed = new TaskCompletionSource<bool>();
		return _dialogClosed.Task;
	}

	#region Icon-Reihen

	// Reichtum: bis zu 14 Münzen, 7 je Reihe, rechtsbündig ab x=458 (Original). Bei ≤7 nur eine Reihe,
	// die zur vertikalen Zentrierung leicht nach unten rückt.
	private void BaueReichtum(int reichtum)
	{
		for (int i = 1; i <= reichtum; i++)
		{
			int spalte = (i - 1) % 7;
			int reihe = (i - 1) / 7;
			float x = 458 - spalte * 26;
			float y = 81 + reihe * 26;
			if (reichtum <= 7)
				y += 13;

			_iconEbene.AddChild(MacheIcon(_symbReichtum, x, y, 23, 23));
		}
	}

	// Kriminalität: bis zu 5 Dolche, rechtsbündig ab x=441 (Original).
	private void BaueKriminalitaet(int kriminalitaet)
	{
		for (int i = 0; i < kriminalitaet; i++)
			_iconEbene.AddChild(MacheIcon(_symbCrime, 441 - i * 35, 220, 29, 36));
	}

	private void BaueRohstoffreihe(IReadOnlyList<Rohstoffangabe> rohstoffe, float startX, float y, float groesse, float abstand)
	{
		for (int i = 0; i < rohstoffe.Count; i++)
		{
			var textur = LadeRohstoff(rohstoffe[i].RohId);
			if (textur != null)
				_iconEbene.AddChild(MacheIcon(textur, startX + i * abstand, y, groesse, groesse, rohstoffe[i].Name));
		}
	}

	// Lagerstand: Rasterreihen zu je 7 Icons; das farbige Feld (rot/orange/grün) zeigt die Bewertung.
	private void BaueLagerstand(IReadOnlyList<Lagerbestand> lager)
	{
		float pad = 3 * S;

		for (int i = 0; i < lager.Count; i++)
		{
			int spalte = i % 7;
			int reihe = i / 7;
			float x = 302 + spalte * 36;
			float y = 453 + reihe * 36;

			var feld = new ColorRect
			{
				Color = FarbeFuer(lager[i].Stufe),
				MouseFilter = MouseFilterEnum.Pass,
				TooltipText = lager[i].Name + " (" + StufeText(lager[i].Stufe) + ")"
			};
			feld.Position = new Vector2(x * S, y * S);
			feld.Size = new Vector2(30 * S, 30 * S);

			var textur = LadeRohstoff(lager[i].RohId);
			if (textur != null)
			{
				var icon = new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					Texture = textur,
					MouseFilter = MouseFilterEnum.Ignore
				};
				icon.Position = new Vector2(pad, pad);
				icon.Size = new Vector2(30 * S - 2 * pad, 30 * S - 2 * pad);
				feld.AddChild(icon);
			}

			_iconEbene.AddChild(feld);
		}
	}

	#endregion

	#region Hilfsfunktionen

	private TextureRect MacheIcon(Texture2D textur, float x, float y, float breite, float hoehe, string tooltip = "")
	{
		var icon = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = textur,
			TooltipText = tooltip,
			MouseFilter = tooltip == "" ? MouseFilterEnum.Ignore : MouseFilterEnum.Pass
		};
		icon.Position = new Vector2(x * S, y * S);
		icon.Size = new Vector2(breite * S, hoehe * S);
		return icon;
	}

	private Texture2D LadeRohstoff(int rohId)
	{
		if (!_rohstoffIcons.TryGetValue(rohId, out var textur))
		{
			string pfad = "res://assets/images/rohstoffe/Roh" + rohId + ".png";
			textur = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
			_rohstoffIcons[rohId] = textur;
		}
		return textur;
	}

	private static Color FarbeFuer(Lagerstufe stufe) => stufe switch
	{
		Lagerstufe.Niedrig => Colors.DarkRed,
		Lagerstufe.Normal => Colors.Orange,
		_ => Colors.DarkGreen
	};

	private static string StufeText(Lagerstufe stufe) => stufe switch
	{
		Lagerstufe.Niedrig => "niedrig",
		Lagerstufe.Normal => "normal",
		_ => "hoch"
	};

	#endregion

	private void CloseDialog()
	{
		Hide();
		SetProcessInput(false);
		_dialogClosed?.TrySetResult(true);
	}
}
