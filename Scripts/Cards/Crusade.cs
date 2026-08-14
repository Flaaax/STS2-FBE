namespace FBE.Scripts.Cards;

using Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.CardPools;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(ColorlessCardPool))]
public class Crusade() : FBECardModel(2, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
        {
            Log.Warn("CombatState is null, this is not supposed to happen!");
            return;
        }
        
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await Cmd.Wait(0.25f);

        foreach (var player in CombatState.Players)
        {
            var hand = PileType.Hand.GetPile(player);
            var items = hand.Cards.Where(c =>
                c.Type == CardType.Attack && !c.Keywords.Contains(CardKeyword.Unplayable)).ToList();
            var cardModel = player.RunState.Rng.Shuffle.NextItem(items);
            if (cardModel != null)
            {
                await CardCmd.AutoPlay(choiceContext, cardModel, null);
            }
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
