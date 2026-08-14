using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers.Models;

/// <summary>Changes vanilla Acrobatics from Uncommon to Common.</summary>
[HarmonyPatch(typeof(Acrobatics), MethodType.Constructor)]
[HarmonyPatch([])]
internal static class PatchAcrobatics
{
    private static readonly FieldInfo? RarityBackingField =
        AccessTools.Field(typeof(CardModel), "<Rarity>k__BackingField");

    private static void Postfix(Acrobatics __instance)
    {
        RarityBackingField?.SetValue(__instance, CardRarity.Common);
    }
}
