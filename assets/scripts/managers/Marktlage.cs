using Conspiratio.Lib.Extensions;
using Conspiratio.Lib.Gameplay.Spielwelt;

namespace Conspiratio.Godot.assets.scripts.managers;

/// <summary>
/// Der Sättigungsabschlag aus <c>Stadt.GetRohstoffPreisVonIDX</c>, in Spielersprache übersetzt.
///
/// Die Formel hat hier <b>einen</b> Wohnsitz, weil inzwischen drei Ansichten auf sie zugreifen: die
/// Preiszeile und die Verkaufszeile der Stadtansicht sowie der Lagerstand im
/// Stadtinformationen-Dialog. Eine zweite Kopie wäre genau die Art Duplikat, die stillschweigend
/// auseinanderläuft, sobald jemand eine der Lib-Konstanten ändert.
///
/// Siehe docs/saettigungsrabatt-sichtbar-konzept.md.
/// </summary>
public static class Marktlage
{
	/// <summary>
	/// Der Jahresbedarf einer Stadt, wie ihn <c>Stadt.GetRohstoffPreisVonIDX</c> ansetzt: ein Zehntel
	/// der Einwohner, mindestens 1 (der Divisionsschutz der Lib).
	/// </summary>
	public static int ErmittleJahresbedarf(Conspiratio.Lib.Gameplay.Gebiete.Stadt stadt)
	{
		return System.Math.Max(1, stadt.GetEinwohner() / 10);
	}

	/// <summary>
	/// Der Mengenabschlag in Prozentpunkten, wie ihn <c>Stadt.GetRohstoffPreisVonIDX</c> vor dem
	/// <c>Min(MaxAbschlagProzent, ...)</c> bildet - also <b>ungekappt</b>.
	///
	/// Ungekappt, weil der Wert dadurch zugleich verrät, wie viele Jahresbedarfe im Stadtlager liegen
	/// (ein Jahresbedarf je <c>AbschlagJeBedarfsjahrProzent</c>). Am Deckel ginge diese Angabe verloren,
	/// und genau sie ist der erklärende Teil: Ob dort fünf oder zwölf Jahresbedarfe liegen, macht
	/// für den Preis keinen Unterschied mehr, für die Entscheidung des Spielers aber sehr wohl.
	/// </summary>
	public static int ErmittleRohAbschlagProzent(Conspiratio.Lib.Gameplay.Gebiete.Stadt stadt, int rohstoffId)
	{
		return (stadt.GetRohstoffIDXVorrat(rohstoffId) * Conspiratio.Lib.Gameplay.Gebiete.Stadt.AbschlagJeBedarfsjahrProzent)
		       / ErmittleJahresbedarf(stadt);
	}

	/// <summary>
	/// Wie weit der Markt zum Deckel hin gesättigt ist, als Anteil zwischen 0 und 1 — die Stärke, mit
	/// der die Sättigungsfarbe eingemischt wird. Gerechnet wird auf dem <b>gekappten</b> Abschlag:
	/// Oberhalb des Deckels ändert sich am Preis nichts mehr, also soll sich auch an der Farbe nichts
	/// mehr ändern — sonst verspäche sie eine Verschlechterung, die nicht eintritt.
	/// </summary>
	public static float ErmittleSaettigungsAnteil(Conspiratio.Lib.Gameplay.Gebiete.Stadt stadt, int rohstoffId)
	{
		int abschlag = System.Math.Min(Conspiratio.Lib.Gameplay.Gebiete.Stadt.MaxAbschlagProzent,
		                               ErmittleRohAbschlagProzent(stadt, rohstoffId));

		return (float)abschlag / Conspiratio.Lib.Gameplay.Gebiete.Stadt.MaxAbschlagProzent;
	}

	/// <summary>
	/// Macht den Sättigungsrabatt in <see cref="HandelsManager"/>/<c>Stadt.GetRohstoffPreisVonIDX</c>
	/// für den Spieler lesbar: Der Preis in der Zeile ist bereits gerabattet, zeigt aber weder die
	/// Ursache (wie viele Jahresbedarfe im Stadtlager liegen) noch, ob der Deckel erreicht ist.
	/// Siehe docs/saettigungsrabatt-sichtbar-konzept.md, Stufe A.
	///
	/// Der Grundpreis kommt aus <c>Stadt.GetRohstoffBasispreisVonIDX</c> (Lib 4.7.0) und darf nicht aus
	/// dem Marktpreis zurückgerechnet werden: Die Lib teilt beim Abschlag ganzzahlig ab, sodass mehrere
	/// Grundpreise auf denselben Marktpreis führen (gemessen über Grundpreis 1-60 x Abschlag 0-50:
	/// 60 % der Rückrechnungen falsch). Eine falsche Zahl wäre hier schlimmer als gar keine, weil die
	/// angezeigten Werte dann beim Nachrechnen nicht aufgehen - genau die Probe, zu der ein erklärender
	/// Tooltip einlädt.
	/// </summary>
	public static string BeschreibeMarktpreis(int stadtId, int rohstoffId)
	{
		var stadt = SW.Dynamisch.GetStadtwithID(stadtId);
		string rohstoffName = SW.Dynamisch.GetRohstoffwithID(rohstoffId).GetRohName();

		int vorrat = stadt.GetRohstoffIDXVorrat(rohstoffId);
		int preis = stadt.GetRohstoffPreisVonIDX(rohstoffId);
		int grundpreis = stadt.GetRohstoffBasispreisVonIDX(rohstoffId);
		int jahresbedarf = ErmittleJahresbedarf(stadt);

		int rohAbschlagProzent = ErmittleRohAbschlagProzent(stadt, rohstoffId);
		int abschlagProzent = System.Math.Min(Conspiratio.Lib.Gameplay.Gebiete.Stadt.MaxAbschlagProzent, rohAbschlagProzent);

		if (abschlagProzent == 0)
		{
			return rohstoffName + ": Grundpreis " + grundpreis.ToStringGeld() + ".\n" +
			       stadt.GetGebietsName() + " verbraucht " + jahresbedarf + " im Jahr und lagert " +
			       (vorrat == 0 ? "nichts" : vorrat.ToString()) + " — kein Überhang, kein Marktabschlag.";
		}

		// Zehntel Jahresbedarfe, aus demselben ungekappten Abschlag gebildet wie der Prozentsatz -
		// so gehen die beiden Zahlen im Tooltip miteinander auf, wenn der Spieler nachrechnet.
		int zehntelBedarfe = (rohAbschlagProzent * 10) / Conspiratio.Lib.Gameplay.Gebiete.Stadt.AbschlagJeBedarfsjahrProzent;
		string jahresbedarfeText = zehntelBedarfe % 10 == 0
			? (zehntelBedarfe / 10) + (zehntelBedarfe == 10 ? " Jahresbedarf" : " Jahresbedarfe")
			: (zehntelBedarfe / 10) + "," + (zehntelBedarfe % 10) + " Jahresbedarfe";

		string hinweisDeckel = abschlagProzent == Conspiratio.Lib.Gameplay.Gebiete.Stadt.MaxAbschlagProzent
			? " (Höchstwert — mehr Vorrat drückt den Preis nicht weiter)"
			: "";

		return rohstoffName + ": Grundpreis " + grundpreis.ToStringGeld() + ".\n" +
		       stadt.GetGebietsName() + " verbraucht " + jahresbedarf + " im Jahr und lagert " + vorrat +
		       " — " + jahresbedarfeText + ".\n" +
		       "Marktabschlag " + abschlagProzent + " %" + hinweisDeckel +
		       ", Ihr erhaltet " + preis.ToStringGeld() + ".";
	}
}
