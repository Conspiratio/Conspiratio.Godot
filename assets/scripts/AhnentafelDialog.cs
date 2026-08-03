using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.controls;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Zeigt die Ahnentafel der Dynastie als grafischen Stammbaum (Issue #9): je Generation Oberhaupt,
/// Ehepartner und Kinder mit Geburts-/Todesjahren, verbunden durch die Dynastielinie. Eine kleine Legende
/// erklärt die Rahmenfarben. Wird als immer verfügbares Privileg geöffnet. Rechtsklick schließt.
/// </summary>
public partial class AhnentafelDialog : DialogBase
{
	[Export]
	public NodePath BaumPath { get; set; }

	[Export]
	public NodePath LegendePath { get; set; }

	private AhnentafelBaum _baum;
	private VBoxContainer _legende;

	protected override void OnReady()
	{
		_baum = GetNode<AhnentafelBaum>(BaumPath);
		_legende = GetNode<VBoxContainer>(LegendePath);
		BaueLegende();
	}

	/// <summary>Öffnet die Ahnentafel des aktiven Spielers.</summary>
	public Task ShowDialog()
	{
		_baum.Baue(new AhnentafelManager().GetGenerationen());
		return ShowAndAwait();
	}

	private void BaueLegende()
	{
		FuegeLegendenzeileHinzu(AhnentafelBaum.LebendRand, "Lebende Generation");
		FuegeLegendenzeileHinzu(AhnentafelBaum.ErbeRand, "Erbe der Dynastie");
		FuegeLegendenzeileHinzu(AhnentafelBaum.KarteRand, "Verstorbene Angehörige");
	}

	private void FuegeLegendenzeileHinzu(Color randfarbe, string beschriftung)
	{
		var zeile = new HBoxContainer();
		zeile.AddThemeConstantOverride("separation", 10);

		var rahmen = new StyleBoxFlat
		{
			BgColor = AhnentafelBaum.KarteFuellung,
			BorderColor = randfarbe,
			BorderWidthLeft = 3,
			BorderWidthRight = 3,
			BorderWidthTop = 3,
			BorderWidthBottom = 3,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4
		};

		var muster = new Panel { CustomMinimumSize = new Vector2(30, 20) };
		muster.AddThemeStyleboxOverride("panel", rahmen);
		zeile.AddChild(muster);

		var label = new Label { Text = beschriftung, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeColorOverride("font_color", new Color(0.16f, 0.11f, 0.05f));
		label.AddThemeFontSizeOverride("font_size", 16);
		zeile.AddChild(label);

		_legende.AddChild(zeile);
	}
}
