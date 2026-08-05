using System.Text;
using System.Threading.Tasks;
using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Einstellungen;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Zeigt die lokale Auftrags-Bestenliste (Highscores): oben ein Auswahlfeld für den Auftrag, darunter
/// die Einträge sortiert nach Schnelligkeit (wenigste Spieljahre zuerst). Rechtsklick/Esc schließt.
/// </summary>
public partial class BestenlisteDialog : DialogBase
{
	[Export] public NodePath OptionButtonAuftragPath { get; set; }
	[Export] public NodePath LabelEintraegePath { get; set; }

	private OptionButton _optionButtonAuftrag;
	private Label _labelEintraege;

	protected override void OnReady()
	{
		_optionButtonAuftrag = GetNode<OptionButton>(OptionButtonAuftragPath);
		_labelEintraege = GetNode<Label>(LabelEintraegePath);
		_optionButtonAuftrag.ItemSelected += _ => ZeigeEintraege();
	}

	/// <summary>Öffnet die Bestenliste und füllt das Auftrags-Auswahlfeld mit allen Aufträgen.</summary>
	public Task ShowDialog()
	{
		_optionButtonAuftrag.Clear();

		foreach (var info in AuftragManager.GetAlleAuftraege())
			_optionButtonAuftrag.AddItem(SchwierigkeitKuerzel(info.Schwierigkeit) + info.Name, (int)info.Auftrag);

		_optionButtonAuftrag.Selected = 0;
		ZeigeEintraege();

		return ShowAndAwait();
	}

	private void ZeigeEintraege()
	{
		var auftrag = (EnumAuftrag)_optionButtonAuftrag.GetSelectedId();
		var eintraege = new HighscoreManager(ClientSettings.SavegamePath).GetEintraege(auftrag);

		if (eintraege.Count == 0)
		{
			_labelEintraege.Text = "Noch keine Einträge für diesen Auftrag.";
			return;
		}

		var sb = new StringBuilder();
		int platz = 1;

		foreach (var e in eintraege)
		{
			sb.AppendLine(platz + ".   " + e.SpielerName + "   —   A.D. " + e.Spieljahr +
			              "   (nach " + e.JahreGespielt + " Jahren, " + e.MitspielerAnzahl + " Mitspieler)");
			platz++;
		}

		_labelEintraege.Text = sb.ToString();
	}

	private static string SchwierigkeitKuerzel(EnumAuftragSchwierigkeit schwierigkeit)
	{
		switch (schwierigkeit)
		{
			case EnumAuftragSchwierigkeit.Leicht: return "[Leicht] ";
			case EnumAuftragSchwierigkeit.Mittel: return "[Mittel] ";
			default: return "[Schwer] ";
		}
	}
}
