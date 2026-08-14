using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(RegentCardPool))]
public class UnlimitedBladeWorks() : FBECardModel(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<UnlimitedBladeWorksPower>("power", 4m),
        new IntVar("power2", 3m),
        new ForgeVar(8)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => HoverTipFactory.FromForge()
        .Concat(HoverTipFactory.FromCardWithCardHoverTips<SovereignBlade>());


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await PowerCmd.Apply<UnlimitedBladeWorksPower>(choiceContext, Owner.Creature, DynamicVars["power"].BaseValue,
            Owner.Creature, this);
        await ForgeCmd.Forge(DynamicVars.Forge.IntValue, Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["power"].UpgradeValueBy(4m);
        DynamicVars["power2"].UpgradeValueBy(4m);
        DynamicVars.Forge.UpgradeValueBy(3m);
    }
}
