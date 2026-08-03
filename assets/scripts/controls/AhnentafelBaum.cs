using System.Collections.Generic;
using Conspiratio.Lib.Gameplay.Personen;
using Godot;

namespace Conspiratio.Godot.assets.scripts.controls;

/// <summary>
/// Zeichnet die Ahnentafel als grafischen Stammbaum: je Generation ein Paar (Oberhaupt + Ehepartner,
/// durch eine Heiratslinie verbunden) mit den Kindern darunter (an einer Geschwisterleiste). Der Erbe
/// (Kind oder Ehepartner) ist golden hervorgehoben und über die „Dynastielinie" mit dem Oberhaupt der
/// nächsten Generation verbunden. Die Personenkarten sind Kindknoten, die Verbindungslinien werden in
/// <see cref="_Draw"/> gezeichnet.
/// </summary>
public partial class AhnentafelBaum : Control
{
	private const float CardW = 168f;
	private const float CardH = 58f;
	private const float KindCardW = 148f;
	private const float KindCardH = 54f;
	private const float CoupleGap = 54f;
	private const float ChildTopGap = 46f;
	private const float ChildHGap = 22f;
	private const float GenVGap = 60f;
	private const float RandOben = 20f;

	private static readonly Color LinienFarbe = new(0.16f, 0.11f, 0.05f);
	private static readonly Color KarteFuellung = new(0.90f, 0.82f, 0.62f);
	private static readonly Color KarteRand = new(0.30f, 0.20f, 0.09f);
	private static readonly Color ErbeRand = new(0.72f, 0.53f, 0.10f);
	private static readonly Color LebendRand = new(0.20f, 0.42f, 0.16f);

	private readonly List<(Vector2 Von, Vector2 Bis)> _linien = new();

	/// <summary>Baut den Stammbaum für die übergebenen Generationen (älteste zuerst, lebende zuletzt).</summary>
	public void Baue(IReadOnlyList<Dynastiegeneration> generationen)
	{
		foreach (Node kind in GetChildren())
		{
			RemoveChild(kind);
			kind.QueueFree();
		}
		_linien.Clear();

		// Breite bestimmen (breiteste Generation) für eine gemeinsame, mittige Dynastie-Achse.
		float maxBreite = CardW;
		foreach (var g in generationen)
		{
			float paarBreite = g.Ehepartner != null ? CardW * 2 + CoupleGap : CardW;
			float kinderBreite = g.Kinder.Count > 0 ? g.Kinder.Count * KindCardW + (g.Kinder.Count - 1) * ChildHGap : 0;
			maxBreite = Mathf.Max(maxBreite, Mathf.Max(paarBreite, kinderBreite));
		}

		// In einer mindestens sichtbaren Breite zentrieren; breitere Dynastien sprengen das und werden scrollbar.
		float canvasBreite = Mathf.Max(maxBreite + 80f, 1280f);
		float mitteX = canvasBreite / 2f;
		float y = RandOben;
		Vector2? vorigerAusgang = null;

		for (int i = 0; i < generationen.Count; i++)
		{
			var g = generationen[i];
			bool lebend = i == generationen.Count - 1;

			float paarBreite = g.Ehepartner != null ? CardW * 2 + CoupleGap : CardW;
			float paarLinks = mitteX - paarBreite / 2f;

			// Verbindung von der vorigen Generation zum Oberhaupt dieser Generation.
			if (vorigerAusgang.HasValue)
				_linien.Add((vorigerAusgang.Value, new Vector2(mitteX, y)));

			// Oberhaupt-Karte (bei Paar linksbündig, sonst mittig).
			float oberhauptLinks = g.Ehepartner != null ? paarLinks : mitteX - CardW / 2f;
			ErzeugeKarte(g.Oberhaupt, new Vector2(oberhauptLinks, y), CardW, CardH, false, lebend);

			// Ehepartner-Karte + Heiratslinie.
			if (g.Ehepartner != null)
			{
				float partnerLinks = paarLinks + CardW + CoupleGap;
				ErzeugeKarte(g.Ehepartner, new Vector2(partnerLinks, y), CardW, CardH, g.EhepartnerErbte, lebend);
				_linien.Add((new Vector2(oberhauptLinks + CardW, y + CardH / 2f), new Vector2(partnerLinks, y + CardH / 2f)));
			}

			float paarUntenY = y + CardH;
			Vector2 ausgang = new(mitteX, paarUntenY);   // Standard-Ausgang: Mitte unter dem Paar.

			if (g.Kinder.Count > 0)
			{
				float kinderY = paarUntenY + ChildTopGap;
				float kinderBreite = g.Kinder.Count * KindCardW + (g.Kinder.Count - 1) * ChildHGap;
				float startX = mitteX - kinderBreite / 2f;
				float leisteY = paarUntenY + ChildTopGap / 2f;

				// Abstieg vom Paar zur Geschwisterleiste.
				_linien.Add((new Vector2(mitteX, paarUntenY), new Vector2(mitteX, leisteY)));

				float ersteMitte = startX + KindCardW / 2f;
				float letzteMitte = startX + (g.Kinder.Count - 1) * (KindCardW + ChildHGap) + KindCardW / 2f;
				_linien.Add((new Vector2(ersteMitte, leisteY), new Vector2(letzteMitte, leisteY)));

				for (int k = 0; k < g.Kinder.Count; k++)
				{
					float kx = startX + k * (KindCardW + ChildHGap);
					float kMitte = kx + KindCardW / 2f;
					bool istErbe = k == g.ErbeKindIndex;

					_linien.Add((new Vector2(kMitte, leisteY), new Vector2(kMitte, kinderY)));
					ErzeugeKarte(g.Kinder[k], new Vector2(kx, kinderY), KindCardW, KindCardH, istErbe, false);

					if (istErbe)
						ausgang = new Vector2(kMitte, kinderY + KindCardH);
				}

				y = kinderY + KindCardH;
			}
			else
			{
				y = paarUntenY;
			}

			// Erbt der Ehepartner, geht die Dynastielinie von ihm aus.
			if (g.EhepartnerErbte && g.Ehepartner != null)
				ausgang = new Vector2(paarLinks + CardW + CoupleGap + CardW / 2f, y);

			vorigerAusgang = ausgang;
			y += GenVGap;
		}

		CustomMinimumSize = new Vector2(canvasBreite, y);
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var (von, bis) in _linien)
			DrawLine(von, bis, LinienFarbe, 2f, true);
	}

	private void ErzeugeKarte(AhnPerson person, Vector2 position, float breite, float hoehe, bool istErbe, bool lebend)
	{
		var rahmen = new StyleBoxFlat
		{
			BgColor = KarteFuellung,
			BorderColor = lebend ? LebendRand : istErbe ? ErbeRand : KarteRand,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5
		};
		int rand = lebend || istErbe ? 3 : 2;
		rahmen.BorderWidthLeft = rand;
		rahmen.BorderWidthRight = rand;
		rahmen.BorderWidthTop = rand;
		rahmen.BorderWidthBottom = rand;

		var panel = new Panel { Position = position, Size = new Vector2(breite, hoehe) };
		panel.AddThemeStyleboxOverride("panel", rahmen);
		AddChild(panel);

		string jahre = "* " + person.Geburtsjahr + (person.Todesjahr > 0 ? "   † " + person.Todesjahr : "");
		var label = new Label
		{
			Text = person.Name + "\n" + jahre,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Position = Vector2.Zero,
			Size = new Vector2(breite, hoehe)
		};
		label.AddThemeColorOverride("font_color", new Color(0.16f, 0.11f, 0.05f));
		label.AddThemeFontSizeOverride("font_size", 15);
		panel.AddChild(label);
	}
}
