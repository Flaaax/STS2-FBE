using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace FBE.Scripts.Powers;

[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public class MasterRealityPower : FBEPowerModel
{
    // 类型，Buff或Debuff
    public override PowerType Type => PowerType.Buff;

    // 叠加类型，Counter表示可叠加，Single表示不可叠加
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator != null && creator.Creature == Owner)
        {
            Flash();
            CardCmd.Upgrade(card);
        }

        return Task.CompletedTask;
    }
}
