using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(SharedRelicPool))]
class TopHat : FBERelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<TheBomb>()];

    public override bool IsAllowed(IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> options,
        CardCreationOptions creationOptions)
    {
        if (Owner != player)
        {
            return false;
        }

        if (creationOptions.Source != CardCreationSource.Encounter)
        {
            return false;
        }

        var bomb = player.RunState.CreateCard(ModelDb.Card<TheBomb>(), player);
        var result = new CardCreationResult(bomb);
        result.ModifyCard(bomb, this); // 可选：让奖励界面显示这是被该遗物/效果修改出来的
        options.Add(result);

        return true;
    }

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card is not TheBomb || card.Owner.Creature != Owner.Creature)
        {
            modifiedCost = originalCost;
            return false;
        }

        modifiedCost = 0;
        return true;
    }
}
