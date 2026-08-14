using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public class SwordSagePower2 : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory
        .FromCardWithCardHoverTips<SovereignBlade>().Concat([HoverTipFactory.Static(StaticHoverTip.ReplayStatic)]);

    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomIconPath => ImageHelper.GetImagePath($"powers/{"SWORD_SAGE_POWER".ToLowerInvariant()}.png");

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        if (power is not SwordSagePower2 || power.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        var cards = Owner.Player?.PlayerCombatState?.AllCards ?? [];
        foreach (var item in cards)
        {
            if (item is SovereignBlade sovereignBlade)
            {
                item.BaseReplayCount += (int)amount;
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner != Owner.Player || card is not SovereignBlade)
        {
            return Task.CompletedTask;
        }

        card.BaseReplayCount += Amount;

        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        var cards = oldOwner.Player?.PlayerCombatState?.AllCards ?? [];
        foreach (var item in cards)
        {
            if (item is SovereignBlade sovereignBlade)
            {
                sovereignBlade.BaseReplayCount -= Amount;
            }
        }

        return Task.CompletedTask;
    }
}
