using System.Collections.Generic;
using Godot;

namespace Conspiratio.Godot.assets.scripts.managers;

/// <summary>
/// Zentrale Audio-Verwaltung (Autoload): spielt Klang-Effekte über einen kleinen Player-Pool auf dem
/// Effekt-Bus und Hintergrundmusik über einen eigenen Player auf dem Musik-Bus. Die Musik wählt je
/// Kategorie ein zufälliges Stück, blendet beim Kategoriewechsel weich über und spielt am Stückende
/// automatisch das nächste (zufällige) Stück derselben Kategorie. Lautstärke/Stummschaltung laufen über
/// die Audio-Busse (siehe <see cref="AudioEinstellungen"/>).
/// </summary>
public partial class SoundManager : Node
{
	public static SoundManager Instance { get; private set; }

	/// <summary>Die Musik-Kategorien; jede verweist auf einen Ordner mit Stücken.</summary>
	public enum MusikKategorie
	{
		Keine,
		Intro,
		Standard,
		Kampf,
		Kirche,
		Hinterzimmer,
		Hochzeit,
		Geburt,
		Tod,
		Katastrophe,
		Orient,
		Outro
	}

	private const string BusEffekt = "Effekt";
	private const string BusMusik = "Musik";
	private const string BusStimmen = "Stimmen";
	private const int SfxPoolGroesse = 8;
	private const float StilleDb = -40f;
	private const double UeberblendDauer = 1.5;

	// Zu jeder Kategorie die Stücke (Ressourcenpfade). Neue Titel: Datei ablegen und Pfad ergänzen.
	private static readonly Dictionary<MusikKategorie, string[]> Musikstuecke = new()
	{
		[MusikKategorie.Intro] = new[]
		{
			"res://assets/music/intro/Song_Intro_BigHornsIntro2.mp3"
		},
		[MusikKategorie.Standard] = new[]
		{
			"res://assets/music/standard/Song_Standard_ATallShip.mp3",
			"res://assets/music/standard/Song_Standard_Conspiratio_Theme.mp3",
			"res://assets/music/standard/Song_Standard_LegendsOfTheRiver.mp3",
			"res://assets/music/standard/Song_Standard_Renaissance.mp3",
			"res://assets/music/standard/Song_Standard_Strobotone_Medieval_Theme_02.mp3",
			"res://assets/music/standard/Song_Standard_SundaysChild.mp3",
			"res://assets/music/standard/Song_Standard_TheMaster.mp3",
			"res://assets/music/standard/Song_Standard_TheMightyKingdom.mp3",
			"res://assets/music/standard/Song_Standard_TriumphantReturn.mp3",
			"res://assets/music/standard/Song_Standard_TroubledBridges.mp3",
			"res://assets/music/standard/Song_Standard_TurkishDance.mp3",
			"res://assets/music/standard/Song_Standard_WhatChildIsThis.mp3"
		},
		[MusikKategorie.Kampf] = new[]
		{
			"res://assets/music/kampf/Song_Kampf_EnemyShips.mp3",
			"res://assets/music/kampf/Song_Kampf_EpicTVTheme.mp3",
			"res://assets/music/kampf/Song_Kampf_HeroInPeril.mp3",
			"res://assets/music/kampf/Song_Kampf_HighTension.mp3",
			"res://assets/music/kampf/Song_Kampf_ImperialMorning.mp3",
			"res://assets/music/kampf/Song_Kampf_NightRunner.mp3",
			"res://assets/music/kampf/Song_Kampf_RememberTheHeroes.mp3",
			"res://assets/music/kampf/Song_Kampf_StalkingPrey.mp3",
			"res://assets/music/kampf/Song_Kampf_WarfareBed.mp3"
		},
		[MusikKategorie.Kirche] = new[]
		{
			"res://assets/music/kirche/Song_Kirche_TheAngelsWeep.mp3"
		},
		[MusikKategorie.Hinterzimmer] = new[]
		{
			"res://assets/music/hinterzimmer/Song_HZ_TemptingFate.mp3",
			"res://assets/music/hinterzimmer/Song_HZ_TheDeadlyYear.mp3",
			"res://assets/music/hinterzimmer/Song_HZ_TheTalk.mp3"
		},
		[MusikKategorie.Hochzeit] = new[]
		{
			"res://assets/music/hochzeit/Song_Hochzeit_30SecondClassical.mp3"
		},
		[MusikKategorie.Geburt] = new[]
		{
			"res://assets/music/geburt/Song_Geburt_Noel.mp3"
		},
		[MusikKategorie.Tod] = new[]
		{
			"res://assets/music/tod/Song_Tod_TheBigDecision.mp3"
		},
		[MusikKategorie.Katastrophe] = new[]
		{
			"res://assets/music/katastrophe/Song_Katastrophe_PendulumWaltz.mp3",
			"res://assets/music/katastrophe/Song_Katastrophe_RoadToKilcoo.mp3"
		},
		[MusikKategorie.Orient] = new[]
		{
			"res://assets/music/orient/Song_Orient_EgyptianCrawl.mp3",
			"res://assets/music/orient/Song_Orient_TemptationMarch.mp3",
			"res://assets/music/orient/Song_Orient_WheelOfKarma.mp3"
		},
		[MusikKategorie.Outro] = new[]
		{
			"res://assets/music/outro/Song_Outro_PartingGlass.mp3"
		}
	};

	// Effekt-Streams
	private AudioStream _sfxLinksklick;
	private AudioStream _sfxRechtsklick;
	private AudioStream _sfxCheckbox;
	private AudioStream _sfxMuenzen;
	private AudioStream _sfxFanfare;

	private AudioStreamPlayer[] _sfxPool;
	private int _sfxNaechster;

	private AudioStreamPlayer _stimme;
	private AudioStreamPlayer _musik;
	private MusikKategorie _aktuelleKategorie = MusikKategorie.Keine;
	private Tween _musikTween;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		Instance = this;
		_rng.Randomize();

		_sfxLinksklick = GD.Load<AudioStream>("res://assets/sounds/bongo_hell.wav");
		_sfxRechtsklick = GD.Load<AudioStream>("res://assets/sounds/bongo_dunkel.wav");
		_sfxCheckbox = GD.Load<AudioStream>("res://assets/sounds/checkbox_click.wav");
		_sfxMuenzen = GD.Load<AudioStream>("res://assets/sounds/muenzen.wav");
		_sfxFanfare = GD.Load<AudioStream>("res://assets/sounds/fanfare.wav");

		_sfxPool = new AudioStreamPlayer[SfxPoolGroesse];

		for (int i = 0; i < SfxPoolGroesse; i++)
		{
			var player = new AudioStreamPlayer { Bus = BusEffekt };
			AddChild(player);
			_sfxPool[i] = player;
		}

		_stimme = new AudioStreamPlayer { Bus = BusStimmen };
		AddChild(_stimme);

		_musik = new AudioStreamPlayer { Bus = BusMusik };
		AddChild(_musik);
		_musik.Finished += OnMusikstueckFertig;

		// Gespeicherte Lautstärke-Einstellungen auf die Audio-Busse anwenden.
		AudioEinstellungen.AlleAnwenden();
	}

	// --- Klang-Effekte (Aufrufe bleiben abwärtskompatibel) ---

	public void PlayLeftClick() => SpieleEffekt(_sfxLinksklick);

	public void PlayRightClick() => SpieleEffekt(_sfxRechtsklick);

	public void PlayCheckBoxClick() => SpieleEffekt(_sfxCheckbox);

	public void PlayCoins() => SpieleEffekt(_sfxMuenzen);

	public void PlayFanfare() => SpieleEffekt(_sfxFanfare);

	/// <summary>Startet die Titelmusik (wird vom Titelbildschirm aufgerufen).</summary>
	public void PlayIntro() => SpieleMusik(MusikKategorie.Intro);

	private void SpieleEffekt(AudioStream stream)
	{
		var player = _sfxPool[_sfxNaechster];
		_sfxNaechster = (_sfxNaechster + 1) % SfxPoolGroesse;

		player.Stream = stream;
		player.Play();
	}

	/// <summary>Spielt eine Sprachausgabe (Stimmen-Bus) vom angegebenen Ressourcenpfad; ein noch laufender Voice wird ersetzt.</summary>
	public void SpieleStimme(string pfad)
	{
		var stream = GD.Load<AudioStream>(pfad);

		if (stream == null)
			return;

		_stimme.Stream = stream;
		_stimme.Play();
	}

	// --- Musik ---

	/// <summary>
	/// Wechselt die Hintergrundmusik zur angegebenen Kategorie (blendet weich über). Läuft bereits ein
	/// Stück dieser Kategorie, passiert nichts. <see cref="MusikKategorie.Keine"/> blendet die Musik aus.
	/// </summary>
	public void SpieleMusik(MusikKategorie kategorie)
	{
		if (kategorie == _aktuelleKategorie && _musik.Playing)
			return;

		_aktuelleKategorie = kategorie;

		if (!Musikstuecke.TryGetValue(kategorie, out var pfade) || pfade.Length == 0)
		{
			MusikAusblenden();
			return;
		}

		var stream = LadeZufallsstueck(pfade);

		_musikTween?.Kill();

		if (_musik.Playing)
		{
			// Laufendes Stück ausblenden, dann das neue einblenden.
			_musikTween = CreateTween();
			_musikTween.TweenProperty(_musik, "volume_db", StilleDb, UeberblendDauer);
			_musikTween.TweenCallback(Callable.From(() => StarteStueck(stream, StilleDb)));
			_musikTween.TweenProperty(_musik, "volume_db", 0f, UeberblendDauer);
		}
		else
		{
			StarteStueck(stream, StilleDb);
			_musikTween = CreateTween();
			_musikTween.TweenProperty(_musik, "volume_db", 0f, UeberblendDauer);
		}
	}

	private void StarteStueck(AudioStream stream, float startLautstaerkeDb)
	{
		_musik.Stream = stream;
		_musik.VolumeDb = startLautstaerkeDb;
		_musik.Play();
	}

	private void OnMusikstueckFertig()
	{
		// Am Stückende nahtlos das nächste zufällige Stück derselben Kategorie anspielen.
		if (Musikstuecke.TryGetValue(_aktuelleKategorie, out var pfade) && pfade.Length > 0)
			StarteStueck(LadeZufallsstueck(pfade), 0f);
	}

	private void MusikAusblenden()
	{
		if (!_musik.Playing)
			return;

		_musikTween?.Kill();
		_musikTween = CreateTween();
		_musikTween.TweenProperty(_musik, "volume_db", StilleDb, UeberblendDauer);
		_musikTween.TweenCallback(Callable.From(() => _musik.Stop()));
	}

	private AudioStream LadeZufallsstueck(string[] pfade)
	{
		return GD.Load<AudioStream>(pfade[_rng.RandiRange(0, pfade.Length - 1)]);
	}
}
