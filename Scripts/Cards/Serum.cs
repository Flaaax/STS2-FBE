using MegaCrit.Sts2.Core.HoverTips;

namespace FBE.Scripts.Cards;

using Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(SilentCardPool))]
public class Serum() : FBECardModel(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        EnergyHoverTip
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        var list = PileType.Hand.GetPile(Owner).Cards
            .Where(c => c.Type is CardType.Status or CardType.Curse).ToList();

        foreach (var item in list)
        {
            await CardCmd.Exhaust(choiceContext, item);
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1);
    }
}
