using FBECore.Scripts.Keywords;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(ColorlessCardPool))]
public sealed class PolishTheJade() : FBECardModel(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => [FBECoreKeywords.Offclass];

	protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		if (!Hook.ShouldDraw(Owner.Creature.CombatState!, Owner, fromHandDraw: false, out var modifier))
		{
			if (modifier is null) return;
			await Hook.AfterPreventingDraw(Owner.Creature.CombatState!, modifier);
			return;
		}

		var combatState = Owner.Creature.CombatState;
		var hand = PileType.Hand.GetPile(Owner);
		var drawPile = PileType.Draw.GetPile(Owner);
		var discardPile = PileType.Discard.GetPile(Owner);
		var hasShuffled = false;

		// 此处不能使用 CardPileCmd.Draw：它会在抽牌堆为空时洗牌，无法限制洗牌条件和次数。
		for (var drawn = 0; drawn < DynamicVars.Cards.IntValue && hand.Cards.Count < CardPile.MaxCardsInHand;)
		{
			if (CombatManager.Instance.IsOverOrEnding)
			{
				break;
			}

			var card = drawPile.Cards.FirstOrDefault(candidate => candidate.Pool != Owner.Character.CardPool);
			if (card is null)
			{
				if (hasShuffled || drawPile.Cards.Count != 0 ||
				    discardPile.Cards.All(candidate => candidate.Pool == Owner.Character.CardPool))
				{
					break;
				}

				hasShuffled = true;
				await CardPileCmd.Shuffle(choiceContext, Owner);
				continue;
			}

			await CardPileCmd.Add(card, hand);
			CombatManager.Instance.History.CardDrawn(combatState!, card, fromHandDraw: false);
			await Hook.AfterCardDrawn(combatState!, choiceContext, card, fromHandDraw: false);
			card.InvokeDrawn();
			NDebugAudioManager.Instance?.Play("card_deal.mp3", 0.25f, PitchVariance.Small);
			drawn++;
		}
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Cards.UpgradeValueBy(1);
	}
}
