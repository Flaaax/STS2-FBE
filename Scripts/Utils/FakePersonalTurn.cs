using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace FBE.Scripts.Utils;

/// <summary>
/// 模拟单个玩家的回合结束与开始，不会切换阵营、推进回合数或触发 side-turn hook。
/// </summary>
public static class FakePersonalTurn
{
	public static async Task Run(PlayerChoiceContext choiceContext, Player player)
	{
		var combatState = player.Creature.CombatState;
		var playerState = player.PlayerCombatState;
		if (combatState is null || playerState is null || CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		await playerState.OrbQueue.BeforeTurnEnd(choiceContext);
		if (CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		var hand = PileType.Hand.GetPile(player);
		var turnEndCards = hand.Cards.Where(card => card.HasTurnEndInHandEffect).ToList();
		var etherealCards = hand.Cards.Where(card => !card.HasTurnEndInHandEffect &&
		                                             card.Keywords.Contains(CardKeyword.Ethereal) &&
		                                             Hook.ShouldEtherealTrigger(combatState, card)).ToList();

		foreach (var card in etherealCards)
		{
			await CardCmd.Exhaust(choiceContext, card, causedByEthereal: true);
		}

		foreach (var card in turnEndCards)
		{
			await card.OnTurnEndInHandWrapper(choiceContext);
		}

		if (CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		await Hook.BeforeFlush(combatState, player);
		var cardsToFlush = new List<CardModel>();
		var cardsToRetain = new List<CardModel>();
		var shouldFlush = Hook.ShouldFlush(combatState, player);
		foreach (var card in hand.Cards)
		{
			if (!shouldFlush || card.ShouldRetainThisTurn)
			{
				cardsToRetain.Add(card);
			}
			else
			{
				cardsToFlush.Add(card);
			}
		}

		if (cardsToFlush.Count > 0)
		{
			await CardPileCmd.Add(cardsToFlush, PileType.Discard);
		}

		await Hook.AfterFlush(combatState, player, choiceContext, cardsToFlush, cardsToRetain);
		playerState.EndOfTurnCleanup();

		player.Creature.BeforeTurnStart(CombatSide.Player);
		await player.Creature.AfterTurnStart(CombatSide.Player);
		await Hook.AfterBlockCleared(combatState, player.Creature);

		if (Hook.ShouldPlayerResetEnergy(combatState, player))
		{
			SfxCmd.Play("event:/sfx/ui/gain_energy");
			playerState.ResetEnergy();
		}
		else
		{
			playerState.AddMaxEnergyToCurrent();
		}

		await Hook.AfterEnergyReset(combatState, player);
		await Hook.BeforeHandDraw(combatState, player, choiceContext);
		var handDraw = Hook.ModifyHandDraw(combatState, player, 5m, out var modifiers);
		await Hook.AfterModifyingHandDraw(combatState, modifiers);
		await CardPileCmd.Draw(choiceContext, handDraw, player, fromHandDraw: true);
		await Hook.AfterPlayerTurnStart(combatState, choiceContext, player);
		if (!CombatManager.Instance.IsOverOrEnding)
		{
			await playerState.OrbQueue.AfterTurnStart(choiceContext);
		}
	}
}