using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

[Pool(typeof(ColorlessCardPool))]
public class InvestmentPromotion() : FBECardModel(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
	protected override IEnumerable<DynamicVar> CanonicalVars => [];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => GetHoverTip();

	private IEnumerable<IHoverTip> GetHoverTip()
	{
		if (!CombatManager.Instance.IsInProgress) return [];

		var card = GetCard();
		return card is null ? [] : [HoverTipFactory.FromCard(card)];
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		var card = GetCard();
		if (card is null) return;
#if STS2_Beta
		var card2 = card.CreateCloneForPlayer(Owner);
#else
        var card2 = card.CreateClone();
        CombatState!.RemoveCard(card2);
        CombatState.AddCard(card2, Owner);
#endif
		await CardPileCmd.AddGeneratedCardToCombat(card2, PileType.Hand, Owner);
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}

	private CardModel? GetCard()
	{
		var entry = CombatManager.Instance.History.CardPlaysFinished.LastOrDefault(e => e.CardPlay.Card.Owner != Owner);
		return entry?.CardPlay.Card;
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		var card = GetCard();
		var title = card?.Title;
		description.Add("CardTitle", title ?? "");
		description.Add("HasCardTitle", title is null ? 0m : 1m);
	}
}
