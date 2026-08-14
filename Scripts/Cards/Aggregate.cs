using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(ColorlessCardPool))]
public class Aggregate() : FBECardModel(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("EnergyPerCard", 4m),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("CalculatedEnergy").WithMultiplier(CalcEnergy)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await PlayerCmd.GainEnergy(((CalculatedVar)DynamicVars["CalculatedEnergy"]).Calculate(cardPlay.Target), Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["EnergyPerCard"].UpgradeValueBy(-1m);
    }

    private static decimal CalcEnergy(CardModel card, Creature? _)
    {
        return Math.Floor(PileType.Draw.GetPile(card.Owner).Cards.Count / card.DynamicVars["EnergyPerCard"].BaseValue);
    }
}
