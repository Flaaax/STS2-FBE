using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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

	/// <summary>侵蚀持续至该牌所属玩家的回合结束；打出卡牌不会提前恢复。</summary>
	public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
		IEnumerable<Creature> participants)
	{
		if (side == CombatSide.Player && participants.Contains(Card.Owner.Creature))
			CardCmd.ClearAffliction(Card);
		return Task.CompletedTask;
	}

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
