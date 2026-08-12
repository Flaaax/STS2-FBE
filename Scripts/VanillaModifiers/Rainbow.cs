using System.Reflection;
using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Reworks vanilla Rainbow into a Power that creates Orb slots and random Orbs.</summary>
[HarmonyPatch(typeof(Rainbow), "get_ExtraHoverTips")]
internal static class PatchRainbowHoverTips
{
    private static bool Prefix(ref IEnumerable<IHoverTip> __result)
    {
        __result = [HoverTipFactory.Static(StaticHoverTip.Channeling)];
        return false;
    }
}

[HarmonyPatch(typeof(Rainbow))]
internal static class PatchRainbowKeywords
{
    private static MethodBase TargetMethod() =>
        AccessTools.PropertyGetter(typeof(Rainbow), "CanonicalKeywords");

    private static bool Prefix(ref IEnumerable<CardKeyword> __result)
    {
        __result = Array.Empty<CardKeyword>();
        return false;
    }
}

[HarmonyPatch(typeof(Rainbow))]
internal static class PatchRainbowVars
{
    private static MethodBase TargetMethod() =>
        AccessTools.PropertyGetter(typeof(Rainbow), "CanonicalVars");

    private static bool Prefix(ref IEnumerable<DynamicVar> __result)
    {
        __result = [new RepeatVar(4)];
        return false;
    }
}

[HarmonyPatch(typeof(Rainbow), MethodType.Constructor)]
internal static class PatchRainbowConstructor
{
    private static void Postfix(Rainbow __instance)
    {
        FieldPatcher.Set(__instance, "Type", CardType.Power);
    }
}

[HarmonyPatch(typeof(Rainbow), "OnPlay")]
internal static class PatchRainbowPlay
{
    private static bool Prefix(Rainbow __instance, ref Task __result, PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        __result = PatchHelper.WrapAsync(async () =>
        {
            await CreatureCmd.TriggerAnim(__instance.Owner.Creature, "Cast", __instance.Owner.Character.CastAnimDelay);
            await OrbCmd.AddSlots(__instance.Owner, __instance.DynamicVars.Repeat.IntValue);
            await Cmd.Wait(0.25f);
            for (var i = 0; i < __instance.DynamicVars.Repeat.IntValue; i++)
            {
                await OrbCmd.Channel(choiceContext,
                    OrbModel.GetRandomOrb(__instance.Owner.RunState.Rng.CombatOrbGeneration).ToMutable(),
                    __instance.Owner);
            }

            await PowerCmd.Apply<RainbowPower>(choiceContext, __instance.Owner.Creature,
                __instance.DynamicVars.Repeat.BaseValue, __instance.Owner.Creature, __instance);
        });
        return false;
    }
}

[HarmonyPatch(typeof(Rainbow), "OnUpgrade")]
internal static class PatchRainbowUpgrade
{
    private static bool Prefix(Rainbow __instance)
    {
        __instance.DynamicVars.Repeat.UpgradeValueBy(1m);
        return false;
    }
}

/// <summary>Tracks the temporary Orb slots granted by the Rainbow rework.</summary>
public sealed class RainbowPower : FBEPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override string CustomIconPath => "res://FBE/images/powers/RainbowPower.png";

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        OrbCmd.RemoveSlots(Owner.Player, 1);
        await PowerCmd.Decrement(this);
    }
}
