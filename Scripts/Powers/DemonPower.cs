using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public class DemonPower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        //HoverTipFactory.FromKeyword(CardKeyword.Ethereal), HoverTipFactory.FromCard<DemonForm>()
        HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DemonFormPower>()
    ];

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount > 0m && power is StrengthPower && power.Owner == Owner && power is not ITemporaryPower)
        {
            Flash();
            await PowerCmd.Apply<DemonFormPower>(choiceContext, Owner, Amount, Owner, null);
        }
    }

    // public override async Task BeforeHandDraw(Player player, PlayerChoiceContext _, ICombatState combatState)
    // {
    //     if (player != Owner.Player)
    //     {
    //         return;
    //     }
    //
    //     IList<CardModel> cards = [];
    //     for (var i = 0; i < Amount; i++)
    //     {
    //         var card = combatState.CreateCard<DemonForm>(Owner.Player);
    //         CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
    //         cards.Add(card);
    //     }
    //
    //     Flash();
    //     await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, Owner.Player);
    // }
}
