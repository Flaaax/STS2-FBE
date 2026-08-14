using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Changes vanilla Eternal Armor into a 10/13 Skill.</summary>
[HarmonyPatch(typeof(EternalArmor), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchEternalArmor
{
    private static readonly FieldInfo? TypeBackingField =
        AccessTools.Field(typeof(CardModel), "<Type>k__BackingField");

    private static void Postfix(EternalArmor __instance)
    {
        __instance.DynamicVars["PlatingPower"].BaseValue = 10m;
        TypeBackingField?.SetValue(__instance, CardType.Skill);
    }
}
