using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Reworks vanilla Coolant into a Power that channels Frost every turn.</summary>
[HarmonyPatch(typeof(Coolant), "OnPlay")]
internal static class PatchCoolantPlay
{
    private static bool Prefix(Coolant __instance, ref Task __result, PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        __result = PatchHelper.WrapAsync(async () =>
        {
            await CreatureCmd.TriggerAnim(__instance.Owner.Creature, "Cast", __instance.Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<CoolantPower>(choiceContext, __instance.Owner.Creature, 1,
                __instance.Owner.Creature, __instance);
        });
        return false;
    }
}

[HarmonyPatch(typeof(Coolant), "get_ExtraHoverTips")]
internal static class PatchCoolantHoverTips
{
    private static bool Prefix(ref IEnumerable<IHoverTip> __result)
    {
        __result =
        [
            HoverTipFactory.Static(StaticHoverTip.Channeling),
            HoverTipFactory.FromOrb<FrostOrb>()
        ];
        return false;
    }
}

[HarmonyPatch(typeof(Coolant), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchCoolantConstructor
{
    private static void Postfix(Coolant __instance)
    {
        FieldPatcher.Set(__instance, "CanonicalEnergyCost", 1);
    }
}

[HarmonyPatch(typeof(Coolant), "OnUpgrade")]
internal static class PatchCoolantUpgrade
{
    private static bool Prefix(Coolant __instance)
    {
        __instance.AddKeyword(CardKeyword.Innate);
        return false;
    }
}

/// <summary>Power applied by the Coolant rework.</summary>
public sealed class CoolantPower : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://FBE/images/powers/coolant_power.png";

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner != player.Creature)
        {
            return;
        }

        for (var i = 0; i < Amount; i++)
        {
            await OrbCmd.Channel<FrostOrb>(choiceContext, player);
        }
    }
}
