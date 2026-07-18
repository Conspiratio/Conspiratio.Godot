using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Die Kirche (minimale Migration): Aktuell sind die Hochzeitsglocken (Partnersuche über die Kupplerin)
/// und das Testament funktional; die übrigen kirchlichen Tätigkeiten (Beichte, Kirchgang, Konvertieren,
/// Austreten, Bauwerk stiften) sind noch Platzhalter. Rechtsklick auf einen Bereich zeigt seine Beschreibung.
/// </summary>
public partial class Kirche : Control
{
	private static readonly string[] AreaNamen = { "AreaHochzeit", "AreaTestament", "AreaBeichte", "AreaKirchgang" };

	private Label _labelPlayerNameAndOffice;
	private Label _labelPlaceDate;
	private Label _labelTaler;

	private Main _main;
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
				return "Hier läuten die Hochzeitsglocken – hier könnt Ihr um einen Ehepartner werben";
			case "AreaTestament":
				return "Hier könnt Ihr Euer Testament aufsetzen und den Erben Eures Vermögens bestimmen";
			case "AreaBeichte":
				return "Hier könnt Ihr beichten";
			default:
				return "Hier könnt Ihr am Kirchgang teilnehmen";
		}
	}

	/// <summary>
	/// Öffnet die Kirche für den aktiven Spieler.
	/// </summary>
	public void ShowKirche()
	{
		_familieManager = new FamilieManager();

		UpdateHud();

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
		_labelTaler.Text = spieler.GetTalerFormatiert() + " Taler";
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

	private async void _on_area_nicht_implementiert_pressed()
	{
		SetProcessInput(false);

		// TODO: Beichte, Kirchgang, Konvertieren, Austreten und Bauwerk stiften migrieren
		await SW.UI.ShowText.ShowDialog("Wurde noch nicht implementiert");

		if (Visible)
			SetProcessInput(true);
	}
}
