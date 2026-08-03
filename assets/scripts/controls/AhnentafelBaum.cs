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
	private const float CardH = 84f;      // Oberhaupt/Ehepartner: Titel + Name + Jahre (3 Zeilen)
	private const float KindCardW = 148f;
	private const float KindCardH = 56f;  // Kinder: Name + Jahre (2 Zeilen, kein Titel)

	/// <summary>
	/// Kleiner Aufwärts-Versatz des Textes: Godot zentriert an der Zeilenbox, deren Oberkante durch die
	/// große Oberlänge der Schrift optisch tiefer wirkt – dadurch scheint sonst oben mehr Luft als unten.
	/// </summary>
	private const float TextVersatzHoch = 4f;
	private const float CoupleGap = 54f;
	private const float ChildTopGap = 46f;
	private const float ChildHGap = 22f;
	private const float GenVGap = 60f;
	private const float RandOben = 20f;

	private static readonly Color LinienFarbe = new(0.16f, 0.11f, 0.05f);

	// Öffentlich, damit die Legende (AhnentafelDialog) dieselben Farben verwendet.
	public static readonly Color KarteFuellung = new(0.90f, 0.82f, 0.62f);
	public static readonly Color KarteRand = new(0.30f, 0.20f, 0.09f);
	public static readonly Color ErbeRand = new(0.72f, 0.53f, 0.10f);
	public static readonly Color LebendRand = new(0.20f, 0.42f, 0.16f);

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
			float oberhauptLinks = g.Ehepartner != null ? paarLinks : mitteX - CardW / 2f;
			float oberhauptMitteX = oberhauptLinks + CardW / 2f;

			// Dynastielinie von der vorigen Generation an die Oberkante des Oberhaupts dieser Generation.
			if (vorigerAusgang.HasValue)
				_linien.Add((vorigerAusgang.Value, new Vector2(oberhauptMitteX, y)));

			ErzeugeKarte(g.Oberhaupt, new Vector2(oberhauptLinks, y), CardW, CardH, false, lebend);

			float midY = y + CardH / 2f;
			float paarUntenY = y + CardH;

			// Startpunkt der Abwärtslinie zu den Kindern und Ausgangspunkt der Dynastielinie.
			float abstiegX = oberhauptMitteX;
			float abstiegStartY = paarUntenY;
			Vector2 ausgang = new(oberhauptMitteX, paarUntenY);   // Standard: Unterkante des Oberhaupts.

			// Ehepartner-Karte + Heiratslinie.
			if (g.Ehepartner != null)
			{
				float partnerLinks = paarLinks + CardW + CoupleGap;
				float partnerMitteX = partnerLinks + CardW / 2f;
				ErzeugeKarte(g.Ehepartner, new Vector2(partnerLinks, y), CardW, CardH, g.EhepartnerErbte, lebend);

				// Heiratslinie auf halber Höhe zwischen den beiden Karten; die Kinderlinie hängt an ihrer Mitte.
				_linien.Add((new Vector2(oberhauptLinks + CardW, midY), new Vector2(partnerLinks, midY)));
				abstiegX = mitteX;
				abstiegStartY = midY;

				// Erbt der Ehepartner, geht die Dynastielinie von seiner Unterkante aus.
				if (g.EhepartnerErbte)
					ausgang = new Vector2(partnerMitteX, paarUntenY);
			}

			if (g.Kinder.Count > 0)
			{
				float kinderY = paarUntenY + ChildTopGap;
				float kinderBreite = g.Kinder.Count * KindCardW + (g.Kinder.Count - 1) * ChildHGap;
				float startX = mitteX - kinderBreite / 2f;
				float leisteY = paarUntenY + ChildTopGap / 2f;

				// Abstieg von der Heiratslinie (bzw. Oberhaupt-Unterkante) zur Geschwisterleiste.
				_linien.Add((new Vector2(abstiegX, abstiegStartY), new Vector2(abstiegX, leisteY)));

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

			vorigerAusgang = ausgang;

			if (i < generationen.Count - 1)
				y += GenVGap;
		}

		CustomMinimumSize = new Vector2(canvasBreite, y + RandOben);
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
		string kopf = string.IsNullOrEmpty(person.Titel) ? person.Name : person.Titel + "\n" + person.Name;
		string text = kopf + "\n" + jahre;

		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Position = new Vector2(0, -TextVersatzHoch),
			Size = new Vector2(breite, hoehe)
		};
		label.AddThemeColorOverride("font_color", new Color(0.16f, 0.11f, 0.05f));
		label.AddThemeFontSizeOverride("font_size", 15);
		panel.AddChild(label);
	}
}
