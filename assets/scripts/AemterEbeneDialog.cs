using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Gameplay.Privilegien.Weltkarte;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Ämter-Ebene (Migration von AemterEbene): zeigt für ein Gebiet (Stadt/Land/Reich) die Ämter
/// je politischer, kirchlicher und militärischer Ebene mit ihren Inhabern. Zu jedem Inhaber werden
/// die Statussymbole (laufende Sabotage/Spionage, Ehe, Konfession) und – bei KI-Inhabern – ein
/// Balken für die Beziehung zum aktiven Spieler eingeblendet. Über den Wechsel-Button blättert man
/// durch die Ebenen; ein Klick auf einen Amtsinhaber führt die Modus-Aktion aus (Prozess/Henkershand).
/// Die Struktur liegt im AemterEbeneManager der Lib.
/// </summary>
public partial class AemterEbeneDialog : DialogBase
{
	// Anzeigegrößen der Symbole (aus dem Original, auf 1600×900 skaliert).
	private static readonly Vector2 GroesseSabo = new(27, 27);
	private static readonly Vector2 GroesseSpio = new(23, 27);
	private static readonly Vector2 GroesseEhe = new(34, 26);
	private static readonly Vector2 GroesseRel = new(27, 27);

	private const float ButtonBreite = 210;
	private const float ButtonHoehe = 72;
	private const float SeitenRand = 34; // Platz für die Symbole links/rechts

	[Export]
	public NodePath LabelTitelPath { get; set; }

	[Export]
	public NodePath ButtonWechselPath { get; set; }

	[Export]
	public NodePath VBoxAemterPath { get; set; }

	private Label _labelTitel;
	private controls.ButtonWithSounds _buttonWechsel;
	private VBoxContainer _vBoxAemter;

	private PackedScene _buttonScene;
	private Texture2D _texSabo;
	private Texture2D _texSpio;
	private Texture2D _texEhe;
	private Texture2D _texRelKath;
	private Texture2D _texRelEvan;

	private AemterEbeneManager _manager;
	private int _modus;
	private int _ebene;

	protected override void OnReady()
	{
		_labelTitel = GetNode<Label>(LabelTitelPath);
		_buttonWechsel = GetNode<controls.ButtonWithSounds>(ButtonWechselPath);
		_vBoxAemter = GetNode<VBoxContainer>(VBoxAemterPath);

		_buttonScene = GD.Load<PackedScene>("res://scenes/controls/ButtonWithSounds.tscn");
		_texSabo = GD.Load<Texture2D>("res://assets/images/symbole/SymbSabotage.png");
		_texSpio = GD.Load<Texture2D>("res://assets/images/symbole/SymbSpionage.png");
		_texEhe = GD.Load<Texture2D>("res://assets/images/symbole/SymbVerheiratet.png");
		_texRelKath = GD.Load<Texture2D>("res://assets/images/symbole/SymbRel1.png");
		_texRelEvan = GD.Load<Texture2D>("res://assets/images/symbole/SymbRel2.png");

		_buttonWechsel.Pressed += OnWechselPressed;
	}

	/// <summary>Öffnet die Ämter-Ebene für ein Gebiet (Stufe 0/1/2 = Stadt/Land/Reich) im gegebenen Modus.</summary>
	public Task ShowDialog(int objektId, int stufe, int modus)
	{
		_manager = new AemterEbeneManager(objektId, stufe);
		_modus = modus;
		_ebene = 0;

		_labelTitel.Text = _manager.GetTitel(modus);
		Fill();

		return ShowAndAwait();
	}

	private void OnWechselPressed()
	{
		_ebene = (_ebene + 1) % _manager.AnzahlEbenen;
		Fill();
	}

	private void Fill()
	{
		foreach (Node child in _vBoxAemter.GetChildren())
		{
			_vBoxAemter.RemoveChild(child);
			child.QueueFree();
		}

		_buttonWechsel.Text = _manager.GetEbenenName(_ebene);

		var aemter = _manager.GetAemter(_ebene);

		// Erstes Amt (das ranghöchste) zentriert oben, die übrigen in Reihen zu je drei.
		HBoxContainer aktuelleReihe = null;
		for (int i = 0; i < aemter.Count; i++)
		{
			if (i == 0 || (i - 1) % 3 == 0)
			{
				aktuelleReihe = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
				aktuelleReihe.AddThemeConstantOverride("separation", 24);
				_vBoxAemter.AddChild(aktuelleReihe);
			}

			aktuelleReihe.AddChild(BaueAmtZelle(aemter[i]));
		}
	}

	private Control BaueAmtZelle(AemterSlotInfo amt)
	{
		var zelle = new Control { CustomMinimumSize = new Vector2(SeitenRand * 2 + ButtonBreite, ButtonHoehe + 18) };

		var button = _buttonScene.Instantiate<controls.ButtonWithSounds>();
		button.Text = amt.AmtName + "\n" + amt.HolderName;
		button.Position = new Vector2(SeitenRand, 0);
		button.Size = new Vector2(ButtonBreite, ButtonHoehe);
		button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		button.Disabled = !amt.Besetzt;

		// Wie im Original: klickbarer Text auf dem Pergament (kein Button-Kasten), dunkle Schrift.
		button.Flat = true;
		button.AddThemeColorOverride("font_color", new Color(0.12f, 0.06f, 0.02f));
		button.AddThemeColorOverride("font_hover_color", new Color(0.55f, 0.2f, 0f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.55f, 0.2f, 0f));
		button.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.34f, 0.28f));

		int holderId = amt.HolderId;
		button.Pressed += () => OnAmtPressed(holderId);
		zelle.AddChild(button);

		float linkeSpalte = SeitenRand + ButtonBreite; // rechts vom Button
		float rechteSymbolX = SeitenRand - GroesseSabo.X + 4; // links vom Button
		if (rechteSymbolX < 0)
			rechteSymbolX = 0;

		// Sabotage: links oben
		if (amt.HatSabotage)
			zelle.AddChild(SymbolRect(_texSabo, new Vector2(rechteSymbolX, 5), GroesseSabo));

		// Konfession: links unterhalb der Sabotage
		if (amt.Konfession == AemterKonfession.Katholisch)
			zelle.AddChild(SymbolRect(_texRelKath, new Vector2(rechteSymbolX, 5 + GroesseSabo.Y + 6), GroesseRel));
		else if (amt.Konfession == AemterKonfession.Evangelisch)
			zelle.AddChild(SymbolRect(_texRelEvan, new Vector2(rechteSymbolX, 5 + GroesseSabo.Y + 6), GroesseRel));

		// Spionage: rechts oben
		if (amt.HatSpionage)
			zelle.AddChild(SymbolRect(_texSpio, new Vector2(linkeSpalte, 5), GroesseSpio));

		// Ehe: rechts unten
		if (amt.IstVerheiratet)
			zelle.AddChild(SymbolRect(_texEhe, new Vector2(linkeSpalte, ButtonHoehe - GroesseEhe.Y - 5), GroesseEhe));

		// Beziehungs-Balken (nur KI-Inhaber): schwarzer Balken unter dem Button, Breite ~ Beziehung.
		if (amt.IstKI && amt.Beziehung > 0)
		{
			float breite = amt.Beziehung * (1600f / 1366f);
			var balken = new ColorRect
			{
				Color = Colors.Black,
				Position = new Vector2(SeitenRand + (ButtonBreite - breite) / 2f, ButtonHoehe + 2),
				Size = new Vector2(breite, 8),
				MouseFilter = MouseFilterEnum.Ignore,
				TooltipText = "Beziehung: " + amt.Beziehung
			};
			zelle.AddChild(balken);
		}

		return zelle;
	}

	private static TextureRect SymbolRect(Texture2D textur, Vector2 position, Vector2 groesse)
	{
		// Wichtig: ExpandMode VOR Size setzen. Bei Default (KeepSize) ist die Mindestgröße die native
		// Texturgröße (z. B. SymbRel1 mit 650×900); ein danach gesetztes Size würde hochgeklemmt und
		// von einem späteren IgnoreSize nicht mehr verkleinert – die Symbole erschienen dann viel zu groß.
		var rect = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Texture = textur,
			MouseFilter = MouseFilterEnum.Ignore
		};

		rect.Position = position;
		rect.Size = groesse;

		return rect;
	}

	private async void OnAmtPressed(int holderId)
	{
		if (holderId == 0)
			return;

		SetProcessInput(false);

		await _manager.PersonWasMachen(holderId, _modus);

		// Wie im Original: nach dem Prozess (Modus 8) schließt die Ämter-Ebene; sonst bleibt sie offen.
		if (_modus == 8)
		{
			Close(DialogResultGame.OK);
			return;
		}

		if (Visible)
		{
			Fill();
			SetProcessInput(true);
		}
	}

	private void _on_link_button_close_pressed()
	{
		Close(DialogResultGame.OK);
	}
}
