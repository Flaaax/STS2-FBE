using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public sealed class KeepingAHandPower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomIconPath => ImageHelper.GetImagePath("atlases/power_atlas.sprites/" + "Well_Laid_Plans_Power".ToLowerInvariant() + ".tres");
    
    public override Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        try
        {
            if (player != Owner.Player || !Hook.ShouldFlush(player.Creature.CombatState!, player))
            {
                return Task.CompletedTask;
            }

            var cards = PileType.Hand.GetPile(player).Cards.Where(c => !c.ShouldRetainThisTurn).ToList();
        
            if (cards.Count == 0)
            {
                return Task.CompletedTask;
            }
        
            player.RunState.Rng.Shuffle.Shuffle(cards);
            // 从大到小排序
            var sortedCards = cards
                .OrderByDescending(GetEffectiveEnergy)
                .ToList();

            var selectedCards = sortedCards.Take(Amount).ToList();
        
            foreach (var item in selectedCards)
            {
                item.GiveSingleTurnRetain();
            }

            return Task.CompletedTask;

            // 定义一个辅助函数获取排序用的值
            int GetEffectiveEnergy(CardModel card)
            {
                return card.EnergyCost.CostsX ? int.MaxValue : // X 费用视为最大
                    card.EnergyCost.GetResolved(); // 否则用当前能量
            }
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}
