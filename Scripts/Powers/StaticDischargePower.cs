using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public class StaticDischargePower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Channeling),
        HoverTipFactory.FromOrb<LightningOrb>()
    ];

    public override PowerStackType StackType => PowerStackType.Counter;
    
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer != null && (props.IsPoweredAttack() || cardSource is Omnislice))
        {
            Flash();
            for (var i = 0; i < Amount; i++)
            {
                await OrbCmd.Channel<LightningOrb>(choiceContext, Owner.Player!);
            }
        }
    }
}
