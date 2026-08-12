using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Reduces vanilla Mayhem's cost by 1.</summary>
[HarmonyPatch(typeof(Mayhem), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchMayhem
{
    private static readonly FieldInfo? CanonicalEnergyCostField =
        AccessTools.Field(typeof(CardModel), "<CanonicalEnergyCost>k__BackingField");

    private static void Postfix(Mayhem __instance)
    {
        CanonicalEnergyCostField?.SetValue(__instance, 1);
    }
}
