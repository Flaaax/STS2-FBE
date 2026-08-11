using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.Cards;

[Pool(typeof(IroncladCardPool))]
public class Demon() : FBECardModel(3, CardType.Power, CardRarity.Rare, TargetType.None)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        //new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        //HoverTipFactory.FromKeyword(CardKeyword.Ethereal), HoverTipFactory.FromCard<DemonForm>()
        HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DemonFormPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        AudioHelper.Play("res://FBE/audio/devil_card.wav");
        await PowerCmd.Apply<DemonPower>(choiceContext, Owner.Creature, 1m, Owner.Creature,
            this);

        if (IsUpgraded)
        {
            await PowerCmd.Apply<DemonFormPower>(choiceContext, Owner.Creature, 1m, Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        //AddKeyword(CardKeyword.Innate);
    }
}