using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Der Kirchgang als eigener Bildschirm (Migration von Posi_Kirchgang): Ablass kaufen (halbiert die
/// Sünden), beichten (senkt die Sünden um eins) und ein Waisenkind adoptieren (kostet Taler und Ansehen).
/// Die Klickbereiche sitzen an den Originalpositionen; Rechtsklick führt zurück in die Kirche.
/// </summary>
public partial class Kirchgang : Control
{
	private static readonly string[] AreaNamen = { "AreaAblass", "AreaBeichten", "AreaWaisenkind" };

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;

	private Main _main;
	private KircheManager _kircheManager;

	public override void _Ready()
	{
		_labelPlayerNameAndOffice = GetNode<Label>("LabelPlayerNameAndOffice");
		_labelPlaceDate = GetNode<Label>("LabelPlaceDate");
		_labelTaler = GetNode<Label>("LabelTaler");

		// Die Klickbereiche sind unsichtbar; ihre goldene Beschriftung erscheint nur bei MouseOver
		foreach (string areaName in AreaNamen)
		{
			var area = GetNode<Button>(areaName);
			area.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
			area.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

			var label = GetNode<Label>("Label" + areaName.Substring("Area".Length));
			area.MouseEntered += () => label.Visible = true;
			area.MouseExited += () => label.Visible = false;
		}

		_main = GetParent<Main>();
		SetProcessInput(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_next_or_close"))
			return;

		SoundManager.Instance.PlayRightClick();
		CloseKirchgang();
	}

	/// <summary>
	/// Öffnet den Kirchgang. Der KircheManager wird von der Kirche übernommen.
	/// </summary>
	public void ShowKirchgang(KircheManager kircheManager)
	{
		_kircheManager = kircheManager;

		UpdateHud();

		Show();
		SetProcessInput(true);
	}

	private void CloseKirchgang()
	{
		Hide();
		SetProcessInput(false);
		_main.Kirche.ReturnFromKirchgang();
	}

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Kirchgang A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert();
	}

	private async void _on_area_ablass_pressed()
	{
		SetProcessInput(false);

		int kosten = _kircheManager.GetAblassKosten();

		if (kosten == 0)
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt keine Sünden begangen, die einen Ablasskauf bedürfen.");
		}
		else if (!_kircheManager.KannBezahlen(kosten))
		{
			await SW.UI.ShowText.ShowDialog("Die " + kosten.ToStringGeld(false) + " Taler für den Ablass besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr den Ablass für\n" + kosten.ToStringGeld() + " kaufen?", "Ja", "Nein") == DialogResultGame.Yes)
		{
			_kircheManager.KaufeAblass(kosten);
			SoundManager.Instance.PlayCoins();
			UpdateHud();
			await SW.UI.ShowText.ShowDialog("Ein Teil Eurer Sünden ist Euch vergeben.");
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_beichten_pressed()
	{
		SetProcessInput(false);

		if (_kircheManager.HatSchonGebeichtet())
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt dieses Jahr bereits genug Sünden gebeichtet!");
		}
		else if (_kircheManager.GetDeliktpunkte() > 0)
		{
			_kircheManager.Beichte();
			await SW.UI.ShowText.ShowDialog("Ihr begebt Euch zu einem Priester, welchem Ihr einen Teil Eurer Sünden gesteht. Entsetzt und widerwillig gewährt Euch dieser die Absolution mit der Aufforderung, das Gotteshaus zu verlassen.");
		}
		else
		{
			await SW.UI.ShowText.ShowDialog("Ihr habt keine Sünden begangen, die einer Beichte bedürfen.");
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_waisenkind_pressed()
	{
		SetProcessInput(false);

		if (!_kircheManager.DarfWaisenkindAdoptieren())
		{
			string vaterMutter = SW.Dynamisch.GetAktHum().GetMaennlich() ? "glücklicher Vater" : "glückliche Mutter";
			await SW.UI.ShowText.ShowDialog("Ihr seid derzeit " + vaterMutter + " eines Kindes und könnt daher kein Waisenkind adoptieren.");
		}
		else
		{
			int preis = _kircheManager.GetWaisenkindPreis();

			if (!_kircheManager.KannBezahlen(preis))
			{
				await SW.UI.ShowText.ShowDialog("Die " + preis.ToStringGeld(false) + " Taler für die Adoption besitzt Ihr nicht.");
			}
			else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr ein Mündel für\n" + preis.ToStringGeld() +
			         " aus dem kirchlichen Waisenhaus adoptieren?\nEuer Ansehen könnte darunter leiden ...", "Ja", "Lieber nicht!") == DialogResultGame.Yes)
			{
				string name = _kircheManager.AdoptiereWaisenkind(preis);
				SoundManager.Instance.PlayCoins();
				UpdateHud();
				await SW.UI.ShowText.ShowDialog("Dank Eurer großzügigen Spende konntet Ihr das Kind " + name +
				                                " aus dem Waisenhaus adoptieren. Euer Ansehen hat gelitten.");
			}
		}

		if (Visible)
			SetProcessInput(true);
	}
}
