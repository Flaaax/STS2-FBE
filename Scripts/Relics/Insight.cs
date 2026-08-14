using System.Diagnostics;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.Relics;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
class Insight : FBERelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override string? CustomIconPath => "res://FBE/images/relics/Insight.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("RewardCount", 20m)
    ];


    public override bool HasUponPickupEffect => true;

    private List<CardCreationResult> GetCardReward()
    {
        var pools = ModelDb.AllCharacterCardPools.ToList();
        pools.Add(ModelDb.CardPool<ColorlessCardPool>());
        Debug.Assert(Owner != null, nameof(Owner) + " != null");
        pools.Remove(Owner.Character.CardPool);

        var options = CardCreationOptions.ForNonCombatWithUniformOdds(pools);
        return CardFactory.CreateForReward(Owner, DynamicVars["RewardCount"].IntValue, options).ToList();
    }

    public override async Task AfterObtained()
    {
        var rewards = GetCardReward();

        var perfs =
            new CardSelectorPrefs(L10NLookup("FBE_RELIC_INSIGHT.selectionScreenPrompt"), 0,
                rewards.Count);
        var selected =
            (await CardSelectCmd.FromSimpleGridForRewards(new BlockingPlayerChoiceContext(), rewards, Owner, perfs))
            .ToList();
        if (selected.Count != 0)
        {
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(selected, PileType.Deck));
        }
    }
}
