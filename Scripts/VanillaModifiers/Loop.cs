using FBE.Scripts.Cards;
using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>
/// FBE's replacement for the vanilla Loop card.  It keeps the original card's
/// cost, rarity, art, and upgrade value, but triggers at the end of the owner's turn.
/// </summary>
[Pool(typeof(DefectCardPool))]
public sealed class LoopAtTurnEnd : FBECardModel
{
    public LoopAtTurnEnd() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    // Reuse the original portrait instead of shipping a duplicate asset.
    public override string PortraitPath =>
        ImageHelper.GetImagePath("atlases/card_atlas.sprites/defect/loop.tres");

    public override string BetaPortraitPath =>
        ImageHelper.GetImagePath("atlases/card_atlas.sprites/defect/beta/loop.tres");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Loop", 1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
        await PowerCmd.Apply<LoopAtTurnEndPower>(choiceContext, Owner.Creature,
            DynamicVars["Loop"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Loop"].UpgradeValueBy(1m);
    }
}

/// <summary>
/// Performs Loop's extra passive triggers immediately before the orb queue's
/// normal end-of-turn triggers. This mirrors vanilla Loop's ordering at turn start.
/// </summary>
public sealed class LoopAtTurnEndPower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // Reuse Loop Power's existing icon instead of adding a second icon asset.
    public override string? CustomIconPath => ImageHelper.GetImagePath("powers/loop_power.png");

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner))
        {
            return;
        }

        var orbs = Owner.Player?.PlayerCombatState?.OrbQueue.Orbs;
        if (orbs == null || orbs.Count == 0)
        {
            return;
        }

        for (var i = 0; i < Amount; i++)
        {
            await OrbCmd.Passive(choiceContext, orbs[0], null);
            await Cmd.Wait(0.25f);
        }
    }
}

/// <summary>
/// Keeps vanilla Loop out of Defect rewards and the card library's unlocked set.
/// The replacement card is supplied by its Pool attribute above.
/// </summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
internal static class LockVanillaLoopInCardPool
{
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance is DefectCardPool)
        {
            __result = __result.Where(card => card.GetType() != typeof(Loop)).ToList();
        }
    }
}
