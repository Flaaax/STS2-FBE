using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Enchantments;

//Should not be enchanted to a curse
public class Quantinized : FBEEnchantmentModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Transform)];
    private CardPoolModel? _pool;

    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card)
               && card.IsTransformable;
    }

    protected override void OnEnchant()
    {
        _pool = Card.Type != CardType.Quest &&
                Card.Rarity is not (CardRarity.Event or CardRarity.Ancient or CardRarity.Token)
            ? Card.Pool
            : ModelDb.CardPool<ColorlessCardPool>();
    }

    private CardModel GetTransformTarget()
    {
        var otherPools = ModelDb.AllCharacterCardPools.Except([_pool!]);
        var rng = Card.Owner.RunState.Rng.CombatCardSelection;
        var targetPool = rng.NextFloat() <= 0.8 ? _pool : rng.NextItem(otherPools);

        var options = targetPool?.GetUnlockedCards(Card.Owner.UnlockState, Card.RunState!.CardMultiplayerConstraint)
            .Where(c => c.Id != Card.Id && c.IsRemovable && c.IsTransformable && c.Rarity != CardRarity.Basic) ?? [];
        var canonicalTarget = rng.NextItem(options) ??
                              throw new InvalidOperationException($"No valid card for {Card.Title} to transform!");

        var targetCard = Card.CardScope!.CreateCard(canonicalTarget, Card.Owner);

        if (Card.IsUpgraded && targetCard.MaxUpgradeLevel > 0)
        {
            CardCmd.Upgrade(targetCard);
        }

        return targetCard;
    }

    public async Task TransformSelf()
    {
        // A Thieving Hopper can restore the deck card only as a pending reward.
        // Such a card is back in the run state, but has no pile until the reward is claimed.
        // Do not consume combat RNG or create a replacement for an invalid transform target.
        if (Card.HasBeenRemovedFromState || Card.Pile is null)
        {
            return;
        }

        var targetCard = GetTransformTarget();

        var result = await CardCmd.Transform(Card, targetCard, CardPreviewStyle.None);

        if (result is { success: true })
        {
            var cardAdded = result.Value.cardAdded;
            if (ModelDb.Enchantment<Quantinized>().CanEnchant(cardAdded))
            {
                CardCmd.Enchant<Quantinized>(cardAdded, 1m);
            }
        }
    }

    // public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    // {
    //     if (card != Card || Card.Pile is null) return;
    //     await TransformSelf();
    // }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (card != Card || Card.Pile is null) return;
        if (Card.Pile.Type == PileType.Draw)
        {
            await TransformSelf();
        }
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        await TransformSelf();
    }
}
