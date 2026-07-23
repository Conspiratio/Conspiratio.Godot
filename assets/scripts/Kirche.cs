using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Kirche (Migration der Kirche aus dem WinForms-Client): Hochzeitsglocken (Partnersuche über die
/// Kupplerin), Kirchgang (Ablass kaufen, beichten, Waisenkind adoptieren), Konvertieren, Austreten und
/// das Testament. Konfessionslose Spieler werden zunächst gefragt, welchen Glauben sie annehmen wollen.
/// Rechtsklick auf einen Bereich zeigt seine Beschreibung, Rechtsklick sonst führt zurück ins Kontor.
/// </summary>
public partial class Kirche : Control
{
	private static readonly string[] AreaNamen = { "AreaHochzeit", "AreaTestament", "AreaKirchgang", "AreaKonvertieren", "AreaAustreten" };

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;

	private Main _main;
	private KircheManager _kircheManager;
	private FamilieManager _familieManager;

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

	public override async void _Input(InputEvent @event)
	{
		if (!Input.IsActionPressed("ui_next_or_close"))
			return;

		// Rechtsklick auf einen Bereich zeigt wie im Original seine Beschreibung an
		if (@event is InputEventMouseButton mausklick)
		{
			foreach (string areaName in AreaNamen)
			{
				var area = GetNode<Button>(areaName);

				if (!area.Visible || !area.GetGlobalRect().HasPoint(mausklick.GlobalPosition))
					continue;

				SetProcessInput(false);
				await SW.UI.ShowText.ShowDialog(GetBereichsBeschreibung(areaName));

				if (Visible)
					SetProcessInput(true);

				return;
			}
		}

		SoundManager.Instance.PlayRightClick();
		CloseKirche();
	}

	private static string GetBereichsBeschreibung(string areaName)
	{
		switch (areaName)
		{
			case "AreaHochzeit":
				return "Um den Fortbestand Eurer Dynastie zu sichern, solltet Ihr rechtzeitig mit der Werbung um einen Ehepartner beginnen";
			case "AreaTestament":
				return "Hier könnt Ihr Euer Testament aufsetzen und den Erben Eures Vermögens bestimmen";
			case "AreaKirchgang":
				return "Hier werden Geschäfte mit der Kirche abgewickelt";
			case "AreaKonvertieren":
				return "Wechselt Euren Glauben. Dies kann Euch Sympathie Andersgläubiger einbringen";
			default:
				return "Durch das Austreten aus der Kirche werdet Ihr von der Kirchensteuer befreit. Allerdings ist Euch damit auch die Kandidatur für kirchliche Ämter untersagt";
		}
	}

	/// <summary>
	/// Öffnet die Kirche für den aktiven Spieler. Konfessionslose werden zuerst nach ihrem Glauben gefragt.
	/// </summary>
	public async void ShowKirche()
	{
		_kircheManager = new KircheManager();
		_familieManager = new FamilieManager();

		// Konfessionslose sehen zunächst die Glaubensfrage (wie FormKonfessionslos im Original)
		if (_kircheManager.IstKonfessionslos())
		{
			await _main.KonfessionslosDialog.ShowDialog(_kircheManager);

			// Nimmt der Spieler keinen Glauben an, geht es zurück ins Kontor
			if (_kircheManager.IstKonfessionslos())
			{
				CloseKirche();
				return;
			}
		}

		UpdateHud();

		SoundManager.Instance.SpieleMusik(SoundManager.MusikKategorie.Kirche);
		Show();
		SetProcessInput(true);
	}

	private void CloseKirche()
	{
		Hide();
		SetProcessInput(false);
		_main.Kontor.ReturnFromStadt();
	}

	private void UpdateHud()
	{
		var spieler = SW.Dynamisch.GetAktHum();

		_labelPlayerNameAndOffice.Text = spieler.GetKompletterName();
		_labelPlaceDate.Text = "Kirche A.D. " + SW.Dynamisch.GetAktuellesJahr();
		_labelTaler.Text = spieler.GetTalerFormatiert();
	}

	private async void _on_area_hochzeit_pressed()
	{
		SetProcessInput(false);

		if (!_familieManager.KannPartnerSuchen(out string hinweis))
		{
			await SW.UI.ShowText.ShowDialog(hinweis);
		}
		else
		{
			var vorschlag = _familieManager.ErmittleKupplerinVorschlag();

			if (vorschlag == null)
			{
				await SW.UI.ShowText.ShowDialog("Die Kupplerin weiß derzeit niemanden, der zu Euch passt.");
			}
			else if (!_familieManager.KannBezahlen(vorschlag.Preis))
			{
				await SW.UI.ShowText.ShowDialog("Die Kupplerin würde Euch " + vorschlag.PartnerName + " vermitteln, doch die " +
				                                vorschlag.Preis.ToStringGeld(false) + " Taler dafür besitzt Ihr nicht.");
			}
			else if (await SW.UI.YesNoQuestion.ShowDialogText(
				         "Die Kupplerin schlägt Euch " + vorschlag.PartnerName + " vor.\nWollt Ihr für " +
				         vorschlag.Preis.ToStringGeld() + " um " + vorschlag.PartnerName + " werben?", "Ja", "Nein") == DialogResultGame.Yes)
			{
				_familieManager.BeginneWerbung(vorschlag);
				SoundManager.Instance.PlayCoins();
				await SW.UI.ShowText.ShowDialog("Die Kupplerin leitet alle Vorkehrungen in die Wege...");
				UpdateHud();
			}
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_testament_pressed()
	{
		SetProcessInput(false);

		await _main.TestamentDialog.ShowDialog(_familieManager);

		if (Visible)
			SetProcessInput(true);
	}

	private void _on_area_kirchgang_pressed()
	{
		SetProcessInput(false);
		Hide();

		// Der Kirchgang ist ein eigener Bildschirm wie im Original
		_main.Kirchgang.ShowKirchgang(_kircheManager);
	}

	/// <summary>
	/// Kehrt aus dem Kirchgang-Bildschirm in die Kirche zurück.
	/// </summary>
	public void ReturnFromKirchgang()
	{
		UpdateHud();
		Show();
		SetProcessInput(true);
	}

	private async void _on_area_konvertieren_pressed()
	{
		SetProcessInput(false);

		int kosten = _kircheManager.GetKonvertierkosten();

		if (!_kircheManager.KannBezahlen(kosten))
		{
			await SW.UI.ShowText.ShowDialog("Die " + kosten.ToStringGeld(false) + " Taler für den Glaubenswechsel besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr für " + kosten.ToStringGeld() + "\nzum " + _kircheManager.GetNaechsteReligionName() +
		                                                  "en Glauben wechseln?", "Ja", "Nein") == DialogResultGame.Yes)
		{
			_kircheManager.Konvertiere(kosten);
			SoundManager.Instance.PlayCoins();
			UpdateHud();
		}

		if (Visible)
			SetProcessInput(true);
	}

	private async void _on_area_austreten_pressed()
	{
		SetProcessInput(false);

		int kosten = _kircheManager.GetAustrittskosten();

		if (!_kircheManager.KannBezahlen(kosten))
		{
			await SW.UI.ShowText.ShowDialog("Die " + kosten.ToStringGeld(false) + " Taler für den Kirchenaustritt besitzt Ihr nicht.");
		}
		else if (await SW.UI.YesNoQuestion.ShowDialogText("Wollt Ihr für " + kosten.ToStringGeld() + "\naus der Kirche austreten?", "Ja", "Nein") == DialogResultGame.Yes)
		{
			_kircheManager.TritteAus(kosten);
			SoundManager.Instance.PlayCoins();
			UpdateHud();
			await SW.UI.ShowText.ShowDialog("Ihr seid nun konfessionslos.");

			// Als Konfessionsloser gibt es hier nichts mehr zu tun – zurück ins Kontor
			CloseKirche();
			return;
		}

		if (Visible)
			SetProcessInput(true);
	}
}
