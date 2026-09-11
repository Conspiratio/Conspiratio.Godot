using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Conspiratio.Godot.assets.scripts.managers;
using Conspiratio.Lib.Allgemein;
using Conspiratio.Lib.Gameplay.Einstellungen;
using Conspiratio.Lib.Gameplay.Kampf;
using Conspiratio.Lib.Gameplay.Niederlassung;
using Conspiratio.Lib.Gameplay.Rohstoffe;
using Conspiratio.Lib.Gameplay.Spielwelt;

using Godot;

namespace Conspiratio.Godot.assets.scripts;

/// <summary>
/// Brautwerbung und Erbfolge - dynastische Fortsetzung, kein Beiwerk.
///
/// Ohne beides überlebt kein Lauf die ersten fünfundzwanzig Jahre: <c>FuehreTestamentAus</c>
/// liest <c>GetErbeSpielerID()</c>, und Kinder erben nicht von selbst.
/// </summary>
public partial class E2eTreiber
{
	/// <summary>
	/// Sucht in der Kirche die Kupplerin auf und wirbt um einen Ehepartner. Läuft wie die Handelsrunde
	/// **immer** mit, auch mit <c>--ohne-aktionen</c>: Ohne Ehe gibt es keinen Erben, und ohne Erben
	/// beendet der erste Todesfall die Dynastie (<c>Kontor.cs</c>, Pfad <c>testament.SpielVorbei</c>).
	/// Gemessen kam deshalb kein Lauf über rund 25 Jahre hinaus, und eine Spätspiel-Messung war
	/// unmöglich. Der Fortbestand der Dynastie ist Kern des Spiels, keine Zufallshandlung.
	///
	/// Die Kirche lehnt von sich aus ab, wenn nichts zu werben ist – schon verheiratet, bereits werbend,
	/// kein passender Vorschlag, zu teuer. Jeder dieser Wege endet in einer schlichten Textmeldung, die
	/// die übliche Dialogbedienung wegklickt; Sonderfälle braucht es hier also nicht. Die Vorabfrage
	/// spart nur den Weg, damit die Kupplerin nicht jedes Jahr aufs Neue behelligt wird.
	/// </summary>
	private async Task WirbUmPartner()
	{
		if (!new FamilieManager().KannPartnerSuchen(out _))
			return;

		// Nur aus echtem Überschuss werben. Die Handelsrunde läuft im selben Zug vor der Kirche und hat
		// bis auf die Rücklage alles investiert; die Kupplerin nähme ihren Lohn also genau aus dem
		// Polster. Gemessen war das der Tropfen, der drei von zehn Läufen in den Schuldturm kippte.
		// Der doppelte Betrag, weil der Preis der Kupplerin vorher nicht feststeht – ihn abzufragen
		// hieße, den Vorschlag zweimal zu ziehen und damit den Zufallsstrom zu verschieben.
		if (SW.Dynamisch.GetAktHum().GetTaler() < _ruecklage * 2)
			return;

		var kirche = _main.GetNodeOrNull<Control>(nameof(Kirche));
		var hochzeit = kirche?.GetNodeOrNull<BaseButton>("AreaHochzeit");

		if (hochzeit == null)
		{
			_fehler.Add("In der Kirche wurde der Bereich „AreaHochzeit\" nicht gefunden.");
			return;
		}

		if (_ausfuehrlich)
			GD.Print("    Brautwerbung: Kupplerin aufgesucht");

		_bedienteKnoepfe.Add("Kirche.Brautwerbung");
		hochzeit.EmitSignal(BaseButton.SignalName.Pressed);

		// Die Zusage selbst faellt in der ueblichen Dialogbedienung: Ausserhalb einer Aktion drueckt die
		// den ersten sichtbaren Knopf, und das ist im Ja/Nein-Dialog „Ja". Ebenso beim jaehrlichen
		// Werbegeschenk, wo der erste Knopf die billigste echte Gabe ist – „nichts schenken" haengt der
		// BrautwerbungDialog bewusst hinten an, und es wuerde die Werbung nie voranbringen.
		await WarteBisBildschirmZurueck(nameof(Kirche), "Kirche.Brautwerbung");
	}

	/// <summary>
	/// Bestimmt im Testament einen Erben. Ohne diesen Schritt bleibt die Ehe wirkungslos:
	/// <c>FamilieManager.FuehreTestamentAus</c> liest <c>GetErbeSpielerID()</c>, und steht die auf 0,
	/// endet die Dynastie beim ersten Todesfall – **Kinder erben nicht von allein**. Gemessen: Mit Ehe,
	/// aber ohne Testament kam ein 40-Jahre-Lauf auf 25 bzw. 33 Jahre statt auf 40.
	///
	/// Gewählt wird die letzte Option, denn <c>GetErbeOptionen</c> listet in fester Reihenfolge das
	/// Erzbistum (Id 0, also kein Erbe), dann den Ehepartner, dann die Kinder. Ein Kind trägt die
	/// Dynastie weiter; der etwa gleichaltrige Ehepartner nur eine Generation.
	/// </summary>
	private async Task BestimmeErben()
	{
		var familie = new FamilieManager();
		var optionen = familie.GetErbeOptionen();

		// Nur das Erzbistum zur Wahl: unverheiratet und kinderlos, es gibt nichts zu bestimmen.
		if (optionen.Count <= 1)
			return;

		int ziel = optionen[optionen.Count - 1].ErbeId;

		if (familie.GetAktuellerErbeId() == ziel)
			return;

		var kirche = _main.GetNodeOrNull<Control>(nameof(Kirche));
		var bereich = kirche?.GetNodeOrNull<BaseButton>("AreaTestament");

		if (bereich == null)
		{
			_fehler.Add("In der Kirche wurde der Bereich „AreaTestament\" nicht gefunden.");
			return;
		}

		bereich.EmitSignal(BaseButton.SignalName.Pressed);

		if (!await WarteAufSichtbar(nameof(TestamentDialog)))
		{
			_fehler.Add("Das Testament ging nicht auf. " + Zustandsbericht());
			return;
		}

		// Ein einziger Knopf blaettert durch die Erben und setzt dabei jedes Mal den neuen. Hoechstens so
		// oft druecken, wie es Optionen gibt – dann ist die Liste einmal ganz herum.
		var knopf = _main.TestamentDialog.GetNodeOrNull<BaseButton>(_main.TestamentDialog.ButtonErbePath);

		for (int i = 0; knopf != null && i < optionen.Count && familie.GetAktuellerErbeId() != ziel; i++)
		{
			knopf.EmitSignal(BaseButton.SignalName.Pressed);
			await NaechsterFrame();
		}

		_bedienteKnoepfe.Add("Kirche.Testament");

		// Ueber den eigenen Schliessknopf, nicht per Rechtsklick: Der wirkt erst im naechsten Frame, und
		// bis dahin uebernimmt die allgemeine Dialogbedienung den noch offenen Dialog – sie drueckt
		// denselben Blaetterknopf weiter und stellte den eben bestimmten Erben gemessen bis zurueck aufs
		// Erzbistum. Der Schliessknopf ist jeder Knopf ausser dem Blaetterknopf.
		var knoepfe = new List<BaseButton>();
		SammleKnoepfe(_main.TestamentDialog, knoepfe);
		var schliessen = knoepfe.Find(k => k != knopf);

		if (schliessen != null)
			schliessen.EmitSignal(BaseButton.SignalName.Pressed);
		else
			SchickeAbbruch();

		// Vor dem Warten pruefen, solange der Stand noch der eben gesetzte ist.
		int erbe = familie.GetAktuellerErbeId();

		if (erbe == 0)
			_fehler.Add("Im Testament blieb der Erbe auf „kein Erbe\", obwohl " + optionen.Count
			            + " Optionen zur Wahl standen.");
		else if (_ausfuehrlich)
			GD.Print("    Testament: Erbe bestimmt – " + familie.GetErbeBezeichnung(erbe));

		await WarteBisBildschirmZurueck(nameof(Kirche), "Kirche.Testament");
	}
}
