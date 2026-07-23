using Godot;

namespace Conspiratio.Godot.assets.scripts.managers;

public partial class SoundManager : Node
{
	public static SoundManager Instance { get; private set; }
	
	[Export]
	public NodePath AudioStreamPlayerLeftClickPath { get; set; }
	
	[Export]
	public NodePath AudioStreamPlayerRightClickPath { get; set; }
	
	[Export]
	public NodePath AudioStreamPlayerCheckBoxClick { get; set; }
	
	[Export]
	public NodePath AudioStreamPlayerIntroPath { get; set; }

	[Export]
	public NodePath AudioStreamPlayerCoinsPath { get; set; }

	private AudioStreamPlayer _audioStreamPlayerLeftClick;
	private AudioStreamPlayer _audioStreamPlayerRightClick;
	private AudioStreamPlayer _audioStreamPlayerCheckBoxClick;
	private AudioStreamPlayer _audioStreamPlayerIntro;
	private AudioStreamPlayer _audioStreamPlayerCoins;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
		_audioStreamPlayerLeftClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerLeftClickPath);
		_audioStreamPlayerRightClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerRightClickPath);
		_audioStreamPlayerCheckBoxClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerCheckBoxClick);
		_audioStreamPlayerIntro = GetNode<AudioStreamPlayer>(AudioStreamPlayerIntroPath);
		_audioStreamPlayerCoins = GetNode<AudioStreamPlayer>(AudioStreamPlayerCoinsPath);

		// Gespeicherte Lautstärke-Einstellungen auf die Audio-Busse anwenden.
		AudioEinstellungen.AlleAnwenden();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void PlayLeftClick()
	{
		_audioStreamPlayerLeftClick.Play();
	}
	
	public void PlayRightClick()
	{
		_audioStreamPlayerRightClick.Play();
	}
	
	public void PlayCheckBoxClick()
	{
		_audioStreamPlayerCheckBoxClick.Play();
	}

	public void PlayIntro()
	{
		_audioStreamPlayerIntro.Play();
	}

	public void PlayCoins()
	{
		_audioStreamPlayerCoins.Play();
	}
}