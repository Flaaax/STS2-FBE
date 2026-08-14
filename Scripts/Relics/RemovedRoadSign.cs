using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace FBE.Scripts.Relics;

// 加入哪个遗物池，此处为通用
[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public class RemovedRoadSign : FBERelicModel
{
    // 稀有度
    public override RelicRarity Rarity => RelicRarity.Event;

    public override string? CustomIconPath => "res://FBE/images/relics/RemovedRoadSign.png";


    private int _combatsLeft = 2;

    public override bool IsUsedUp => CombatsLeft <= 0;

    public override bool ShowCounter => true;

    public override int DisplayAmount => Math.Max(0, CombatsLeft);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new("Combats", CombatsLeft)
    ];

    [SavedProperty]
    private int CombatsLeft
    {
        get => _combatsLeft;
        set
        {
            AssertMutable();
            _combatsLeft = value;
            DynamicVars["Combats"].BaseValue = _combatsLeft;
            InvokeDisplayAmountChanged();
            if (IsUsedUp)
            {
                Status = RelicStatus.Disabled;
            }
        }
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (!IsUsedUp && room is CombatRoom croom)
        {
            Flash();
            await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
                1m, null, null);
            // await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
            //     1m, null, null);
            // await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
            //     1m, null, null);
            
            CombatsLeft--;
        }
    }
}
