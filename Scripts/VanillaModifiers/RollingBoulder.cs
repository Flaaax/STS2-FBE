using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Reduces Rolling Boulder's cost and increases its scaling values.</summary>
[HarmonyPatch(typeof(RollingBoulder), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchRollingBoulderConstructor
{
    private static readonly FieldInfo? CanonicalEnergyCostField =
        AccessTools.Field(typeof(CardModel), "<CanonicalEnergyCost>k__BackingField");

    private static void Postfix(RollingBoulder __instance)
    {
        __instance.DynamicVars["RollingBoulderPower"].BaseValue = 10m;
        __instance.DynamicVars["IncrementAmount"].BaseValue = 10m;
        CanonicalEnergyCostField?.SetValue(__instance, 2);
    }
}

[HarmonyPatch(typeof(RollingBoulder), "OnUpgrade")]
internal static class PatchRollingBoulderUpgrade
{
    private static void Postfix(RollingBoulder __instance)
    {
        __instance.DynamicVars["RollingBoulderPower"].UpgradeValueBy(5m);
    }
}

[HarmonyPatch(typeof(RollingBoulderPower), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchRollingBoulderPower
{
    private static void Postfix(RollingBoulderPower __instance)
    {
        __instance.DynamicVars.Damage.BaseValue =
            ModelDb.Card<RollingBoulder>().DynamicVars["IncrementAmount"].BaseValue;
    }
}
