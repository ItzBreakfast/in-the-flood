using Godot;
using Godot.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using MEC;

public partial class PlayerAudio : Node3D
{
	private RandomNumberGenerator _rng = new RandomNumberGenerator();
	private AnimationTree _animator;

	[Export] public string movementBus;
	private int _movementBusIndex;

	[ExportCategory("Audio")]

	[ExportSubgroup("Wind")]
	[Export] private AudioStreamPlayer3D _windRun;
	[Export] private float _minWindVolume = -80.0f;
	[Export] private float _maxWindVolume = -20.0f;
	[Export] private float _minWindPitch = 1.0f;
	[Export] private float _maxwindPitch = 1.2f;
	[Export] private float _maxPlayerVelocity = 15;
	[Export] private float _windLerpSpeed = 2.0f;
	[Export] private float _velocityExponent = 2.0f;
	private Vector3 _playerVelocity;

	[ExportSubgroup("Sprinting")]
	[Export] private float _minSpeedScale = 0.7f;
	[Export] private float _maxSpeedScale = 1.1f;

	[ExportSubgroup("Sliding")]
	[Export] private AudioStreamPlayer3D _slideIn;
	[Export] private AudioStreamPlayer3D _slideLoop;

	[ExportSubgroup("Landing")]
	[Export] private AudioStreamPlayer3D _land;
	[Export] private Array<AudioConditionPlayer> _landPlayers;


	[ExportSubgroup("Step sounds")]
	[Export] private AudioStreamPlayer3D _step;
	[Export] private Array<AudioGroupPlayer> _stepPlayers;
	private string _currentGroup;

	[ExportSubgroup("Vaulting")]
	[Export] private AudioStreamPlayer3D _vaultIn;
	[Export] private AudioStreamPlayer3D _vaultOut;

	[ExportSubgroup("Wallrun")]
	[Export] private AudioStreamPlayer3D _wallrun;

    public override void _Ready()
    {
		CollisionChecker.OnGroupChange += GetStepGroup;
		PlayerAir.LastVelocityChangeLanded += PlayAudioLandCondition;

		PlayerSlide.SlideStartChange += PlaySlideSound;
		PlayerSlide.SlideCurrentChange += PlaySlideLoop;

		PlayerMovement.VelocityChange += PlayWindVelocity;

		PlayerVault.PlayerVaulted += PlayVaultIn;
		//PlayerVault.PlayerVaultEnded += PlayVaultOut;

		PlayerWallrun.WallRunStart += PlayWallRun;
		PlayerWallrun.WallRunEnd += StopWallRun;

		PlayerVerticalWallrun.VerticalWallRunStart += PlayWallRun;
		PlayerVerticalWallrun.VerticalWallRunEnd += StopWallRun;

		_animator = Owner.GetNode<AnimationTree>("AnimationTree");
		_movementBusIndex = AudioServer.GetBusIndex(movementBus);
    }

    public override void _ExitTree()
    {
		CollisionChecker.OnGroupChange -= GetStepGroup;

        PlayerAir.LastVelocityChangeLanded -= PlayAudioLandCondition;
		PlayerSlide.SlideStartChange -= PlaySlideSound;
		PlayerSlide.SlideCurrentChange -= PlaySlideLoop;

		PlayerMovement.VelocityChange -= PlayWindVelocity;

		PlayerVault.PlayerVaulted -= PlayVaultIn;
		//PlayerVault.PlayerVaultEnded -= PlayVaultOut;

		PlayerWallrun.WallRunStart -= PlayWallRun;
		PlayerWallrun.WallRunEnd -= StopWallRun;

		PlayerVerticalWallrun.VerticalWallRunStart -= PlayWallRun;
		PlayerVerticalWallrun.VerticalWallRunEnd -= StopWallRun;
    }

    public override void _Process(double delta)
    {
        Vector3 playerVelocity = _playerVelocity;

		float velocityMagnitude = playerVelocity.Length() / _maxPlayerVelocity;
		float velocityScale = Mathf.Pow(velocityMagnitude, _velocityExponent);

		float desiredVolume = Mathf.Lerp(_minWindVolume, _maxWindVolume, velocityScale);
		float desiredPitch = Mathf.Lerp(_minWindPitch, _maxwindPitch, velocityScale);

		desiredVolume = Mathf.Clamp(desiredVolume, _minWindVolume, _maxWindVolume);
		desiredPitch = Mathf.Clamp(desiredPitch, _minWindPitch, _maxwindPitch);

		_windRun.VolumeDb = Mathf.Lerp(_windRun.VolumeDb, desiredVolume, _windLerpSpeed * (float)delta);
		_windRun.PitchScale = Mathf.Lerp(_windRun.PitchScale, desiredPitch, _windLerpSpeed * (float)delta);

		ChangeSprintSound(velocityScale);
    }

	private void ChangeSprintSound(float scale)
	{
		float desiredSpeed = Mathf.Lerp(_minSpeedScale, _maxSpeedScale, scale * 1.5f);

		desiredSpeed = Mathf.Clamp(desiredSpeed, _minSpeedScale, _maxSpeedScale);

		_animator.Set("parameters/moveState/move/sprint/TimeScale/scale", desiredSpeed);
	}
    
	private void PlayVaultIn()
	{
		_vaultIn.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_vaultIn.Play();
	}

	private void PlayVaultOut()
	{
		_vaultOut.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_vaultOut.Play();
	}

	private void PlayWallRun()
	{
		Timing.KillCoroutines("Wallrun");

		_wallrun.PitchScale = 1f;

		if (!_wallrun.Playing)
			_wallrun.Play();

		AudioEffect lowPass = AudioServer.GetBusEffect(_movementBusIndex, 2);
		lowPass.Set("cutoff_hz", 3000f);
	}

	private void StopWallRun(Node node)
	{
		Timing.RunCoroutine(StopWallRunSound(node).CancelWith(this), "Wallrun");
	}

	private IEnumerator<double> StopWallRunSound(Node node)
	{
		float timeElapsed = 0f;
		float duration = 0.3f;

		AudioEffect lowPass = AudioServer.GetBusEffect(_movementBusIndex, 2);
		float initialLowpass = (float)lowPass.Get("cutoff_hz");

		do
		{
			timeElapsed += (float)node.GetProcessDeltaTime();
			float normalizedTime = timeElapsed / duration;

			_wallrun.PitchScale = Mathf.Lerp(1f, 0.1f, normalizedTime);
			lowPass.Set("cutoff_hz", Mathf.Lerp(initialLowpass, 20500f, normalizedTime));

			yield return Timing.WaitForOneFrame;
		}
		while (timeElapsed < duration);

		_wallrun.Stop();
	}

	private void GetStepGroup(string curGroup)
	{
		_currentGroup = curGroup;
	}

	private void PlayWindVelocity(Vector3 velocity)
	{
		_playerVelocity = velocity;
	}
    
	public void PlayStepSound()
	{
		// fail safe default
		if (_currentGroup == string.Empty || _currentGroup == null)
			_currentGroup = "Concrete";
		
		LoadStepSound(_currentGroup);

		_step.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_step.Play();
	}

	private void LoadStepSound(string group)
	{
		// loop through through the audio groups
		foreach (AudioGroupPlayer player in _stepPlayers)
		{
			// loop through the groups
			foreach (string curGroup in player.groups)
			{
				if (group == curGroup)
				{
					_step.Stream = player.streams[_rng.RandiRange(0, player.streams.Length - 1)];

					// break if group is found
					break;
				}
			}
		}
	}

	public void PlayAudio(AudioStream stream)
	{
		_step.Stream = stream;

		_step.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_step.Play();
	}

	public void PlayAnyAudio(NodePath player, AudioStream stream)
	{
		AudioStreamPlayer3D streamPlayer = (AudioStreamPlayer3D)GetNode(player);
		
		streamPlayer.Stream = stream;

		streamPlayer.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		streamPlayer.Play();
	}

	private void PlaySlideSound()
	{
		_slideIn.PitchScale = _rng.RandfRange(0.9f, 1.1f);
		_slideIn.Play();
	}

	private void PlaySlideLoop(float timer)
	{
		// Start looping sound
		if (timer > 0.05f)
		{
			if (!_slideLoop.Playing)
				_slideLoop.Play();
			
			_slideLoop.PitchScale = 1f * Mathf.InverseLerp(0f, 1f, timer);
		}
		// Stop looping sound
		else if (timer <= 0f)
		{
			_slideLoop.Stop();
		}
	}

	public void StopCurrentAudio(NodePath player)
	{
		AudioStreamPlayer3D streamPlayer = (AudioStreamPlayer3D)GetNode(player);

		streamPlayer.Stop();
	}

	private void PlayAudioLandCondition(Vector3 lastVelocity)
	{
		foreach (AudioConditionPlayer player in _landPlayers)
		{
			if (Mathf.Abs(lastVelocity.Y) > player.value)
			{
				// loop through the groups
				foreach (string curGroup in player.groups)
				{
					if (_currentGroup == curGroup)
					{
						_land.Stream = player.stream;

						_land.PitchScale = _rng.RandfRange(0.9f, 1.1f);
						_land.Play();

						break;
					}
					
				}
			}
		}
	}
}
