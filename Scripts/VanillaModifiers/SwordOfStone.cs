using System.Diagnostics;
using System.Reflection;
using FBE.Scripts.Relics;
using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Replaces the Sword of Stone reward from Sunken Statue with FBE's reworked relic.</summary>
[Pool(typeof(EventRelicPool))]
internal sealed class SwordOfStoneMk2 : FBERelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShowCounter => true;

    protected override string CustomIconPath => "res://FBE/images/relics/sword_of_stone.png";

    public override int DisplayAmount => Math.Max(DynamicVars["HealthToLose"].IntValue - HealthLost, 0);

    public bool Complete => DisplayAmount == 0;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("HealthToLose", 120m)];

    private int _healthLost;

    [SavedProperty]
    public int HealthLost
    {
        get => _healthLost;
        private set
        {
            AssertMutable();
            _healthLost = value;
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner.Creature || Complete || result.UnblockedDamage == 0)
        {
            return;
        }

        Flash();
        HealthLost += result.UnblockedDamage;
        Status = Complete ? RelicStatus.Active : RelicStatus.Normal;
        InvokeDisplayAmountChanged();

        if (!CombatManager.Instance.IsInProgress && Complete)
        {
            await ReplaceWithSwordOfJade();
        }
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Complete)
        {
            await ReplaceWithSwordOfJade();
        }
    }

    private async Task ReplaceWithSwordOfJade()
    {
        Flash();
        await RelicCmd.Replace(this, ModelDb.Relic<SwordOfJade>().ToMutable());
    }
}

[HarmonyPatch(typeof(SunkenStatue))]
internal static class PatchSunkenStatueVars
{
    private static MethodBase TargetMethod() =>
        AccessTools.PropertyGetter(typeof(SunkenStatue), "CanonicalVars");

    private static bool Prefix(ref IEnumerable<DynamicVar> __result)
    {
        __result =
        [
            new StringVar("Relic", ModelDb.Relic<SwordOfStoneMk2>().Title.GetFormattedText()),
            new GoldVar(111),
            new DynamicVar("HpLoss", 7m)
        ];
        return false;
    }
}

[HarmonyPatch(typeof(SunkenStatue), "GenerateInitialOptions")]
internal static class PatchSunkenStatueOptions
{
    private static bool Prefix(SunkenStatue __instance, ref IReadOnlyList<EventOption> __result)
    {
        var grabSword = AccessTools.MethodDelegate<Func<Task>>(
            AccessTools.Method(typeof(SunkenStatue), "GrabSword"), __instance);
        var diveIntoWater = AccessTools.MethodDelegate<Func<Task>>(
            AccessTools.Method(typeof(SunkenStatue), "DiveIntoWater"), __instance);

        __result =
        [
            new EventOption(__instance, grabSword, "SUNKEN_STATUE.pages.INITIAL.options.GRAB_SWORD",
                HoverTipFactory.FromRelic<SwordOfStoneMk2>()),
            new EventOption(__instance, diveIntoWater, "SUNKEN_STATUE.pages.INITIAL.options.DIVE_INTO_WATER")
                .ThatDoesDamage(__instance.DynamicVars["HpLoss"].BaseValue)
        ];
        return false;
    }
}

[HarmonyPatch(typeof(SunkenStatue), "GrabSword")]
internal static class PatchSunkenStatueReward
{
    private static bool Prefix(SunkenStatue __instance, ref Task __result)
    {
        __result = PatchHelper.WrapAsync(async () =>
        {
            Debug.Assert(__instance.Owner != null, "__instance.Owner != null");
            await RelicCmd.Obtain<SwordOfStoneMk2>(__instance.Owner);

            var eventType = typeof(SunkenStatue);
            var l10NLookup = eventType.GetMethod("L10NLookup", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(eventType.FullName, "L10NLookup");
            var description = (LocString?)l10NLookup.Invoke(__instance,
                ["SUNKEN_STATUE.pages.GRAB_SWORD.description"])
                ?? throw new MissingMethodException(eventType.FullName, "L10NLookup");
            var setEventFinished = eventType.GetMethod("SetEventFinished", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(eventType.FullName, "SetEventFinished");

            setEventFinished.Invoke(__instance, [description]);
        });
        return false;
    }
}
