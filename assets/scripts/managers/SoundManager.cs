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
	public NodePath AudioStreamPlayerIntroPath { get; set; }
	
	private AudioStreamPlayer _audioStreamPlayerLeftClick;
	private AudioStreamPlayer _audioStreamPlayerRightClick;
	private AudioStreamPlayer _audioStreamPlayerIntro;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
		_audioStreamPlayerLeftClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerLeftClickPath);
		_audioStreamPlayerRightClick = GetNode<AudioStreamPlayer>(AudioStreamPlayerRightClickPath);
		_audioStreamPlayerIntro = GetNode<AudioStreamPlayer>(AudioStreamPlayerIntroPath);
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

	public void PlayIntro()
	{
		_audioStreamPlayerIntro.Play();
	}
}