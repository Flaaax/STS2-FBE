using FBE.Scripts.Enchantments;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public class UnlimitedBladeWorksPower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory
        .FromCardWithCardHoverTips<SovereignBlade>();

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://FBE/images/powers/UnlimitedBladeWorksPower.png";

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        if (power is not UnlimitedBladeWorksPower || power.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        var enumerable = Owner.Player?.PlayerCombatState?.AllCards ?? [];
        foreach (var item in enumerable)
        {
            if (item is not SovereignBlade sovereignBlade) continue;
            sovereignBlade.SetRepeats(Amount);
            CardCmd.ClearEnchantment(sovereignBlade);
            CardCmd.Enchant<UbwEnchantment>(sovereignBlade, Amount);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner != Owner.Player || card is not SovereignBlade sovereignBlade)
        {
            return Task.CompletedTask;
        }

        sovereignBlade.SetRepeats(Amount);
        CardCmd.ClearEnchantment(sovereignBlade);
        CardCmd.Enchant<UbwEnchantment>(sovereignBlade, Amount);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        var enumerable = oldOwner.Player?.PlayerCombatState?.AllCards ?? [];
        foreach (var item in enumerable)
        {
            if (item is not SovereignBlade sovereignBlade) continue;
            sovereignBlade.SetRepeats(1m);
            CardCmd.ClearEnchantment(sovereignBlade);
        }

        return Task.CompletedTask;
    }
}
