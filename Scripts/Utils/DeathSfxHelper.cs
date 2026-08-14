using Godot;

namespace FBE.Scripts.Utils;

/// <summary>
/// Plays one random player-death sound from FBE's dedicated death-audio directory.
/// </summary>
public static class DeathSfxHelper
{
	private const string DeathAudioDirectory = "res://FBE/audio/death";

	public static bool PlayRandom(float volume = 1f)
	{
		var directory = DirAccess.Open(DeathAudioDirectory);
		if (directory == null)
		{
			GD.PushWarning($"[DeathSfxHelper] Audio directory is unavailable: {DeathAudioDirectory}");
			return false;
		}

		var candidates = directory
			.GetFiles()
			.Select(fileName => $"{DeathAudioDirectory}/{fileName}")
			.Where(IsSupportedAudioFile)
			.Where(f => ResourceLoader.Exists(f))
			.ToArray();

		if (candidates.Length == 0)
		{
			GD.PushWarning($"[DeathSfxHelper] No playable death audio was found in: {DeathAudioDirectory}");
			return false;
		}

		return AudioHelper.Play(candidates[Random.Shared.Next(candidates.Length)], volume);
	}

	private static bool IsSupportedAudioFile(string resourcePath)
	{
		return resourcePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
		       resourcePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
		       resourcePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
	}
}