using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.TestSupport;

namespace FBE.Scripts.Utils;

/// <summary>
/// 瓦库接管时的自动出牌流程。必须在玩家的自动出牌阶段中串行调用。
/// </summary>
public static class VakuuTakeover
{
	private const int MaxCardsToPlay = 13;

	private static readonly LocString ApprovalLine = new("relics", "FBE_RELIC_PLAN_C.vakuu_approval");
	private static readonly LocString WarningLine = new("relics", "FBE_RELIC_PLAN_C.vakuu_warning");
	private static readonly LocString[] TakeoverLines =
	[
		new LocString("relics", "FBE_RELIC_PLAN_C.vakuu_takeover_1"),
		new LocString("relics", "FBE_RELIC_PLAN_C.vakuu_takeover_2"),
		new LocString("relics", "FBE_RELIC_PLAN_C.vakuu_takeover_3"),
	];

	public static async Task Run(PlayerChoiceContext choiceContext, Player player)
	{
		var combatState = player.Creature.CombatState;
		if (combatState is null || player.Creature.IsDead)
		{
			return;
		}

		var takeoverLine = player.RunState.Rng.CombatCardSelection.NextItem(TakeoverLines)!;
		TalkCmd.Play(takeoverLine, player.Creature, VfxColor.Purple);
		await Cmd.Wait(0.5f);

		var cardsPlayed = 0;
		using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
		{
			var startTurn = player.PlayerCombatState!.TurnNumber;
			for (; cardsPlayed < MaxCardsToPlay; cardsPlayed++)
			{
				if (CombatManager.Instance.IsOverOrEnding || CombatManager.Instance.IsPlayerReadyToEndTurn(player) ||
				    player.PlayerCombatState.TurnNumber != startTurn)
				{
					break;
				}

				var hand = PileType.Hand.GetPile(player);
				var card = hand.Cards.FirstOrDefault(candidate => candidate.CanPlay());
				if (card is null)
				{
					break;
				}

				await card.SpendResources();
				await CardCmd.AutoPlay(choiceContext, card, GetTarget(card, player, combatState),
					AutoPlayType.Default, skipXCapture: true);
			}
		}

		if (CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead || cardsPlayed == 0)
		{
			return;
		}

		var line = cardsPlayed >= MaxCardsToPlay ? WarningLine : ApprovalLine;
		TalkCmd.Play(line, player.Creature, VfxColor.Purple);
		await Cmd.Wait(0.5f);
	}

	private static Creature? GetTarget(CardModel card, Player player, ICombatState combatState)
	{
		Rng combatTargets = player.RunState.Rng.CombatTargets;
		return card.TargetType switch
		{
			TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
			TargetType.AnyAlly => combatTargets.NextItem(combatState.Allies.Where(creature =>
				creature is { IsAlive: true, IsPlayer: true } && creature != player.Creature)),
			TargetType.AnyPlayer => player.Creature,
			_ => null,
		};
	}

	/// <summary>
	/// 接管期间自动处理卡牌效果提出的选择，行为与原版低语耳环一致。
	/// </summary>
	private sealed class VakuuCardSelector : ICardSelector
	{
		public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect,
			int maxSelect)
		{
			return Task.FromResult<IEnumerable<CardModel>>(options.Take(maxSelect).ToList());
		}

		public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
			IReadOnlyList<CardRewardAlternative> alternatives)
		{
			return new CardRewardSelection { card = options.FirstOrDefault()?.Card };
		}
	}
}
