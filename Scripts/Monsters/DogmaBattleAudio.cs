using FBECore.Audio;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace FBE.Scripts.Monsters;

/// <summary>Dogma 战的本地背景音乐会话；负责替换并恢复原版战斗 BGM。</summary>
internal static class DogmaBattleAudio
{
	private const string ThemeLoopKey = "fbe.dogma.living_in_the_light";
	private const string ThemePath = "res://FBE/audio/Dogma/LivingInTheLight.mp3";
	private const string BrimstoneChargePath = "res://FBE/audio/Dogma/BrimstoneCharge.wav";
	private const string BrimstoneLaserPath = "res://FBE/audio/Dogma/BrimstoneLaser.wav";
	private static readonly string[] GodheadTearPaths =
	[
		"res://FBE/audio/Dogma/GodheadTear1.wav",
		"res://FBE/audio/Dogma/GodheadTear2.wav",
	];
	private static readonly string[] ScreamPaths =
	[
		"res://FBE/audio/Dogma/Scream1.wav",
		"res://FBE/audio/Dogma/Scream2.wav",
	];

	private static bool _active;
	private static bool _gameBgmSuppressed;
	private static bool _combatEndCleanupRegistered;
	private static bool _runExitCleanupRegistered;
	private static Node? _runForCleanup;

	public static void Start(ActModel act)
	{
		StopMusic();
		_active = true;
		RegisterCleanup();

		// 仅替换原版章节的 BGM；额外章节保留自身音乐，但仍启用 Dogma 音效与清理。
		if (act is not (Overgrowth or Underdocks or Hive or Glory))
			return;

		var audioManager = NAudioManager.Instance;
		if (audioManager != null)
		{
			audioManager.SetBgmVol(0f);
			_gameBgmSuppressed = true;
		}

		FbeAudio.PlayLoop(ThemePath, new AudioLoopOptions
		{
			LoopKey = ThemeLoopKey,
			Volume = 0.85f,
			VolumeChannel = AudioVolumeChannel.Bgm,
			RestartIfAlreadyPlaying = true,
		});
	}

	/// <summary>硫磺火的蓄力阶段音效。</summary>
	public static void PlayBrimstoneCharge() => PlayOneShotWhileActive(BrimstoneChargePath);

	/// <summary>硫磺火进入发射状态时的音效。</summary>
	public static void PlayBrimstoneLaser() => PlayOneShotWhileActive(BrimstoneLaserPath);

	/// <summary>Godhead 每一发射出时随机播放一段泪弹音效。</summary>
	public static void PlayGodheadTear()
	{
		if (_active)
			FbeAudio.PlayRandom(GodheadTearPaths);
	}

	/// <summary>Dogma 哭喊时随机播放一段尖叫音效。</summary>
	public static void PlayScream()
	{
		if (_active)
			FbeAudio.PlayRandom(ScreamPaths);
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
		if (_active)
			StopMusic();
	}

	private static void PlayOneShotWhileActive(string resourcePath)
	{
		if (_active)
			FbeAudio.PlayOneShot(resourcePath);
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
