using Godot;
using MegaCrit.Sts2.Core.Assets;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Afflictions;

/// <summary>
/// A visual-only affliction whose overlay obscures the entire card with animated television static.
/// It deliberately uses the base non-stackable behavior: each card can receive it at most once.
/// </summary>
[RegisterAffliction]
public sealed class NoSignal : ModAfflictionTemplate
{
	public const string OverlayScenePath = "res://FBE/scenes/afflictions/no_signal.tscn";

	public override AfflictionAssetProfile AssetProfile => new(
		OverlayScenePath: OverlayScenePath
	);

	/// <summary>
	/// Places the overlay in the vanilla cache for the current combat. This is needed because the
	/// card-affliction overlay path ultimately uses <see cref="PreloadManager" /> on some game builds.
	/// </summary>
	public static void CacheOverlayForCombat()
	{
		if (PreloadManager.Cache.ContainsKey(OverlayScenePath))
		{
			return;
		}

		PackedScene? overlayScene = ResourceLoader.Load<PackedScene>(OverlayScenePath);
		if (overlayScene == null)
		{
			GD.PushError($"[NoSignal] Failed to load overlay scene: {OverlayScenePath}");
			return;
		}

		PreloadManager.Cache.SetAsset(OverlayScenePath, overlayScene);
	}
}
