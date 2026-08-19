using FBECore.Scripts.Keywords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

/// <summary>
/// A status that replicates itself into its owner's draw pile whenever they draw during their turn.
/// </summary>
[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(StatusCardPool))]
public sealed class Scrutiny() : FBECardModel(1, CardType.Status, CardRarity.Status, TargetType.None)
{
    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
        FBECoreKeywords.WhileInHand,
    ];

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (fromHandDraw || Pile?.Type != PileType.Hand || card.Owner != Owner ||
            card.Owner.Creature.CombatState!.CurrentSide != card.Owner.Creature.Side)
        {
            return;
        }

        var scrutiny = CreateClone();
        var result = await CardPileCmd.AddGeneratedCardToCombat(scrutiny, PileType.Draw, Owner,
            CardPilePosition.Random);
        CardCmd.PreviewCardPileAdd(result);
    }
}
