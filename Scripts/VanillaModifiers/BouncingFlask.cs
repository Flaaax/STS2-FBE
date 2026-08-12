using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Rebalances vanilla Bouncing Flask to lower Poison and more hits.</summary>
[HarmonyPatch(typeof(BouncingFlask))]
internal static class PatchBouncingFlaskCanonicalVars
{
    private static MethodBase TargetMethod() =>
        AccessTools.PropertyGetter(typeof(BouncingFlask), "CanonicalVars");

    private static bool Prefix(ref IEnumerable<DynamicVar> __result)
    {
        __result =
        [
            new PowerVar<PoisonPower>(2m),
            new RepeatVar(4)
        ];
        return false;
    }
}

[HarmonyPatch(typeof(BouncingFlask), "OnUpgrade")]
internal static class PatchBouncingFlaskUpgrade
{
    private static bool Prefix(BouncingFlask __instance)
    {
        __instance.DynamicVars.Repeat.UpgradeValueBy(2m);
        return false;
    }
}
