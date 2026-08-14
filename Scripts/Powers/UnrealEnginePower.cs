using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public class UnrealEnginePower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Ethereal), HoverTipFactory.ForEnergy(this)];
    public override PowerStackType StackType => PowerStackType.Counter;
    
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner.Player)
        {
            CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
        }
        return Task.CompletedTask;
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (var item in Owner.Player!.PlayerCombatState!.AllCards)
        {
            CardCmd.ApplyKeyword(item, CardKeyword.Ethereal);
        }
        return Task.CompletedTask;
    }
    
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        return player != Owner.Player ? amount : amount + Amount;
    }
}
