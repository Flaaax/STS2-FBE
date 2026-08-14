using FBE.Scripts.Utils;

namespace FBE.Scripts.Relics;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
class Redemption : FBERelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(30m)
    ];
    
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (room.RoomType == RoomType.Boss)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        }
    }
}
