using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Sets vanilla Bolas to 7 damage and 10 damage when upgraded.</summary>
[HarmonyPatch(typeof(Bolas), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchBolasConstructor
{
    private static void Postfix(Bolas __instance)
    {
        __instance.DynamicVars.Damage.BaseValue = 7m;
    }
}

[HarmonyPatch(typeof(Bolas), "OnUpgrade")]
[HarmonyPatch([])]
internal static class PatchBolasUpgrade
{
    private static void Postfix(Bolas __instance)
    {
        __instance.DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
