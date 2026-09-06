using FBECore.Audio;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 古烈战的本地音频会话。主题曲和原版 BGM 的切换在整场战斗内只执行一次。
/// </summary>
internal static class GuileBattleAudio
{
	private const string ThemeLoopKey = "fbe.guile.theme";
	private const string ThemePath = "res://FBE/audio/Guile/GuileTheme.mp3";
	private const string CrescentPath = "res://FBE/audio/Guile/Crescent.wav";
	private const string Die1Path = "res://FBE/audio/Guile/Die1.wav";
	private const string Die2Path = "res://FBE/audio/Guile/Die2.wav";
	private const string IkuzouPath = "res://FBE/audio/Guile/Ikuzou.wav";
	private const string KoPath = "res://FBE/audio/Guile/KO.wav";
	private const string PerfectKoPath = "res://FBE/audio/Guile/Perfect KO.wav";
	private const string ShootingPath = "res://FBE/audio/Guile/Shooting.wav";
	private const string WinPath = "res://FBE/audio/Guile/Win.wav";

	private static readonly HashSet<Creature> PlayerCreatures = [];
	private static bool _active;
	private static bool _gameBgmSuppressed;
	private static bool _combatEndCleanupRegistered;
	private static bool _runExitCleanupRegistered;
	private static bool _playersWereDamaged;
	private static bool _guileDefeatSoundPlayed;
	private static Node? _runForCleanup;
	private static Guile? _guile;

	public static void Start(Guile guile)
	{
		StopMusic();

		_guile = guile;
		PlayerCreatures.Clear();
		PlayerCreatures.UnionWith(guile.CombatState.PlayerCreatures);
		_active = true;
		_playersWereDamaged = false;
		_guileDefeatSoundPlayed = false;

		var audioManager = NAudioManager.Instance;
		if (audioManager != null)
		{
			audioManager.SetBgmVol(0f);
			_gameBgmSuppressed = true;
		}

		// 主题曲使用 BGM 音量，短音效则保留 FBECore 默认的 SFX 音量。
		FbeAudio.PlayLoop(ThemePath, new AudioLoopOptions
		{
			LoopKey = ThemeLoopKey,
			VolumeChannel = AudioVolumeChannel.Bgm,
			RestartIfAlreadyPlaying = true,
		});
		RegisterCleanup();
	}

	public static void MarkPlayerHurt(Creature creature, decimal delta)
	{
		if (!_active || delta >= 0m || !PlayerCreatures.Contains(creature))
			return;

		_playersWereDamaged = true;
	}

	public static void PlayShooting() => PlayOneShotWhileActive(ShootingPath);

	public static void PlaySecondEmpower() => PlayOneShotWhileActive(IkuzouPath);

	public static void PlaySomersault() => PlayOneShotWhileActive(CrescentPath);

	public static void PlayGuileDefeat()
	{
		if (!_active || _guileDefeatSoundPlayed)
			return;

		_guileDefeatSoundPlayed = true;
		if (_playersWereDamaged)
		{
			FbeAudio.PlayOneShot(KoPath, new AudioPlaybackOptions
			{
				StartPositionSeconds = 0.3f,
			});
		}
		else
		{
			FbeAudio.PlayOneShot(PerfectKoPath);
		}
		FbeAudio.PlayRandom([Die1Path, Die2Path], new AudioPlaybackOptions
		{
			DelaySeconds = 0.5f,
		});
	}

	private static void PlayOneShotWhileActive(string resourcePath)
	{
		if (_active)
			FbeAudio.PlayOneShot(resourcePath);
	}

	private static void RegisterCleanup()
	{
		if (!_combatEndCleanupRegistered)
		{
			CombatManager.Instance.CombatEnded += OnCombatEnded;
			_combatEndCleanupRegistered = true;
		}

		if (_runExitCleanupRegistered)
			return;

		var run = NRun.Instance;
		if (run == null)
			return;

		run.TreeExiting += StopMusic;
		_runForCleanup = run;
		_runExitCleanupRegistered = true;
	}

	private static void OnCombatEnded(CombatRoom _)
	{
		if (!_active)
			return;

		if (PlayerCreatures.Any(creature => creature.IsDead))
		{
			_guile?.ForceStandingVictoryPose();
			FbeAudio.PlayOneShot(WinPath, new AudioPlaybackOptions
			{
				DelaySeconds = 1f,
			});
		}

		StopMusic();
	}

	private static void StopMusic()
	{
		UnregisterCleanup();
		FbeAudio.StopLoop(ThemeLoopKey);

		if (_gameBgmSuppressed)
		{
			NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);
			_gameBgmSuppressed = false;
		}

		_active = false;
		PlayerCreatures.Clear();
		_guile = null;
	}

	private static void UnregisterCleanup()
	{
		if (_combatEndCleanupRegistered)
		{
			CombatManager.Instance.CombatEnded -= OnCombatEnded;
			_combatEndCleanupRegistered = false;
		}

		if (_runExitCleanupRegistered && _runForCleanup != null && GodotObject.IsInstanceValid(_runForCleanup))
			_runForCleanup.TreeExiting -= StopMusic;

		_runForCleanup = null;
		_runExitCleanupRegistered = false;
	}
}
