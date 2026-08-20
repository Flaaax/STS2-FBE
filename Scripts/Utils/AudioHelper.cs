using FBECore.Audio;
using Godot;

namespace FBE.Scripts.Utils;

/// <summary>
/// Compatibility facade for FBE's established audio helper API.
/// New shared consumers should call <see cref="FbeAudio"/> directly.
/// </summary>
public static class AudioHelper
{
    public static bool Play(string resPath, float volume = 1f)
    {
        return FbeAudio.PlayOneShot(resPath, new AudioPlaybackOptions { Volume = volume }).Accepted;
    }

    public static bool PlayRandom(string resPath, float volume = 1f)
    {
        try
        {
            var candidates = GetRandomAudioCandidates(resPath);
            if (candidates.Count == 0)
            {
                GD.PushWarning($"[AudioHelper] No random audio variants found for: {resPath}");
                return false;
            }

            return FbeAudio.PlayRandom(candidates, new AudioPlaybackOptions { Volume = volume }).Accepted;
        }
        catch (Exception ex)
        {
            GD.PushError($"[AudioHelper] PlayRandom failed: {resPath}\n{ex}");
            return false;
        }
    }

    public static bool PlayLoop(string resPath, float volume = 1f, bool restartIfAlreadyPlaying = false)
    {
        return FbeAudio.PlayLoop(resPath, new AudioLoopOptions
        {
            Volume = volume,
            LoopKey = resPath,
            RestartIfAlreadyPlaying = restartIfAlreadyPlaying,
        }) is not null;
    }

    public static bool StopLoop(string key)
    {
        return FbeAudio.StopLoop(key);
    }

    public static bool IsLoopPlaying(string key)
    {
        return FbeAudio.IsLoopPlaying(key);
    }

    private static List<string> GetRandomAudioCandidates(string resPath)
    {
        var normalizedPath = resPath.Replace('\\', '/').Trim();
        if (!normalizedPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            return [];

        var slashIndex = normalizedPath.LastIndexOf('/');
        var extensionIndex = normalizedPath.LastIndexOf('.');
        if (extensionIndex <= slashIndex)
            extensionIndex = normalizedPath.Length;

        var digitIndex = extensionIndex - 1;
        if (digitIndex <= slashIndex || !char.IsDigit(normalizedPath[digitIndex]))
            return [];

        var prefix = normalizedPath[..digitIndex];
        var suffix = normalizedPath[extensionIndex..];
        List<string> candidates = [];
        for (var i = 0; i <= 9; i++)
        {
            var candidate = $"{prefix}{i}{suffix}";
            if (ResourceLoader.Exists(candidate))
                candidates.Add(candidate);
        }

        return candidates;
    }
}
